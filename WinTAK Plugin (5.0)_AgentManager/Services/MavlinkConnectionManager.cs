using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    /// <summary>
    /// Single MAVLink connection that tracks all drones by System ID
    /// </summary>
    public class MavlinkConnectionManager : IDisposable
    {
        private UdpClient _udpClient;
        private UdpClient _commandClient; // Separate client for sending commands
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _commandEndPoint; // Where to send commands (MAVProxy udpin port)
        private IPEndPoint _lastReceivedFrom; // Track where packets come from
        private MAVLink.MavlinkParse _mavlink;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        // Track all drones by System ID
        private Dictionary<byte, DroneState> _drones;
        private object _dronesLock = new object();
        private int _packetsReceived = 0;
        private int _messagesProcessed = 0;

        public string Status { get; private set; }
        public int Port { get; private set; }
        public int PacketsReceived => _packetsReceived;
        public int MessagesProcessed => _messagesProcessed;

        public MavlinkConnectionManager()
        {
            _drones = new Dictionary<byte, DroneState>();
            _mavlink = new MAVLink.MavlinkParse();
            Status = "Disconnected";
        }

        public async Task ConnectAsync(string host = "127.0.0.1", int port = 14550)
        {
            try
            {
                Port = port;
                Status = "Connecting...";

                // Create UDP client that CONNECTS to MAVProxy's udpin server (bidirectional)
                // MAVProxy must be run with: --out=udpin:0.0.0.0:14551
                _udpClient = new UdpClient();
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
                _udpClient.Connect(_remoteEndPoint); // Connect establishes the remote endpoint

                System.Diagnostics.Debug.WriteLine($"Connected UDP client to MAVProxy at {_remoteEndPoint}");
                System.Diagnostics.Debug.WriteLine($"Make sure MAVProxy is running with: --out=udpin:0.0.0.0:{port}");

                // No separate command client needed - same socket for tx/rx
                _commandClient = _udpClient;
                _commandEndPoint = _remoteEndPoint;

                _cts = new CancellationTokenSource();

                // Start receive loop
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                // Request data streams from all systems (broadcast)
                await Task.Delay(500);
                RequestAllDataStreams();

                await Task.Delay(1000);
                Status = "Connected";
            }
            catch (SocketException sockEx)
            {
                Status = $"Port Error: {sockEx.Message}";
                throw new Exception($"Cannot bind to port {port}. Check if another program (QGC, Mission Planner) is using it. Error: {sockEx.Message}");
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                throw;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        var packet = result.Buffer;

                        // Track where packets come from so we can reply
                        _lastReceivedFrom = result.RemoteEndPoint;

                        _packetsReceived++;
                        System.Diagnostics.Debug.WriteLine($"[PKT {_packetsReceived}] Received UDP packet: {packet.Length} bytes from {result.RemoteEndPoint}");

                        // Parse MAVLink messages using MemoryStream
                        using (var stream = new MemoryStream(packet))
                        {
                            while (stream.Position < stream.Length)
                            {
                                try
                                {
                                    var msg = _mavlink.ReadPacket(stream);
                                    if (msg != null)
                                    {
                                        _messagesProcessed++;
                                        System.Diagnostics.Debug.WriteLine($"[MSG {_messagesProcessed}] Parsed MAVLink: SysID={msg.sysid}, MsgID={msg.msgid}, Drones={_drones.Count}");
                                        ProcessMessage(msg);
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Parse error: {parseEx.Message}");
                                    // Skip invalid bytes and continue
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Receive error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Status = $"Receive Loop Error: {ex.Message}";
            }
        }

        private void ProcessMessage(MAVLink.MAVLinkMessage msg)
        {
            try
            {
                var sysId = msg.sysid;

                // Ensure drone exists in tracking
                lock (_dronesLock)
                {
                    if (!_drones.ContainsKey(sysId))
                    {
                        System.Diagnostics.Debug.WriteLine($"NEW DRONE DETECTED: System ID {sysId}");
                        _drones[sysId] = new DroneState
                        {
                            SystemId = sysId,
                            LastSeen = DateTime.UtcNow
                        };
                        System.Diagnostics.Debug.WriteLine($"Total drones tracked: {_drones.Count}");
                    }

                    var drone = _drones[sysId];
                    drone.LastSeen = DateTime.UtcNow;

                    // Process message types
                    switch (msg.msgid)
                    {
                        case (uint)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT:
                            var gps = (MAVLink.mavlink_global_position_int_t)msg.data;
                            drone.Position = new PositionData
                            {
                                Latitude = gps.lat / 1e7,
                                Longitude = gps.lon / 1e7,
                                Altitude = gps.relative_alt / 1000.0, // Use relative altitude (AGL) instead of absolute (MSL)
                                Heading = gps.hdg / 100.0, // Heading in degrees (0-360), from centidegrees
                                GroundSpeed = Math.Sqrt(gps.vx * gps.vx + gps.vy * gps.vy) / 100.0, // Speed in m/s from cm/s
                                Timestamp = DateTime.UtcNow
                            };
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.SYS_STATUS:
                            var sys = (MAVLink.mavlink_sys_status_t)msg.data;
                            drone.Battery = new BatteryData
                            {
                                Percentage = sys.battery_remaining,
                                Voltage = sys.voltage_battery / 1000.0,
                                Timestamp = DateTime.UtcNow
                            };
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.HEARTBEAT:
                            var hb = (MAVLink.mavlink_heartbeat_t)msg.data;
                            drone.Armed = (hb.base_mode & (byte)MAVLink.MAV_MODE_FLAG.SAFETY_ARMED) != 0;
                            drone.VehicleType = (MAVLink.MAV_TYPE)hb.type; // Store vehicle type
                            drone.FlightMode = ParseFlightMode(hb.custom_mode, drone.VehicleType);

                            // Only request mission ONCE on first heartbeat (not periodically)
                            // User can click "Refresh All Missions" button to re-request
                            if (!drone.MissionRequested)
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto-requesting mission for Drone {sysId} (type: {drone.VehicleType}) on first heartbeat");
                                drone.MissionRequested = true;
                                drone.LastMissionRequest = DateTime.UtcNow;
                                Task.Run(() => RequestMissionForDrone(sysId));
                            }
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_CURRENT:
                            var mc = (MAVLink.mavlink_mission_current_t)msg.data;
                            drone.CurrentWaypoint = mc.seq;
                            drone.UpdateWaypointStatus();
                            System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Current waypoint = {mc.seq}");
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_COUNT:
                            var mcount = (MAVLink.mavlink_mission_count_t)msg.data;
                            drone.ExpectedWaypointCount = mcount.count;
                            System.Diagnostics.Debug.WriteLine($"Drone {sysId}: MISSION_COUNT received - expecting {mcount.count} waypoints");
                            // Clear existing waypoints, prepare for new mission
                            drone.Waypoints.Clear();
                            drone.MissionRetryCount = 0; // Reset retry count since we got a response

                            // Request each waypoint individually
                            for (ushort i = 0; i < mcount.count; i++)
                            {
                                var mreq = new MAVLink.mavlink_mission_request_int_t
                                {
                                    target_system = sysId,
                                    target_component = 1,
                                    seq = i,
                                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION
                                };
                                var packet = _mavlink.GenerateMAVLinkPacket10(
                                    MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_INT,
                                    mreq);
                                _udpClient.Send(packet, packet.Length);
                                System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Sent MISSION_REQUEST_INT for waypoint {i}");
                            }
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT:
                            var mi = (MAVLink.mavlink_mission_item_int_t)msg.data;
                            var cmdName = GetCommandName(mi.command);
                            System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Received MISSION_ITEM_INT {mi.seq} - {cmdName} at ({mi.x/1e7:F6}, {mi.y/1e7:F6})");
                            // Skip waypoint 0 (home position) and invalid (0,0) coordinates
                            if (mi.seq > 0 && !(mi.x == 0 && mi.y == 0))
                            {
                                drone.AddOrUpdateWaypoint(mi);
                            }
                            else if (mi.x == 0 && mi.y == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Skipping invalid waypoint at (0,0)");
                            }
                            break;

                        case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_ITEM:
                            var mi_legacy = (MAVLink.mavlink_mission_item_t)msg.data;
                            System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Received MISSION_ITEM (legacy) {mi_legacy.seq} at ({mi_legacy.x:F6}, {mi_legacy.y:F6})");
                            // Skip waypoint 0 (home position) and invalid (0,0) coordinates
                            if (mi_legacy.seq > 0 && !(mi_legacy.x == 0.0 && mi_legacy.y == 0.0))
                            {
                                // Convert to MISSION_ITEM_INT format
                                var mi_int = new MAVLink.mavlink_mission_item_int_t
                                {
                                    seq = mi_legacy.seq,
                                    command = mi_legacy.command,
                                    x = (int)(mi_legacy.x * 1e7),
                                    y = (int)(mi_legacy.y * 1e7),
                                    z = mi_legacy.z,
                                    param1 = mi_legacy.param1,
                                    param2 = mi_legacy.param2,
                                    param3 = mi_legacy.param3,
                                    param4 = mi_legacy.param4
                                };
                                drone.AddOrUpdateWaypoint(mi_int);
                            }
                            else if (mi_legacy.x == 0.0 && mi_legacy.y == 0.0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Drone {sysId}: Skipping invalid waypoint at (0,0)");
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Message processing error: {ex.Message}");
            }
        }

        private string ParseFlightMode(uint customMode, MAVLink.MAV_TYPE vehicleType)
        {
            // COPTER mode map (for MAV_TYPE.QUADROTOR, HEXAROTOR, OCTOROTOR, etc.)
            var copterModes = new Dictionary<uint, string>
            {
                {0, "STABILIZE"}, {1, "ACRO"}, {2, "ALT_HOLD"},
                {3, "AUTO"}, {4, "GUIDED"}, {5, "LOITER"},
                {6, "RTL"}, {7, "CIRCLE"}, {8, "POSITION"},
                {9, "LAND"}, {10, "OF_LOITER"}, {11, "DRIFT"},
                {13, "SPORT"}, {14, "FLIP"}, {15, "AUTOTUNE"},
                {16, "POSHOLD"}, {17, "BRAKE"}, {18, "THROW"},
                {19, "AVOID_ADSB"}, {20, "GUIDED_NOGPS"}, {21, "SMART_RTL"},
                {22, "FLOWHOLD"}, {23, "FOLLOW"}, {24, "ZIGZAG"},
                {25, "SYSTEMID"}, {26, "AUTOROTATE"}, {27, "AUTO_RTL"}
            };

            // VTOL mode map (for MAV_TYPE.FIXED_WING, VTOL_DUOROTOR, VTOL_QUADROTOR, VTOL_TILTROTOR, etc.)
            var vtolModes = new Dictionary<uint, string>
            {
                {0, "MANUAL"}, {1, "CIRCLE"}, {2, "STABILIZE"},
                {3, "TRAINING"}, {4, "ACRO"}, {5, "FBWA"},
                {6, "FBWB"}, {7, "CRUISE"}, {8, "AUTOTUNE"},
                {10, "AUTO"}, {11, "RTL"}, {12, "LOITER"},
                {13, "TAKEOFF"}, {14, "AVOID_ADSB"}, {15, "GUIDED"},
                // Q-modes (QuadPlane VTOL modes)
                {17, "QSTABILIZE"}, {18, "QHOVER"}, {19, "QLOITER"},
                {20, "QLAND"}, {21, "QRTL"}, {23, "QACRO"},
                {25, "QLAND"}
            };

            // Determine which mode map to use based on vehicle type
            bool isVTOL = vehicleType == MAVLink.MAV_TYPE.FIXED_WING ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_DUOROTOR ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_QUADROTOR ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_TILTROTOR ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_RESERVED2 ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_RESERVED3 ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_RESERVED4 ||
                          vehicleType == MAVLink.MAV_TYPE.VTOL_RESERVED5;

            var modeMap = isVTOL ? vtolModes : copterModes;

            return modeMap.ContainsKey(customMode) ? modeMap[customMode] : $"UNKNOWN_{customMode}";
        }

        private string GetCommandName(ushort cmd)
        {
            var commands = new Dictionary<ushort, string>
            {
                {16, "WAYPOINT"},
                {22, "TAKEOFF"},
                {21, "LAND"},
                {20, "RTL"},
                {17, "LOITER_UNLIM"},
                {18, "LOITER_TURNS"},
                {19, "LOITER_TIME"}
            };

            return commands.ContainsKey(cmd) ? commands[cmd] : $"CMD_{cmd}";
        }

        private void RequestAllDataStreams()
        {
            try
            {
                // Request data streams from system 0 (broadcast) and specific systems 1-10
                for (byte sysId = 0; sysId <= 10; sysId++)
                {
                    var msg = new MAVLink.mavlink_request_data_stream_t
                    {
                        target_system = sysId,
                        target_component = 1,
                        req_stream_id = (byte)MAVLink.MAV_DATA_STREAM.ALL,
                        req_message_rate = 2,
                        start_stop = 1
                    };

                    var packet = _mavlink.GenerateMAVLinkPacket10(
                        MAVLink.MAVLINK_MSG_ID.REQUEST_DATA_STREAM,
                        msg);

                    // Send using connected UDP client (no need to specify endpoint)
                    _udpClient.Send(packet, packet.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Request data streams error: {ex.Message}");
            }
        }

        public async Task RequestMissionForDrone(byte systemId)
        {
            const int MaxRetries = 3;
            const int TimeoutMs = 3000;

            try
            {
                DroneState drone;
                lock (_dronesLock)
                {
                    if (!_drones.ContainsKey(systemId))
                    {
                        System.Diagnostics.Debug.WriteLine($"Drone {systemId} not found, cannot request mission");
                        return;
                    }
                    drone = _drones[systemId];
                }

                // Retry loop
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    System.Diagnostics.Debug.WriteLine($"*** REQUESTING MISSION LIST for Drone {systemId} (Attempt {attempt}/{MaxRetries}) ***");

                    // Reset expected count before sending request
                    lock (_dronesLock)
                    {
                        drone.ExpectedWaypointCount = 0;
                    }

                    var msg = new MAVLink.mavlink_mission_request_list_t
                    {
                        target_system = systemId,
                        target_component = 1,
                        mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION
                    };

                    var packet = _mavlink.GenerateMAVLinkPacket10(
                        MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_LIST,
                        msg);

                    // Send using connected UDP client
                    System.Diagnostics.Debug.WriteLine($"Sending MISSION_REQUEST_LIST packet ({packet.Length} bytes) to {_remoteEndPoint}");
                    _udpClient.Send(packet, packet.Length);

                    // Wait for MISSION_COUNT response
                    await Task.Delay(TimeoutMs);

                    // Check if we received MISSION_COUNT
                    int expectedCount;
                    lock (_dronesLock)
                    {
                        expectedCount = drone.ExpectedWaypointCount;
                    }

                    if (expectedCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Drone {systemId}: Mission request successful - expecting {expectedCount} waypoints");
                        return; // Success!
                    }

                    System.Diagnostics.Debug.WriteLine($"Drone {systemId}: No MISSION_COUNT response, retrying...");
                    lock (_dronesLock)
                    {
                        drone.MissionRetryCount = attempt;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"*** Drone {systemId}: Mission request FAILED after {MaxRetries} attempts ***");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"*** MISSION REQUEST ERROR: {ex.Message} ***");
            }
        }

        public void ClearMissionState(byte systemId)
        {
            lock (_dronesLock)
            {
                if (_drones.ContainsKey(systemId))
                {
                    var drone = _drones[systemId];
                    drone.MissionRequested = false;
                    drone.Waypoints.Clear();
                    System.Diagnostics.Debug.WriteLine($"Cleared mission state for Drone {systemId}");
                }
            }
        }

        public List<DroneState> GetAllDrones()
        {
            lock (_dronesLock)
            {
                return _drones.Values.ToList();
            }
        }

        public DroneState GetDrone(byte systemId)
        {
            lock (_dronesLock)
            {
                return _drones.ContainsKey(systemId) ? _drones[systemId] : null;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
            _udpClient?.Close();
            _udpClient?.Dispose();
            // _commandClient is same reference as _udpClient, don't dispose twice
            _cts?.Dispose();
        }
    }

    /// <summary>
    /// State for a single drone
    /// </summary>
    public class DroneState
    {
        public byte SystemId { get; set; }
        public DateTime LastSeen { get; set; }
        public PositionData Position { get; set; }
        public BatteryData Battery { get; set; }
        public string FlightMode { get; set; }
        public bool Armed { get; set; }
        public MAVLink.MAV_TYPE VehicleType { get; set; } // Vehicle type from HEARTBEAT
        public List<MissionWaypoint> Waypoints { get; set; }
        public int CurrentWaypoint { get; set; }
        public bool MissionRequested { get; set; }
        public DateTime LastMissionRequest { get; set; }
        public int MissionRetryCount { get; set; }
        public int ExpectedWaypointCount { get; set; } // From MISSION_COUNT

        public DroneState()
        {
            Waypoints = new List<MissionWaypoint>();
            MissionRequested = false;
            LastMissionRequest = DateTime.MinValue;
            MissionRetryCount = 0;
            ExpectedWaypointCount = 0;
            VehicleType = MAVLink.MAV_TYPE.GENERIC; // Default until we receive HEARTBEAT
        }

        public void AddOrUpdateWaypoint(MAVLink.mavlink_mission_item_int_t mi)
        {
            var waypoint = new MissionWaypoint
            {
                Sequence = mi.seq,
                Command = ParseCommand(mi.command),
                Latitude = mi.x / 1e7,
                Longitude = mi.y / 1e7,
                Altitude = mi.z,
                Param1 = mi.param1,
                Param2 = mi.param2,
                Param3 = mi.param3,
                Param4 = mi.param4,
                IsCurrent = mi.seq == CurrentWaypoint,
                IsReached = mi.seq < CurrentWaypoint
            };

            var existing = Waypoints.FirstOrDefault(w => w.Sequence == mi.seq);
            if (existing != null)
            {
                Waypoints.Remove(existing);
            }
            Waypoints.Add(waypoint);
            Waypoints = Waypoints.OrderBy(w => w.Sequence).ToList();
        }

        private string ParseCommand(ushort cmd)
        {
            var commands = new Dictionary<ushort, string>
            {
                {16, "WAYPOINT"},
                {22, "TAKEOFF"},
                {21, "LAND"},
                {20, "RTL"},
                {17, "LOITER_UNLIM"},
                {18, "LOITER_TURNS"},
                {19, "LOITER_TIME"}
            };

            return commands.ContainsKey(cmd) ? commands[cmd] : $"CMD_{cmd}";
        }

        public void UpdateWaypointStatus()
        {
            foreach (var wp in Waypoints)
            {
                wp.IsCurrent = wp.Sequence == CurrentWaypoint;
                wp.IsReached = wp.Sequence < CurrentWaypoint;
            }
        }

        public string GetMissionStatus()
        {
            if (Waypoints.Count == 0) return "no_mission";
            if (CurrentWaypoint == 0) return "not_started";
            if (CurrentWaypoint >= Waypoints.Count) return "completed";
            return "in_progress";
        }

        public bool IsAlive()
        {
            return (DateTime.UtcNow - LastSeen).TotalSeconds < 5;
        }
    }
}
