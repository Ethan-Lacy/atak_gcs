# Agent Manager Backend Requirements

This document specifies the exact API contract required by the WinTAK Agent Manager plugin frontend.

## Base URL

```
http://localhost:8000/api/v1
```

## Authentication

Currently **no authentication** is required. Future versions may add API keys.

---

## API Endpoints

### 1. Health Check / Server Config

#### `GET /config/server`

**Purpose**: Get read-only server configuration and verify API availability.

**Response** (200 OK):
```json
{
  "server_url": "takserver.example.com",
  "ssl_port": 8089,
  "tcp_port": 8087
}
```

**Note**: Password is excluded for security. This endpoint is also used to check if the API is reachable.

---

### 2. List Certificates

#### `GET /certificates`

**Purpose**: Get list of available certificates for pilot/mission control agents.

**Response** (200 OK):
```json
[
  {
    "name": "pilot1.p12",
    "path": "/path/to/atak/certificates/pilot1.p12",
    "is_valid": true
  },
  {
    "name": "pilot2.p12",
    "path": "/path/to/atak/certificates/pilot2.p12",
    "is_valid": true
  },
  {
    "name": "mission_control.p12",
    "path": "/path/to/atak/certificates/mission_control.p12",
    "is_valid": true
  }
]
```

**Fields**:
- `name` (string): Display name for dropdown
- `path` (string): Full path to certificate file
- `is_valid` (bool): Whether certificate is valid/usable

---

### 3. Add Pilot Agent

#### `POST /pilots`

**Purpose**: Start a new pilot agent thread.

**Request Body**:
```json
{
  "vehicle_id": 1,
  "cert_name": "pilot1.p12",
  "connection_port": 14591,
  "vehicle_type": "quad",
  "altitude": 50
}
```

**Field Details**:
- `vehicle_id` (int): Unique ID for this vehicle (e.g., 1, 2, 3)
- `cert_name` (string): Certificate filename from `/certificates` endpoint
- `connection_port` (int): MAVLink connection port (typically 14590 + vehicle_id)
- `vehicle_type` (string): Either `"quad"` or `"vtol"`
- `altitude` (int): Default/target altitude in meters

**Response** (200 OK):
```json
{
  "agent_id": "pilot_1",
  "type": "pilot",
  "status": "started",
  "details": {
    "vehicle_id": 1,
    "vehicle_type": "quad",
    "port": 14591
  },
  "created_at": "2025-01-15T10:30:00Z"
}
```

**Error Responses**:
- `400 Bad Request`: Invalid input (missing fields, invalid vehicle_type)
- `409 Conflict`: Agent with this vehicle_id already exists
- `500 Internal Server Error`: Failed to start agent thread

---

### 4. Remove Pilot Agent

#### `DELETE /pilots/{pilot_id}`

**Purpose**: Stop and remove a pilot agent.

**Path Parameters**:
- `pilot_id` (string): Agent ID returned from POST /pilots

**Response** (200 OK):
```json
{
  "success": true
}
```

**Error Responses**:
- `404 Not Found`: Agent with this ID does not exist
- `500 Internal Server Error`: Failed to stop agent thread

---

### 5. List Active Pilots

#### `GET /pilots`

**Purpose**: Get list of all currently active pilot agents.

**Response** (200 OK):
```json
[
  {
    "agent_id": "pilot_1",
    "type": "pilot",
    "status": "active",
    "details": {
      "vehicle_id": 1,
      "vehicle_type": "quad",
      "port": 14591
    },
    "created_at": "2025-01-15T10:30:00Z"
  },
  {
    "agent_id": "pilot_2",
    "type": "pilot",
    "status": "active",
    "details": {
      "vehicle_id": 2,
      "vehicle_type": "vtol",
      "port": 14592
    },
    "created_at": "2025-01-15T10:35:00Z"
  }
]
```

**Status Values**:
- `"active"`: Agent is running normally
- `"error"`: Agent encountered an error
- `"stopped"`: Agent has been stopped

---

### 6. Get Pilot Status (Detailed)

#### `GET /pilots/{pilot_id}/status`

**Purpose**: Get detailed status including position, battery, and **mission waypoints**.

**Response** (200 OK):
```json
{
  "agent_id": "pilot_1",
  "status": "active",
  "position": {
    "latitude": 38.870334,
    "longitude": -77.055977,
    "altitude": 50.5,
    "timestamp": "2025-01-15T10:45:00Z"
  },
  "battery": {
    "percentage": 78,
    "voltage": 22.4,
    "timestamp": "2025-01-15T10:45:00Z"
  },
  "flight_mode": "AUTO",
  "waypoints": {
    "current": 3,
    "total": 13,
    "mission_status": "in_progress",
    "waypoints": [
      {
        "sequence": 1,
        "command": "TAKEOFF",
        "latitude": 38.870334,
        "longitude": -77.055977,
        "altitude": 50.0,
        "param1": 15.0,
        "param2": 0.0,
        "param3": 0.0,
        "param4": 0.0,
        "is_current": false,
        "is_reached": true
      },
      {
        "sequence": 2,
        "command": "WAYPOINT",
        "latitude": 38.871000,
        "longitude": -77.056500,
        "altitude": 50.0,
        "param1": 0.0,
        "param2": 5.0,
        "param3": 0.0,
        "param4": 0.0,
        "is_current": false,
        "is_reached": true
      },
      {
        "sequence": 3,
        "command": "WAYPOINT",
        "latitude": 38.871500,
        "longitude": -77.057000,
        "altitude": 50.0,
        "param1": 0.0,
        "param2": 5.0,
        "param3": 0.0,
        "param4": 0.0,
        "is_current": true,
        "is_reached": false
      },
      {
        "sequence": 4,
        "command": "WAYPOINT",
        "latitude": 38.872000,
        "longitude": -77.057500,
        "altitude": 50.0,
        "param1": 0.0,
        "param2": 5.0,
        "param3": 0.0,
        "param4": 0.0,
        "is_current": false,
        "is_reached": false
      },
      {
        "sequence": 13,
        "command": "LAND",
        "latitude": 38.870334,
        "longitude": -77.055977,
        "altitude": 0.0,
        "param1": 0.0,
        "param2": 0.0,
        "param3": 0.0,
        "param4": 0.0,
        "is_current": false,
        "is_reached": false
      }
    ]
  }
}
```

**Waypoint Command Types** (MAVLink standard):
- `WAYPOINT`: Navigate to waypoint
- `TAKEOFF`: Takeoff command
- `LAND`: Land at location
- `LOITER_UNLIM`: Loiter indefinitely
- `LOITER_TURNS`: Loiter for N turns
- `LOITER_TIME`: Loiter for N seconds
- `RETURN_TO_LAUNCH` / `RTL`: Return to home
- `DO_JUMP`: Jump to another waypoint
- `DO_CHANGE_SPEED`: Change speed
- `DO_SET_HOME`: Set new home position

**Waypoint Parameters** (MAVLink standard):
- `param1`: Hold time (WAYPOINT), Min pitch (TAKEOFF), etc.
- `param2`: Accept radius (WAYPOINT), Empty (TAKEOFF), etc.
- `param3`: Pass radius, Yaw, etc.
- `param4`: Yaw angle, Empty, etc.

**Mission Status Values**:
- `"not_started"`: Mission loaded but not started
- `"in_progress"`: Mission actively executing
- `"completed"`: All waypoints reached
- `"paused"`: Mission paused
- `"aborted"`: Mission aborted/cancelled

---

### 7. Start Mission Control

#### `POST /mission-control`

**Purpose**: Start the mission control agent thread.

**Request Body**:
```json
{
  "cert_name": "mission_control.p12"
}
```

**Response** (200 OK):
```json
{
  "agent_id": "mission_control_1",
  "type": "mission_control",
  "status": "started",
  "details": {
    "active_pilots": 2,
    "tasks_assigned": 0
  },
  "created_at": "2025-01-15T10:50:00Z"
}
```

**Error Responses**:
- `409 Conflict`: Mission control already running
- `500 Internal Server Error`: Failed to start agent

---

### 8. Stop Mission Control

#### `DELETE /mission-control/{mc_id}`

**Purpose**: Stop the mission control agent.

**Path Parameters**:
- `mc_id` (string): Agent ID from POST /mission-control

**Response** (200 OK):
```json
{
  "success": true
}
```

---

## Data Models Summary

### AgentInfo
```typescript
{
  agent_id: string;          // Unique identifier
  type: "pilot" | "mission_control";
  status: "active" | "stopped" | "error";
  details: object;           // Type-specific details
  created_at: string;        // ISO 8601 timestamp
}
```

### PilotConfig (Request)
```typescript
{
  vehicle_id: number;        // 1, 2, 3, etc.
  cert_name: string;         // "pilot1.p12"
  connection_port: number;   // 14591, 14592, etc.
  vehicle_type: "quad" | "vtol";
  altitude: number;          // Meters
}
```

### MissionControlConfig (Request)
```typescript
{
  cert_name: string;         // "mission_control.p12"
}
```

### AgentStatus (Detailed)
```typescript
{
  agent_id: string;
  status: string;
  position?: {
    latitude: number;
    longitude: number;
    altitude: number;
    timestamp: string;
  };
  battery?: {
    percentage: number;
    voltage: number;
    timestamp: string;
  };
  flight_mode?: string;
  waypoints?: {
    current: number;
    total: number;
    mission_status: string;
    waypoints: MissionWaypoint[];
  };
}
```

### MissionWaypoint
```typescript
{
  sequence: number;          // Waypoint number (1-based)
  command: string;           // MAVLink command name
  latitude: number;          // Decimal degrees
  longitude: number;         // Decimal degrees
  altitude: number;          // Meters (AMSL or relative)
  param1: number;            // Command-specific parameter
  param2: number;            // Command-specific parameter
  param3: number;            // Command-specific parameter
  param4: number;            // Command-specific parameter
  is_current: boolean;       // True if this is the active waypoint
  is_reached: boolean;       // True if waypoint has been reached
}
```

---

## Configuration Notes

### Server Settings
The plugin expects these to be configured globally in the backend:
- `server_url`: TAK server hostname/IP
- `ssl_port`: TAK server SSL port (default 8089)
- `tcp_port`: TAK server TCP port (default 8087)
- `client_password`: Certificate password (NOT exposed via API)

These are read from backend config files, not passed per-agent.

### Certificate Management
- Certificates should be stored in `atak/certificates/` directory on the backend
- The `/certificates` endpoint scans this directory
- Only `.p12` files should be listed
- Backend validates certificate exists before starting agents

---

## Threading Model

The backend should:
1. **Maintain agent registry**: Track all active pilot and mission control threads
2. **Thread-safe operations**: Use locks for adding/removing agents
3. **Graceful shutdown**: Clean up threads on DELETE requests
4. **Database integration**: Query `DroneDatabase` for position, battery, waypoints
5. **Real-time updates**: Poll MAVLink data and update database continuously

---

## Error Handling

All error responses should follow this format:

```json
{
  "error": "Human-readable error message",
  "details": "Optional additional context"
}
```

HTTP Status Codes:
- `200 OK`: Success
- `400 Bad Request`: Invalid input
- `404 Not Found`: Resource not found
- `409 Conflict`: Resource already exists
- `500 Internal Server Error`: Server-side error

---

## Example Implementation Flow

### 1. Plugin Startup
```
GET /config/server          → Check API availability
GET /certificates           → Populate certificate dropdowns
GET /pilots                 → Load existing active pilots
```

### 2. Add Pilot
```
POST /pilots
{
  "vehicle_id": 1,
  "cert_name": "pilot1.p12",
  "connection_port": 14591,
  "vehicle_type": "quad",
  "altitude": 50
}
→ Backend starts new thread running pilot agent
→ Returns agent_id for future operations
```

### 3. Monitor Agents (Auto-refresh every 2 seconds)
```
GET /pilots                 → Update agent list UI
GET /pilots/{id}/status     → Get detailed status when viewing mission
```

### 4. View Mission
```
User clicks "📍 View Mission" button
→ GET /pilots/{id}/status
→ Parse waypoints array
→ Display in qGroundControl-style popup window
```

### 5. Remove Pilot
```
User clicks "Remove Pilot" button
→ DELETE /pilots/{id}
→ Backend stops thread gracefully
→ Frontend refreshes agent list
```

---

## Future Enhancements (Not Required Now)

- **WebSocket** (`/api/v1/ws/agents/{agent_id}`): Real-time agent updates
- **Authentication**: API key or OAuth tokens
- **Mission upload**: `POST /pilots/{id}/mission` to upload new waypoint plan
- **Mission control commands**: `POST /mission-control/commands` to assign tasks
- **Historical data**: `GET /pilots/{id}/history` for position/battery over time
- **Bulk operations**: `POST /pilots/batch` to add multiple pilots at once

---

## Testing the Backend

Use `curl` to test endpoints:

```bash
# Check API availability
curl http://localhost:8000/api/v1/config/server

# List certificates
curl http://localhost:8000/api/v1/certificates

# Add pilot
curl -X POST http://localhost:8000/api/v1/pilots \
  -H "Content-Type: application/json" \
  -d '{
    "vehicle_id": 1,
    "cert_name": "pilot1.p12",
    "connection_port": 14591,
    "vehicle_type": "quad",
    "altitude": 50
  }'

# Get pilot status
curl http://localhost:8000/api/v1/pilots/pilot_1/status

# List all pilots
curl http://localhost:8000/api/v1/pilots

# Remove pilot
curl -X DELETE http://localhost:8000/api/v1/pilots/pilot_1
```

---

## Questions?

If any of these requirements are unclear or need modification, please update this document and coordinate with the frontend developer.
