using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Mavlink.V2.Common;
using Asv.IO;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    /// <summary>
    /// MAVLink agent that connects directly to SITL via UDP
    /// </summary>
    public class MavlinkAgent : IDisposable
    {
        private readonly int _vehicleId;
        private readonly int _systemId;
        private readonly string _connectionString;
        private readonly string _vehicleType;

        private IMavlinkV2Connection _connection;
        private CancellationTokenSource _cts;
        private Task _telemetryTask;

        // Telemetry state
        public PositionData Position { get; private set; }
        public BatteryData Battery { get; private set; }
        public string FlightMode { get; private set; }
        public bool Armed { get; private set; }
        public List<MissionWaypoint> Waypoints { get; private set; }
        public int CurrentWaypoint { get; private set; }
        public string Status { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public MavlinkAgent(int vehicleId, int systemId, string host, int port, string vehicleType)
        {
            _vehicleId = vehicleId;
            _systemId = systemId;
            _connectionString = $"udp://{host}:{port}";
            _vehicleType = vehicleType;

            Waypoints = new List<MissionWaypoint>();
            Status = "Connecting";
            CreatedAt = DateTime.UtcNow;
        }

        public async Task ConnectAsync()
        {
            try
            {
                // Create UDP connection
                var port = new PortFactory().CreatePort(_connectionString);
                _connection = new MavlinkV2Connection(port);

                _cts = new CancellationTokenSource();

                // Start telemetry loop
                _telemetryTask = Task.Run(() => TelemetryLoopAsync(_cts.Token));

                // Wait a moment for connection
                await Task.Delay(1000);

                Status = "Connected";
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                throw;
            }
        }

        private async Task TelemetryLoopAsync(CancellationToken token)
        {
            try
            {
                // Subscribe to MAVLink packets
                _connection.Where(x => x.SystemId == _systemId).Subscribe(OnPacketReceived);

                // Keep task alive
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(100, token);
                }
            }
            catch (Exception ex)
            {
                Status = $"Telemetry Error: {ex.Message}";
            }
        }

        private void OnPacketReceived(IPacketV2<IPayload> packet)
        {
            try
            {
                switch (packet.Payload)
                {
                    case GlobalPositionIntPayload gps:
                        Position = new PositionData
                        {
                            Latitude = gps.Lat / 1e7,
                            Longitude = gps.Lon / 1e7,
                            Altitude = gps.Alt / 1000.0,
                            Timestamp = DateTime.UtcNow
                        };
                        break;

                    case SysStatusPayload sys:
                        Battery = new BatteryData
                        {
                            Percentage = sys.BatteryRemaining,
                            Voltage = sys.VoltageBattery / 1000.0,
                            Timestamp = DateTime.UtcNow
                        };
                        break;

                    case HeartbeatPayload hb:
                        Armed = (hb.BaseMode & (byte)MavModeFlag.MavModeFlagSafetyArmed) != 0;
                        FlightMode = ParseFlightMode(hb.CustomMode);
                        break;

                    case MissionCurrentPayload mc:
                        CurrentWaypoint = mc.Seq;
                        UpdateWaypointStatus();
                        break;

                    case MissionItemIntPayload mi:
                        // Mission items received during download
                        AddOrUpdateWaypoint(mi);
                        break;

                    case MissionCountPayload count:
                        // Start of mission download
                        Waypoints.Clear();
                        break;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash telemetry loop
                System.Diagnostics.Debug.WriteLine($"Packet processing error: {ex.Message}");
            }
        }

        private string ParseFlightMode(uint customMode)
        {
            // ArduCopter modes
            var modes = new Dictionary<uint, string>
            {
                {0, "STABILIZE"}, {1, "ACRO"}, {2, "ALT_HOLD"},
                {3, "AUTO"}, {4, "GUIDED"}, {5, "LOITER"},
                {6, "RTL"}, {7, "CIRCLE"}, {9, "LAND"},
                {16, "POSHOLD"}, {17, "BRAKE"}
            };

            return modes.ContainsKey(customMode) ? modes[customMode] : $"UNKNOWN_{customMode}";
        }

        private void AddOrUpdateWaypoint(MissionItemIntPayload mi)
        {
            var waypoint = new MissionWaypoint
            {
                Sequence = mi.Seq,
                Command = ParseCommand(mi.Command),
                Latitude = mi.X / 1e7,
                Longitude = mi.Y / 1e7,
                Altitude = mi.Z,
                Param1 = mi.Param1,
                Param2 = mi.Param2,
                Param3 = mi.Param3,
                Param4 = mi.Param4,
                IsCurrent = mi.Seq == CurrentWaypoint,
                IsReached = mi.Seq < CurrentWaypoint
            };

            // Replace or add
            var existing = Waypoints.FirstOrDefault(w => w.Sequence == mi.Seq);
            if (existing != null)
            {
                Waypoints.Remove(existing);
            }
            Waypoints.Add(waypoint);
            Waypoints = Waypoints.OrderBy(w => w.Sequence).ToList();
        }

        private string ParseCommand(MavCmd cmd)
        {
            var commands = new Dictionary<MavCmd, string>
            {
                {MavCmd.MavCmdNavWaypoint, "WAYPOINT"},
                {MavCmd.MavCmdNavTakeoff, "TAKEOFF"},
                {MavCmd.MavCmdNavLand, "LAND"},
                {MavCmd.MavCmdNavReturnToLaunch, "RTL"},
                {MavCmd.MavCmdNavLoiterUnlim, "LOITER_UNLIM"},
                {MavCmd.MavCmdNavLoiterTurns, "LOITER_TURNS"},
                {MavCmd.MavCmdNavLoiterTime, "LOITER_TIME"}
            };

            return commands.ContainsKey(cmd) ? commands[cmd] : cmd.ToString();
        }

        private void UpdateWaypointStatus()
        {
            foreach (var wp in Waypoints)
            {
                wp.IsCurrent = wp.Sequence == CurrentWaypoint;
                wp.IsReached = wp.Sequence < CurrentWaypoint;
            }
        }

        public async Task RequestMissionAsync()
        {
            try
            {
                // Send mission request list
                var packet = new MissionRequestListPacket
                {
                    SystemId = 255,
                    ComponentId = 190,
                    Payload =
                    {
                        TargetSystem = (byte)_systemId,
                        TargetComponent = 1,
                        MissionType = MavMissionType.MavMissionTypeMission
                    }
                };

                await _connection.Send(packet, CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mission request error: {ex.Message}");
            }
        }

        public string GetMissionStatus()
        {
            if (Waypoints.Count == 0) return "not_started";
            if (CurrentWaypoint == 0) return "not_started";
            if (CurrentWaypoint >= Waypoints.Count) return "completed";
            return "in_progress";
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _telemetryTask?.Wait(TimeSpan.FromSeconds(2));
            _connection?.Dispose();
            _cts?.Dispose();
        }
    }
}
