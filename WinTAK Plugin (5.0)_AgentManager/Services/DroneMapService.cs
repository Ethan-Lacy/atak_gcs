using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Xml;
using WinTak.CursorOnTarget.Services;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    /// <summary>
    /// Manages drone and mission markers on the WinTAK map using CoT messages
    /// </summary>
    [Export(typeof(DroneMapService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DroneMapService
    {
        private readonly ICotMessageSender _cotSender;
        private Dictionary<byte, string> _droneMarkerUids;
        private Dictionary<string, string> _waypointMarkerUids; // key: "droneId_seq"
        private Dictionary<byte, int> _drawnMissionCounts; // Track how many waypoints drawn per drone

        [ImportingConstructor]
        public DroneMapService(ICotMessageSender cotSender)
        {
            _cotSender = cotSender;
            _droneMarkerUids = new Dictionary<byte, string>();
            _waypointMarkerUids = new Dictionary<string, string>();
            _drawnMissionCounts = new Dictionary<byte, int>();
        }

        /// <summary>
        /// Update or create drone position marker on map
        /// </summary>
        public void UpdateDroneMarker(DroneState drone)
        {
            try
            {
                if (drone.Position == null) return;

                // Get or create UID for this drone
                if (!_droneMarkerUids.ContainsKey(drone.SystemId))
                {
                    _droneMarkerUids[drone.SystemId] = Guid.NewGuid().ToString();
                }
                string uid = _droneMarkerUids[drone.SystemId];

                // Create CoT XML for drone marker
                string cotXml = CreateDroneCoT(
                    uid,
                    drone.SystemId,
                    drone.Position.Latitude,
                    drone.Position.Longitude,
                    drone.Position.Altitude,
                    drone.Position.Heading,
                    drone.Position.GroundSpeed,
                    drone.Armed,
                    drone.FlightMode,
                    drone.Battery?.Percentage ?? -1
                );

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cotXml);
                _cotSender.Send(xmlDoc);

                System.Diagnostics.Debug.WriteLine($"Updated drone marker for Drone {drone.SystemId} at ({drone.Position.Latitude:F6}, {drone.Position.Longitude:F6}) heading {drone.Position.Heading:F0}°");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating drone marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Draw mission waypoints and route for a drone (only if changed)
        /// </summary>
        public void DrawMission(byte droneId, List<MissionWaypoint> waypoints)
        {
            try
            {
                if (waypoints == null || waypoints.Count == 0) return;

                // Only redraw if waypoint count changed (mission updated)
                if (_drawnMissionCounts.ContainsKey(droneId) && _drawnMissionCounts[droneId] == waypoints.Count)
                {
                    return; // Mission already drawn, skip
                }

                // Clear old waypoint markers for this drone
                ClearMissionMarkers(droneId);

                // Create parent route object (parent for all waypoints)
                string routeKey = $"{droneId}_route";
                if (!_waypointMarkerUids.ContainsKey(routeKey))
                {
                    _waypointMarkerUids[routeKey] = Guid.NewGuid().ToString();
                }
                string parentUid = _waypointMarkerUids[routeKey];

                // Send parent route first
                string routeXml = CreateMissionRouteCoT(parentUid, droneId, waypoints);
                var routeDoc = new XmlDocument();
                routeDoc.LoadXml(routeXml);
                _cotSender.Send(routeDoc);

                // Create child waypoint markers (linked to parent route)
                for (int i = 0; i < waypoints.Count; i++)
                {
                    var wp = waypoints[i];
                    string wpKey = $"{droneId}_{wp.Sequence}";
                    string wpUid = Guid.NewGuid().ToString();
                    _waypointMarkerUids[wpKey] = wpUid;

                    string wpXml = CreateWaypointCoT(
                        wpUid,
                        parentUid,
                        droneId,
                        wp.Sequence,
                        wp.Latitude,
                        wp.Longitude,
                        wp.Altitude,
                        wp.Command,
                        wp.IsCurrent,
                        wp.IsReached
                    );

                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(wpXml);
                    _cotSender.Send(xmlDoc);
                }

                // Track that we've drawn this mission
                _drawnMissionCounts[droneId] = waypoints.Count;

                System.Diagnostics.Debug.WriteLine($"Drew mission with {waypoints.Count} waypoints and route for Drone {droneId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing mission: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear mission markers for a drone
        /// </summary>
        public void ClearMissionMarkers(byte droneId)
        {
            try
            {
                var keysToRemove = new List<string>();
                foreach (var key in _waypointMarkerUids.Keys)
                {
                    if (key.StartsWith($"{droneId}_"))
                    {
                        // Send delete CoT message
                        string deleteXml = CreateDeleteCoT(_waypointMarkerUids[key]);
                        var xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(deleteXml);
                        _cotSender.Send(xmlDoc);
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _waypointMarkerUids.Remove(key);
                }

                // Clear the drawn count so mission can be redrawn
                if (_drawnMissionCounts.ContainsKey(droneId))
                {
                    _drawnMissionCounts.Remove(droneId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing mission markers: {ex.Message}");
            }
        }

        private string CreateDroneCoT(string uid, byte systemId, double lat, double lon, double alt, double heading, double speed, bool armed, string flightMode, int battery)
        {
            string timeStart = DateTime.UtcNow.ToString("o");
            string stale = DateTime.UtcNow.AddMinutes(5).ToString("o");

            // Use different CoT types based on armed status
            // a-f-A-M-F-Q = Friendly Aircraft (MIL-STD-2525C)
            string cotType = armed ? "a-f-A-M-F-Q" : "a-f-A-C-F"; // Armed = military aircraft, disarmed = civilian

            string callsign = $"Drone {systemId}";
            string remarks = $"Mode: {flightMode ?? "UNKNOWN"}";
            if (battery >= 0)
            {
                remarks += $" | Battery: {battery}%";
            }

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<event version=""2.0"" uid=""{uid}"" type=""{cotType}"" how=""m-g"" time=""{timeStart}"" start=""{timeStart}"" stale=""{stale}"">
  <point lat=""{lat:F6}"" lon=""{lon:F6}"" hae=""{alt:F1}"" ce=""10.0"" le=""10.0"" />
  <detail>
    <contact callsign=""{callsign}"" />
    <remarks>{remarks}</remarks>
    <color value=""-65536"" />
    <usericon iconsetpath=""COT_MAPPING_2525C/a-f-A-M-F-Q"" />
    <precisionlocation altsrc=""GPS"" />
    <track course=""{heading:F1}"" speed=""{speed:F1}"" />
    <_agent_ systemid=""{systemId}"" armed=""{armed}"" mode=""{flightMode}"" battery=""{battery}"" />
  </detail>
</event>";
        }

        private string CreateWaypointCoT(string uid, string parentUid, byte droneId, int seq, double lat, double lon, double alt, string command, bool isCurrent, bool isReached)
        {
            string timeStart = DateTime.UtcNow.ToString("o");
            string stale = DateTime.UtcNow.AddMinutes(60).ToString("o"); // Waypoints stay longer

            // Use b-m-p-s-m type (battle dimension - space/missile - missile) for route waypoints
            string cotType = "b-m-p-s-m";

            string callsign = $"Waypoint {seq}";
            string remarks = $"{command}";
            if (isCurrent) remarks += " [CURRENT]";
            if (isReached) remarks += " [REACHED]";

            // Color: Orange=pending (#FFFF33 = -51361), Green=current, Gray=reached (matching QGC style)
            string color = isReached ? "-7829368" : (isCurrent ? "-16711936" : "-51361");

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<event version=""2.0"" uid=""{uid}"" type=""{cotType}"" how=""m-g"" time=""{timeStart}"" start=""{timeStart}"" stale=""{stale}"">
  <point lat=""{lat:F6}"" lon=""{lon:F6}"" hae=""{alt:F1}"" ce=""0.0"" le=""0.0"" />
  <detail>
    <contact callsign=""{callsign}"" />
    <precisionlocation geopointsrc=""???"" altsrc=""???"" />
    <status readiness=""true"" />
    <archive />
    <link uid=""{parentUid}"" production_time=""{timeStart}"" type=""b-m-p-s-m"" parent_callsign=""Mission {droneId}"" relation=""p-p"" />
    <usericon iconsetpath=""COT_MAPPING_SPOTMAP/b-m-p-s-m/{color}"" />
    <color argb=""{color}"" />
    <remarks>{remarks}</remarks>
    <_waypoint_ droneid=""{droneId}"" sequence=""{seq}"" command=""{command}"" current=""{isCurrent}"" reached=""{isReached}"" />
  </detail>
</event>";
        }

        private string CreateMissionRouteCoT(string uid, byte droneId, List<MissionWaypoint> waypoints)
        {
            string timeStart = DateTime.UtcNow.ToString("o");
            string stale = DateTime.UtcNow.AddMinutes(60).ToString("o");

            // Use first waypoint as the primary point
            var firstWp = waypoints[0];

            // Build polyline link elements connecting all waypoints and loiter circles
            var linkElements = new System.Text.StringBuilder();
            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                linkElements.AppendLine($"      <link point=\"{wp.Latitude:F6},{wp.Longitude:F6}\" />");

                // Add loiter circle visualization for loiter commands
                if (IsLoiterCommand(wp.Command))
                {
                    double radius = wp.Param3 > 0 ? wp.Param3 : 50.0; // Default 50m if not specified
                    var circlePoints = GenerateCirclePoints(wp.Latitude, wp.Longitude, radius, 16);
                    foreach (var point in circlePoints)
                    {
                        linkElements.AppendLine($"      <link point=\"{point.Item1:F6},{point.Item2:F6}\" />");
                    }
                    // Close the circle back to first point
                    linkElements.AppendLine($"      <link point=\"{circlePoints[0].Item1:F6},{circlePoints[0].Item2:F6}\" />");
                }
            }

            // CoT type for shapes/polylines - use u-d-f (drawing/freehand) for visible lines
            string cotType = "u-d-f";

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<event version=""2.0"" uid=""{uid}"" type=""{cotType}"" how=""h-g-i-g-o"" time=""{timeStart}"" start=""{timeStart}"" stale=""{stale}"">
  <point lat=""{firstWp.Latitude:F6}"" lon=""{firstWp.Longitude:F6}"" hae=""{firstWp.Altitude:F1}"" ce=""1.0"" le=""1.0"" />
  <detail>
    <contact callsign=""Mission {droneId}"" />
    <link_attr color=""-51361"" type=""b-m-p-s-m"" method=""Driving"" direction=""infil"" />
    <strokeColor value=""-51361"" />
    <strokeWeight value=""4.0"" />
    <fillColor value=""0"" />
    <labels_on value=""false"" />
    <_route_ droneid=""{droneId}"" waypoints=""{waypoints.Count}"" />
{linkElements.ToString().TrimEnd()}
  </detail>
</event>";
        }

        private bool IsLoiterCommand(string command)
        {
            // MAV_CMD loiter types: LOITER_UNLIM (17), LOITER_TURNS (18), LOITER_TIME (19)
            return command != null && (
                command.Contains("LOITER_UNLIM") ||
                command.Contains("LOITER_TURNS") ||
                command.Contains("LOITER_TIME") ||
                command.Contains("LOITER"));
        }

        private List<Tuple<double, double>> GenerateCirclePoints(double centerLat, double centerLon, double radiusMeters, int numPoints)
        {
            var points = new List<Tuple<double, double>>();

            // Convert radius from meters to degrees (approximate)
            // At equator: 1 degree ≈ 111,320 meters
            double latRadius = radiusMeters / 111320.0;
            double lonRadius = radiusMeters / (111320.0 * Math.Cos(centerLat * Math.PI / 180.0));

            for (int i = 0; i < numPoints; i++)
            {
                double angle = 2.0 * Math.PI * i / numPoints;
                double lat = centerLat + latRadius * Math.Sin(angle);
                double lon = centerLon + lonRadius * Math.Cos(angle);
                points.Add(Tuple.Create(lat, lon));
            }

            return points;
        }

        private string CreateDeleteCoT(string uid)
        {
            string timeStart = DateTime.UtcNow.ToString("o");
            string stale = DateTime.UtcNow.AddSeconds(-1).ToString("o"); // Stale time in past = delete

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<event version=""2.0"" uid=""{uid}"" type=""t-x-d-d"" how=""h-e"" time=""{timeStart}"" start=""{timeStart}"" stale=""{stale}"">
  <point lat=""0"" lon=""0"" hae=""0"" ce=""9999999"" le=""9999999"" />
  <detail />
</event>";
        }
    }
}
