using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MAVLink;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    /// <summary>
    /// MAVLink agent that connects directly to SITL via UDP
    /// Uses the official MAVLink 1.0.8 NuGet package
    /// </summary>
    public class MavlinkAgent : IDisposable
    {
        private readonly int _vehicleId;
        private readonly int _systemId;
        private readonly string _host;
        private readonly int _port;
        private readonly string _vehicleType;

        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private MavlinkParse _mavlink;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

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
            _host = host;
            _port = port;
            _vehicleType = vehicleType;

            Waypoints = new List<MissionWaypoint>();
            Status = "Connecting";
            CreatedAt = DateTime.UtcNow;

            _mavlink = new MavlinkParse();
        }

        public async Task ConnectAsync()
        {
            try
            {
                // Create UDP client listening on any port
                _udpClient = new UdpClient(0);
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _cts = new CancellationTokenSource();

                // Start receive loop
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                // Request data streams
                await Task.Delay(500); // Wait for connection establishment
                RequestDataStreams();

                // Wait for heartbeat
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500);
                    if (!string.IsNullOrEmpty(FlightMode))
                    {
                        Status = "Connected";
                        return;
                    }
                }

                throw new Exception("No heartbeat received from vehicle");
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

                        // Parse MAVLink messages
                        foreach (var b in packet)
                        {
                            var msg = _mavlink.ReadPacket(b);
                            if (msg != null)
                            {
                                ProcessMessage(msg);
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

        private void ProcessMessage(MAVLinkMessage msg)
        {
            try
            {
                // Filter by system ID
                if (msg.sysid != _systemId)
                    return;

                switch (msg.msgid)
                {
                    case (uint)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT:
                        var gps = (MAVLink.mavlink_global_position_int_t)msg.data;
                        Position = new PositionData
                        {
                            Latitude = gps.lat / 1e7,
                            Longitude = gps.lon / 1e7,
                            Altitude = gps.alt / 1000.0,
                            Timestamp = DateTime.UtcNow
                        };
                        break;

                    case (uint)MAVLink.MAVLINK_MSG_ID.SYS_STATUS:
                        var sys = (MAVLink.mavlink_sys_status_t)msg.data;
                        Battery = new BatteryData
                        {
                            Percentage = sys.battery_remaining,
                            Voltage = sys.voltage_battery / 1000.0,
                            Timestamp = DateTime.UtcNow
                        };
                        break;

                    case (uint)MAVLink.MAVLINK_MSG_ID.HEARTBEAT:
                        var hb = (MAVLink.mavlink_heartbeat_t)msg.data;
                        Armed = (hb.base_mode & (byte)MAVLink.MAV_MODE_FLAG.SAFETY_ARMED) != 0;
                        FlightMode = ParseFlightMode(hb.custom_mode);
                        break;

                    case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_CURRENT:
                        var mc = (MAVLink.mavlink_mission_current_t)msg.data;
                        CurrentWaypoint = mc.seq;
                        UpdateWaypointStatus();
                        break;

                    case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_COUNT:
                        var count = (MAVLink.mavlink_mission_count_t)msg.data;
                        // Clear existing waypoints, prepare for new mission
                        Waypoints.Clear();
                        break;

                    case (uint)MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT:
                        var mi = (MAVLink.mavlink_mission_item_int_t)msg.data;
                        AddOrUpdateWaypoint(mi);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Message processing error: {ex.Message}");
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

        private void AddOrUpdateWaypoint(MAVLink.mavlink_mission_item_int_t mi)
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

            // Replace or add
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

        private void UpdateWaypointStatus()
        {
            foreach (var wp in Waypoints)
            {
                wp.IsCurrent = wp.Sequence == CurrentWaypoint;
                wp.IsReached = wp.Sequence < CurrentWaypoint;
            }
        }

        private void RequestDataStreams()
        {
            try
            {
                // Request position, status, and other telemetry at 2 Hz
                var msg = new MAVLink.mavlink_request_data_stream_t
                {
                    target_system = (byte)_systemId,
                    target_component = 1,
                    req_stream_id = (byte)MAVLink.MAV_DATA_STREAM.ALL,
                    req_message_rate = 2,
                    start_stop = 1
                };

                var packet = _mavlink.GenerateMAVLinkPacket10(
                    MAVLink.MAVLINK_MSG_ID.REQUEST_DATA_STREAM,
                    msg);

                _udpClient.Send(packet, packet.Length, _remoteEndPoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Request data streams error: {ex.Message}");
            }
        }

        public async Task RequestMissionAsync()
        {
            try
            {
                // Send mission request list
                var msg = new MAVLink.mavlink_mission_request_list_t
                {
                    target_system = (byte)_systemId,
                    target_component = 1,
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION
                };

                var packet = _mavlink.GenerateMAVLinkPacket10(
                    MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_LIST,
                    msg);

                _udpClient.Send(packet, packet.Length, _remoteEndPoint);

                await Task.Delay(100); // Give time for response
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Request mission error: {ex.Message}");
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
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
            _udpClient?.Close();
            _udpClient?.Dispose();
            _cts?.Dispose();
        }
    }
}
