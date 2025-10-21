# Agent Manager Backend Requirements (MAVLink/SITL Version)

This document specifies the API contract for the WinTAK Agent Manager plugin frontend when using **direct MAVLink connections** to SITL vehicles.

## Overview

The backend acts as a **MAVLink proxy and agent coordinator**, connecting directly to SITL instances via MAVLink UDP/TCP and exposing real-time vehicle data via REST API.

### Architecture

```
┌─────────────────────────────────────────────────────┐
│  WinTAK Plugin (Frontend)                           │
│  └─ HTTP REST Client                                │
└─────────────────────────────────────────────────────┘
                    ↓ HTTP REST
┌─────────────────────────────────────────────────────┐
│  FastAPI Backend (MAVLink Proxy)                    │
│  ├─ Agent Manager (thread coordination)             │
│  ├─ MAVLink Clients (pymavlink)                     │
│  │   ├─ Vehicle 1: udp:localhost:14551              │
│  │   ├─ Vehicle 2: udp:localhost:14552              │
│  │   └─ Vehicle 3: udp:localhost:14553              │
│  └─ Real-time telemetry parsing                     │
└─────────────────────────────────────────────────────┘
                    ↓ MAVLink UDP
┌─────────────────────────────────────────────────────┐
│  SITL Instances (ArduPilot/PX4)                     │
│  ├─ Vehicle 1 (quad): 127.0.0.1:14551               │
│  ├─ Vehicle 2 (vtol): 127.0.0.1:14552               │
│  └─ Vehicle 3 (quad): 127.0.0.1:14553               │
└─────────────────────────────────────────────────────┘
```

### Key Differences from Database Approach
- ✅ **No database** - All data comes directly from MAVLink telemetry
- ✅ **Real-time** - Backend parses live MAVLink messages
- ✅ **Mission download** - Backend requests waypoints via `MISSION_REQUEST_LIST`
- ✅ **Standard protocol** - Uses MAVLink 2.0 (ArduPilot/PX4 compatible)

---

## Base URL

```
http://localhost:8000/api/v1
```

---

## API Endpoints

### 1. Health Check

#### `GET /config/server`

**Purpose**: Verify API availability and get TAK server config.

**Response** (200 OK):
```json
{
  "server_url": "takserver.example.com",
  "ssl_port": 8089,
  "tcp_port": 8087,
  "mavlink_enabled": true,
  "active_connections": 2
}
```

---

### 2. List Certificates

#### `GET /certificates`

**Purpose**: Get available certificates for ATAK connection.

**Response** (200 OK):
```json
[
  {
    "name": "pilot1.p12",
    "path": "/path/to/atak/certificates/pilot1.p12",
    "is_valid": true
  }
]
```

---

### 3. Add Pilot Agent (MAVLink Connection)

#### `POST /pilots`

**Purpose**: Start a new pilot agent with **MAVLink SITL connection**.

**Request Body**:
```json
{
  "vehicle_id": 1,
  "cert_name": "pilot1.p12",
  "connection_port": 14551,
  "vehicle_type": "quad",
  "altitude": 50
}
```

**Field Details**:
- `vehicle_id` (int): Unique vehicle ID
- `cert_name` (string): ATAK certificate for TAK server connection
- `connection_port` (int): **MAVLink UDP port** (e.g., 14551, 14552, 14553)
- `vehicle_type` (string): `"quad"` or `"vtol"`
- `altitude` (int): Default altitude (meters)

**Backend Implementation**:
```python
# Backend connects to MAVLink
connection_string = f"udp:127.0.0.1:{config.connection_port}"
mav = mavutil.mavlink_connection(connection_string)

# Start telemetry loop in background thread
agent = PilotAgent(vehicle_id, mav, cert_name)
agent.start_telemetry_loop()
agent.download_mission()  # Request waypoints from vehicle
```

**Response** (200 OK):
```json
{
  "agent_id": "pilot_1",
  "type": "pilot",
  "status": "connected",
  "details": {
    "vehicle_id": 1,
    "vehicle_type": "quad",
    "mavlink_port": 14551,
    "heartbeat_received": true
  },
  "created_at": "2025-01-15T10:30:00Z"
}
```

**Error Responses**:
- `400 Bad Request`: Invalid input
- `503 Service Unavailable`: Cannot connect to MAVLink (SITL not running)
- `500 Internal Server Error`: Failed to start agent

---

### 4. Get Pilot Status (Real-Time MAVLink Data)

#### `GET /pilots/{pilot_id}/status`

**Purpose**: Get real-time vehicle status from **live MAVLink telemetry**.

**Backend Implementation**:
```python
# Backend reads from MAVLink message buffer
agent = agents[pilot_id]
return {
    "position": agent.latest_position,      # from GLOBAL_POSITION_INT
    "battery": agent.latest_battery,        # from SYS_STATUS
    "flight_mode": agent.latest_flight_mode,# from HEARTBEAT
    "waypoints": agent.current_mission      # from MISSION_ITEM_INT
}
```

**Response** (200 OK):
```json
{
  "agent_id": "pilot_1",
  "status": "active",
  "position": {
    "latitude": 38.870334,
    "longitude": -77.055977,
    "altitude": 50.5,
    "relative_altitude": 50.0,
    "heading": 180,
    "groundspeed": 5.2,
    "timestamp": "2025-01-15T10:45:00Z"
  },
  "battery": {
    "percentage": 78,
    "voltage": 22.4,
    "current": 15.0,
    "timestamp": "2025-01-15T10:45:00Z"
  },
  "flight_mode": "AUTO",
  "armed": true,
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
      }
    ]
  }
}
```

**MAVLink Message Sources**:
- `position` ← `GLOBAL_POSITION_INT` message
- `battery` ← `SYS_STATUS` message
- `flight_mode` ← `HEARTBEAT.custom_mode` (ArduPilot mode numbers)
- `armed` ← `HEARTBEAT.base_mode & MAV_MODE_FLAG_SAFETY_ARMED`
- `waypoints.current` ← `MISSION_CURRENT.seq`
- `waypoints.waypoints` ← `MISSION_ITEM_INT` messages

---

### 5. List Active Pilots

#### `GET /pilots`

**Response** (200 OK):
```json
[
  {
    "agent_id": "pilot_1",
    "type": "pilot",
    "status": "connected",
    "details": {
      "vehicle_id": 1,
      "vehicle_type": "quad",
      "mavlink_port": 14551,
      "heartbeat_received": true,
      "last_heartbeat": "2025-01-15T10:45:00Z"
    },
    "created_at": "2025-01-15T10:30:00Z"
  }
]
```

---

### 6. Remove Pilot Agent

#### `DELETE /pilots/{pilot_id}`

**Backend Implementation**:
```python
# Close MAVLink connection
agent = agents[pilot_id]
agent.mav.close()
agent.stop_telemetry_loop()
del agents[pilot_id]
```

**Response** (200 OK):
```json
{
  "success": true
}
```

---

### 7. Start Mission Control

#### `POST /mission-control`

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
    "monitoring_vehicles": [1, 2]
  },
  "created_at": "2025-01-15T10:50:00Z"
}
```

---

### 8. Stop Mission Control

#### `DELETE /mission-control/{mc_id}`

**Response** (200 OK):
```json
{
  "success": true
}
```

---

## MAVLink Integration Details

### Required Python Library

```bash
pip install pymavlink
```

**Latest Version**: pymavlink 2.4.49 (August 2025)
**Documentation**: https://mavlink.io/en/mavgen_python/
**GitHub**: https://github.com/ArduPilot/pymavlink

### Basic MAVLink Connection Pattern

```python
from pymavlink import mavutil
import threading

class PilotAgent:
    def __init__(self, vehicle_id, connection_string, cert_name):
        self.vehicle_id = vehicle_id
        self.connection_string = connection_string
        self.cert_name = cert_name

        # Connect to MAVLink
        self.mav = mavutil.mavlink_connection(connection_string)

        # Wait for heartbeat
        self.mav.wait_heartbeat()
        print(f"Heartbeat from system {self.mav.target_system}")

        # State
        self.position = None
        self.battery = None
        self.flight_mode = None
        self.current_mission = []
        self.current_waypoint = 0

        # Start background telemetry loop
        self.running = True
        self.thread = threading.Thread(target=self._telemetry_loop)
        self.thread.start()

    def _telemetry_loop(self):
        """Background thread reading MAVLink messages"""
        while self.running:
            msg = self.mav.recv_match(blocking=True, timeout=1.0)
            if msg is None:
                continue

            msg_type = msg.get_type()

            if msg_type == 'GLOBAL_POSITION_INT':
                self.position = {
                    'latitude': msg.lat / 1e7,
                    'longitude': msg.lon / 1e7,
                    'altitude': msg.alt / 1000.0,
                    'relative_altitude': msg.relative_alt / 1000.0,
                    'heading': msg.hdg / 100.0,
                    'groundspeed': (msg.vx**2 + msg.vy**2)**0.5 / 100.0,
                    'timestamp': datetime.utcnow().isoformat() + 'Z'
                }

            elif msg_type == 'SYS_STATUS':
                self.battery = {
                    'voltage': msg.voltage_battery / 1000.0,
                    'current': msg.current_battery / 100.0,
                    'percentage': msg.battery_remaining,
                    'timestamp': datetime.utcnow().isoformat() + 'Z'
                }

            elif msg_type == 'HEARTBEAT':
                # Parse ArduPilot flight mode
                self.flight_mode = self._parse_flight_mode(msg.custom_mode)
                self.armed = (msg.base_mode & mavutil.mavlink.MAV_MODE_FLAG_SAFETY_ARMED) != 0

            elif msg_type == 'MISSION_CURRENT':
                self.current_waypoint = msg.seq

    def _parse_flight_mode(self, custom_mode):
        """Convert ArduPilot custom_mode to string"""
        modes = {
            0: 'STABILIZE', 1: 'ACRO', 2: 'ALT_HOLD',
            3: 'AUTO', 4: 'GUIDED', 5: 'LOITER',
            6: 'RTL', 7: 'CIRCLE', 9: 'LAND',
            16: 'POSHOLD', 17: 'BRAKE'
        }
        return modes.get(custom_mode, f'UNKNOWN_{custom_mode}')

    def download_mission(self):
        """Request mission from vehicle using MAVLink mission protocol"""
        # Step 1: Request mission count using MISSION_REQUEST_LIST
        self.mav.mav.mission_request_list_send(
            self.mav.target_system,
            self.mav.target_component
        )

        # Step 2: Wait for MISSION_COUNT response
        msg = self.mav.recv_match(type='MISSION_COUNT', blocking=True, timeout=5.0)
        if msg is None:
            print("No mission on vehicle")
            return

        mission_count = msg.count
        mission = []

        # Step 3: Request each waypoint individually using MISSION_REQUEST_INT
        # Note: MISSION_ITEM_INT is preferred over deprecated MISSION_ITEM for precision
        for seq in range(mission_count):
            self.mav.mav.mission_request_int_send(
                self.mav.target_system,
                self.mav.target_component,
                seq
            )

            # Step 4: Wait for MISSION_ITEM_INT response
            msg = self.mav.recv_match(type='MISSION_ITEM_INT', blocking=True, timeout=5.0)
            if msg is None:
                print(f"Failed to receive waypoint {seq}")
                break

            # Step 5: Parse MISSION_ITEM_INT fields
            # x/y are in degrees * 1e7 (integer encoding for precision)
            waypoint = {
                'sequence': msg.seq,
                'command': self._mavlink_cmd_to_string(msg.command),
                'latitude': msg.x / 1e7,   # Convert to decimal degrees
                'longitude': msg.y / 1e7,  # Convert to decimal degrees
                'altitude': msg.z,         # Already in meters
                'param1': msg.param1,      # Command-specific (e.g., hold time)
                'param2': msg.param2,      # Command-specific (e.g., accept radius)
                'param3': msg.param3,      # Command-specific (e.g., pass radius)
                'param4': msg.param4,      # Command-specific (e.g., yaw angle)
                'is_current': msg.seq == self.current_waypoint,
                'is_reached': msg.seq < self.current_waypoint
            }
            mission.append(waypoint)

        self.current_mission = mission
        print(f"Downloaded mission with {len(mission)} waypoints")

    def _mavlink_cmd_to_string(self, cmd):
        """Convert MAVLink command ID to string"""
        commands = {
            16: 'WAYPOINT',
            22: 'TAKEOFF',
            21: 'LAND',
            20: 'RTL',
            17: 'LOITER_UNLIM',
            18: 'LOITER_TURNS',
            19: 'LOITER_TIME',
            177: 'DO_JUMP',
            178: 'DO_CHANGE_SPEED',
            179: 'DO_SET_HOME'
        }
        return commands.get(cmd, f'CMD_{cmd}')

    def stop(self):
        """Stop telemetry loop and close connection"""
        self.running = False
        self.thread.join()
        self.mav.close()
```

---

## MAVLink Mission Protocol Reference

### Mission Download Sequence

The MAVLink mission protocol follows a reliable request-response pattern:

```
GCS/Backend                          Vehicle/Autopilot
     │                                      │
     ├──────MISSION_REQUEST_LIST───────────>│  (Request mission count)
     │                                      │
     │<─────────MISSION_COUNT────────────── │  (Reply with count=N)
     │                                      │
     ├──────MISSION_REQUEST_INT(0)────────> │  (Request waypoint 0)
     │<─────MISSION_ITEM_INT(0)─────────────│  (Send waypoint 0)
     │                                      │
     ├──────MISSION_REQUEST_INT(1)────────> │  (Request waypoint 1)
     │<─────MISSION_ITEM_INT(1)─────────────│  (Send waypoint 1)
     │                                      │
     ...                                   ...
     │                                      │
     ├──────MISSION_REQUEST_INT(N-1)──────> │  (Request last waypoint)
     │<─────MISSION_ITEM_INT(N-1)───────────│  (Send last waypoint)
     │                                      │
```

### MISSION_ITEM_INT vs MISSION_ITEM

**ALWAYS use MISSION_ITEM_INT** - MISSION_ITEM is deprecated.

| Feature | MISSION_ITEM_INT | MISSION_ITEM (deprecated) |
|---------|------------------|---------------------------|
| Latitude/Longitude | int32 (degrees × 1e7) | float (degrees) |
| Precision | High precision | Precision loss |
| Status | Current standard | Deprecated |

### Common MAVLink Command IDs (MAV_CMD)

| Command ID | Name | Description |
|------------|------|-------------|
| 16 | WAYPOINT | Navigate to waypoint |
| 22 | TAKEOFF | Takeoff command |
| 21 | LAND | Land at location |
| 20 | RTL | Return to launch |
| 17 | LOITER_UNLIM | Loiter indefinitely |
| 18 | LOITER_TURNS | Loiter for N turns |
| 19 | LOITER_TIME | Loiter for N seconds |
| 177 | DO_JUMP | Jump to waypoint |
| 178 | DO_CHANGE_SPEED | Change speed |
| 179 | DO_SET_HOME | Set home position |

**Reference**: https://mavlink.io/en/services/mission.html

---

## SITL Connection Strings

### ArduPilot SITL (Default Ports)
```python
# Single vehicle (default)
"udp:127.0.0.1:14550"

# Multiple vehicles (use --instance parameter when starting SITL)
# Vehicle 1: sim_vehicle.py --instance 0 --out=udpout:127.0.0.1:14550
# Vehicle 2: sim_vehicle.py --instance 1 --out=udpout:127.0.0.1:14551
# Vehicle 3: sim_vehicle.py --instance 2 --out=udpout:127.0.0.1:14552

# Backend connections
connections = {
    1: "udp:127.0.0.1:14550",
    2: "udp:127.0.0.1:14551",
    3: "udp:127.0.0.1:14552"
}
```

### PX4 SITL
```python
# PX4 uses UDP 14540 by default
"udp:127.0.0.1:14540"
```

### TCP Alternative
```python
# If UDP doesn't work, try TCP
"tcp:127.0.0.1:5760"
```

---

## Starting SITL Instances

### ArduPilot (Multiple Vehicles)

```bash
# Terminal 1 - Vehicle 1 (quad) on UDP port 14550
cd ~/ardupilot/ArduCopter
sim_vehicle.py --instance 0 -v ArduCopter --map --console --out=udpout:127.0.0.1:14550

# Terminal 2 - Vehicle 2 (VTOL) on UDP port 14551
cd ~/ardupilot/ArduPlane
sim_vehicle.py --instance 1 -v ArduPlane --map --console --out=udpout:127.0.0.1:14551

# Terminal 3 - Vehicle 3 (quad) on UDP port 14552
cd ~/ardupilot/ArduCopter
sim_vehicle.py --instance 2 -v ArduCopter --map --console --out=udpout:127.0.0.1:14552
```

**Note**: The `--instance` parameter automatically assigns different MAVProxy ports (5760, 5770, 5780) and system IDs to each vehicle. The `--out=udpout:127.0.0.1:PORT` directs MAVLink output to specific UDP ports for backend connection.

### Loading a Mission in SITL

```bash
# In MAVProxy console
wp load /path/to/mission.txt

# Or upload from QGroundControl
# File → Load Mission → Select .plan or .waypoints file
```

---

## Mission File Format (QGroundControl)

Example `mission.txt`:
```
QGC WPL 110
0	1	0	16	0	0	0	0	38.870334	-77.055977	50.000000	1
1	0	3	22	15.000000	0.000000	0.000000	0.000000	38.870334	-77.055977	50.000000	1
2	0	3	16	0.000000	5.000000	0.000000	0.000000	38.871000	-77.056500	50.000000	1
3	0	3	16	0.000000	5.000000	0.000000	0.000000	38.871500	-77.057000	50.000000	1
4	0	3	21	0.000000	0.000000	0.000000	0.000000	38.870334	-77.055977	0.000000	1
```

---

## Error Handling

### MAVLink Connection Errors

```python
try:
    mav = mavutil.mavlink_connection(connection_string)
    mav.wait_heartbeat(timeout=10.0)
except Exception as e:
    raise HTTPException(
        status_code=503,
        detail=f"Cannot connect to MAVLink: {str(e)}. Is SITL running?"
    )
```

### Mission Download Timeout

```python
try:
    agent.download_mission()
except TimeoutError:
    # Continue without mission (vehicle may not have one loaded)
    agent.current_mission = []
```

---

## Testing the MAVLink Backend

### 1. Start SITL
```bash
cd ~/ardupilot/ArduCopter
sim_vehicle.py -v ArduCopter --map --console
```

### 2. Load a Mission
```bash
# In MAVProxy
wp load ~/missions/test_mission.txt
arm throttle
mode auto
```

### 3. Test Backend
```bash
# Add pilot
curl -X POST http://localhost:8000/api/v1/pilots \
  -H "Content-Type: application/json" \
  -d '{
    "vehicle_id": 1,
    "cert_name": "pilot1.p12",
    "connection_port": 14551,
    "vehicle_type": "quad",
    "altitude": 50
  }'

# Get real-time status
curl http://localhost:8000/api/v1/pilots/pilot_1/status
```

### 4. Verify in WinTAK Plugin
- Add pilot via UI
- Click "📍 View Mission"
- Should show live waypoints from SITL!

---

## Advantages of MAVLink Approach

✅ **No database needed** - All data is real-time from vehicle
✅ **Standard protocol** - Works with ArduPilot, PX4, any MAVLink vehicle
✅ **Real-time telemetry** - Sub-second updates
✅ **Mission sync** - Always shows actual vehicle mission
✅ **Simpler backend** - Just MAVLink proxy, no complex database logic
✅ **Scales to real hardware** - Same code works with physical drones

---

## Next Steps

1. ✅ **Install pymavlink** in backend environment
2. ✅ **Implement PilotAgent class** with MAVLink connection
3. ✅ **Add mission download** via `MISSION_REQUEST_LIST`
4. ✅ **Parse telemetry** (GLOBAL_POSITION_INT, SYS_STATUS, HEARTBEAT)
5. ✅ **Test with SITL** - Start ArduCopter SITL and connect
6. ✅ **WinTAK plugin unchanged** - Same REST API contract!

---

## Questions?

This MAVLink approach is **much cleaner** than polling a database. The backend becomes a thin MAVLink proxy that just parses messages and exposes them via REST.

Let me know if you need help implementing the `PilotAgent` MAVLink class!
