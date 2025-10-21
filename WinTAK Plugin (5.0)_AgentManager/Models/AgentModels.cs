using System;
using System.Collections.Generic;

namespace AgentManagerPlugin.Models
{
    /// <summary>
    /// Configuration for adding a new pilot agent
    /// </summary>
    public class PilotConfig
    {
        public int VehicleId { get; set; }
        public string CertName { get; set; }
        public int ConnectionPort { get; set; }
        public string VehicleType { get; set; } // "quad" or "vtol"
        public int Altitude { get; set; }
    }

    /// <summary>
    /// Configuration for starting mission control agent
    /// </summary>
    public class MissionControlConfig
    {
        public string CertName { get; set; }
    }

    /// <summary>
    /// Information about an active agent
    /// </summary>
    public class AgentInfo
    {
        public string AgentId { get; set; }
        public string Type { get; set; } // "pilot" or "mission_control"
        public string Status { get; set; }
        public Dictionary<string, object> Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Detailed status of an agent
    /// </summary>
    public class AgentStatus
    {
        public string AgentId { get; set; }
        public string Status { get; set; } // "active", "stopped", "error"
        public PositionData Position { get; set; }
        public BatteryData Battery { get; set; }
        public WaypointData Waypoints { get; set; }
        public string FlightMode { get; set; }
    }

    /// <summary>
    /// Position information
    /// </summary>
    public class PositionData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Heading { get; set; } // Direction drone is pointing (degrees, 0-360)
        public double GroundSpeed { get; set; } // Speed in m/s
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Battery status information
    /// </summary>
    public class BatteryData
    {
        public int Percentage { get; set; }
        public double Voltage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Waypoint/mission information
    /// </summary>
    public class WaypointData
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string MissionStatus { get; set; }
        public List<MissionWaypoint> Waypoints { get; set; }
    }

    /// <summary>
    /// Individual mission waypoint (qGroundControl style)
    /// </summary>
    public class MissionWaypoint
    {
        public int Sequence { get; set; }
        public string Command { get; set; } // WAYPOINT, TAKEOFF, LAND, RTL, etc.
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Param1 { get; set; } // Hold time / Pitch / etc.
        public double Param2 { get; set; } // Accept radius / Empty / etc.
        public double Param3 { get; set; } // Pass radius / Empty / etc.
        public double Param4 { get; set; } // Yaw / Empty / etc.
        public bool IsCurrent { get; set; }
        public bool IsReached { get; set; }
    }

    /// <summary>
    /// Certificate information
    /// </summary>
    public class CertificateInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Server configuration (read-only from backend)
    /// </summary>
    public class ServerConfig
    {
        public string ServerUrl { get; set; }
        public int SslPort { get; set; }
        public int TcpPort { get; set; }
        // Password excluded for security
    }

    /// <summary>
    /// API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
    }
}
