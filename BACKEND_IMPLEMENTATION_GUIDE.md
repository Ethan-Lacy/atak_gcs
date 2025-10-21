# Backend Implementation Guide

This guide provides step-by-step instructions for implementing the Agent Manager backend using FastAPI and pymavlink.

## Table of Contents

1. [Environment Setup](#environment-setup)
2. [Project Structure](#project-structure)
3. [Implementation Steps](#implementation-steps)
4. [Testing](#testing)
5. [Deployment](#deployment)

---

## Environment Setup

### Prerequisites

- Python 3.8 or higher
- pip package manager
- ArduPilot SITL (for testing)

### Create Virtual Environment

```bash
# Create project directory
mkdir agent_manager_backend
cd agent_manager_backend

# Create virtual environment
python -m venv venv

# Activate virtual environment
# On Windows:
venv\Scripts\activate
# On Linux/Mac:
source venv/bin/activate

# Install dependencies
pip install fastapi uvicorn pymavlink python-multipart
```

### Install ArduPilot SITL (for testing)

```bash
# Clone ArduPilot
git clone https://github.com/ArduPilot/ardupilot.git
cd ardupilot

# Install prerequisites
git submodule update --init --recursive
Tools/environment_install/install-prereqs-ubuntu.sh -y

# Build SITL
./waf configure --board sitl
./waf copter
```

---

## Project Structure

```
agent_manager_backend/
├── venv/                      # Virtual environment
├── app/
│   ├── __init__.py
│   ├── main.py               # FastAPI app entry point
│   ├── models.py             # Pydantic models for API
│   ├── config.py             # Configuration (TAK server, certs path)
│   ├── agents/
│   │   ├── __init__.py
│   │   ├── pilot_agent.py    # PilotAgent class with MAVLink
│   │   └── mission_control.py # MissionControlAgent (future)
│   └── routers/
│       ├── __init__.py
│       ├── pilots.py         # /pilots endpoints
│       ├── mission_control.py # /mission-control endpoints
│       ├── certificates.py   # /certificates endpoint
│       └── config.py         # /config/server endpoint
├── atak/
│   └── certificates/         # Store .p12 certificates here
│       ├── pilot1.p12
│       ├── pilot2.p12
│       └── mission_control.p12
├── missions/                 # Sample mission files
│   └── test_mission.txt
├── requirements.txt
└── README.md
```

---

## Implementation Steps

### Step 1: Create `requirements.txt`

```txt
fastapi==0.109.0
uvicorn[standard]==0.27.0
pymavlink==2.4.49
python-multipart==0.0.6
pydantic==2.5.0
```

### Step 2: Create `app/config.py`

```python
import os

class Config:
    # API Settings
    API_HOST = "0.0.0.0"
    API_PORT = 8000
    API_BASE_URL = "/api/v1"

    # TAK Server Settings (read-only, exposed via /config/server)
    TAK_SERVER_URL = os.getenv("TAK_SERVER_URL", "takserver.example.com")
    TAK_SSL_PORT = int(os.getenv("TAK_SSL_PORT", "8089"))
    TAK_TCP_PORT = int(os.getenv("TAK_TCP_PORT", "8087"))
    TAK_CLIENT_PASSWORD = os.getenv("TAK_CLIENT_PASSWORD", "atakatak")  # Not exposed

    # Certificate Directory
    CERT_DIR = os.path.join(os.path.dirname(__file__), "..", "atak", "certificates")

    # MAVLink Settings
    MAVLINK_TIMEOUT = 10.0  # Seconds to wait for heartbeat
    TELEMETRY_RATE = 1.0    # Seconds between telemetry updates

config = Config()
```

### Step 3: Create `app/models.py`

```python
from pydantic import BaseModel, Field
from typing import List, Dict, Optional, Any
from datetime import datetime

class PilotConfig(BaseModel):
    vehicle_id: int = Field(..., description="Unique vehicle ID")
    cert_name: str = Field(..., description="Certificate filename")
    connection_port: int = Field(..., description="MAVLink UDP port")
    vehicle_type: str = Field(..., pattern="^(quad|vtol)$", description="Vehicle type")
    altitude: int = Field(..., description="Default altitude in meters")

class MissionControlConfig(BaseModel):
    cert_name: str = Field(..., description="Certificate filename")

class AgentInfo(BaseModel):
    agent_id: str
    type: str  # "pilot" or "mission_control"
    status: str  # "connected", "disconnected", "error"
    details: Dict[str, Any]
    created_at: datetime

class PositionData(BaseModel):
    latitude: float
    longitude: float
    altitude: float
    relative_altitude: Optional[float] = None
    heading: Optional[float] = None
    groundspeed: Optional[float] = None
    timestamp: datetime

class BatteryData(BaseModel):
    percentage: int
    voltage: float
    current: Optional[float] = None
    timestamp: datetime

class MissionWaypoint(BaseModel):
    sequence: int
    command: str
    latitude: float
    longitude: float
    altitude: float
    param1: float
    param2: float
    param3: float
    param4: float
    is_current: bool
    is_reached: bool

class WaypointData(BaseModel):
    current: int
    total: int
    mission_status: str  # "not_started", "in_progress", "completed", "paused", "aborted"
    waypoints: List[MissionWaypoint]

class AgentStatus(BaseModel):
    agent_id: str
    status: str
    position: Optional[PositionData] = None
    battery: Optional[BatteryData] = None
    flight_mode: Optional[str] = None
    armed: Optional[bool] = None
    waypoints: Optional[WaypointData] = None

class CertificateInfo(BaseModel):
    name: str
    path: str
    is_valid: bool

class ServerConfig(BaseModel):
    server_url: str
    ssl_port: int
    tcp_port: int
    mavlink_enabled: bool = True
    active_connections: int = 0

class ApiResponse(BaseModel):
    success: bool
    data: Optional[Any] = None
    error: Optional[str] = None
```

### Step 4: Create `app/agents/pilot_agent.py`

```python
from pymavlink import mavutil
import threading
from datetime import datetime
from typing import Optional, List, Dict, Any
import time

class PilotAgent:
    """MAVLink-connected pilot agent"""

    def __init__(self, vehicle_id: int, connection_string: str, cert_name: str, vehicle_type: str):
        self.vehicle_id = vehicle_id
        self.connection_string = connection_string
        self.cert_name = cert_name
        self.vehicle_type = vehicle_type
        self.agent_id = f"pilot_{vehicle_id}"

        # State
        self.position: Optional[Dict[str, Any]] = None
        self.battery: Optional[Dict[str, Any]] = None
        self.flight_mode: Optional[str] = None
        self.armed: bool = False
        self.current_mission: List[Dict[str, Any]] = []
        self.current_waypoint: int = 0
        self.status: str = "connecting"
        self.created_at = datetime.utcnow()

        # MAVLink connection
        self.mav = None
        self.running = False
        self.thread = None

    def connect(self, timeout: float = 10.0):
        """Connect to MAVLink and wait for heartbeat"""
        try:
            print(f"[{self.agent_id}] Connecting to {self.connection_string}")
            self.mav = mavutil.mavlink_connection(self.connection_string)

            # Wait for heartbeat
            self.mav.wait_heartbeat(timeout=timeout)
            print(f"[{self.agent_id}] Heartbeat from system {self.mav.target_system}, component {self.mav.target_component}")

            self.status = "connected"

            # Start telemetry loop
            self.running = True
            self.thread = threading.Thread(target=self._telemetry_loop, daemon=True)
            self.thread.start()

            # Download mission
            self.download_mission()

            return True

        except Exception as e:
            print(f"[{self.agent_id}] Connection failed: {e}")
            self.status = "error"
            raise

    def _telemetry_loop(self):
        """Background thread reading MAVLink messages"""
        print(f"[{self.agent_id}] Telemetry loop started")

        while self.running:
            try:
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
                        'groundspeed': ((msg.vx**2 + msg.vy**2)**0.5) / 100.0,
                        'timestamp': datetime.utcnow()
                    }

                elif msg_type == 'SYS_STATUS':
                    self.battery = {
                        'voltage': msg.voltage_battery / 1000.0,
                        'current': msg.current_battery / 100.0 if msg.current_battery != -1 else None,
                        'percentage': msg.battery_remaining,
                        'timestamp': datetime.utcnow()
                    }

                elif msg_type == 'HEARTBEAT':
                    self.flight_mode = self._parse_flight_mode(msg.custom_mode)
                    self.armed = (msg.base_mode & mavutil.mavlink.MAV_MODE_FLAG_SAFETY_ARMED) != 0

                elif msg_type == 'MISSION_CURRENT':
                    self.current_waypoint = msg.seq
                    self._update_waypoint_status()

            except Exception as e:
                print(f"[{self.agent_id}] Telemetry error: {e}")
                time.sleep(1)

        print(f"[{self.agent_id}] Telemetry loop stopped")

    def _parse_flight_mode(self, custom_mode: int) -> str:
        """Convert ArduPilot custom_mode to string"""
        # ArduCopter modes
        modes = {
            0: 'STABILIZE', 1: 'ACRO', 2: 'ALT_HOLD',
            3: 'AUTO', 4: 'GUIDED', 5: 'LOITER',
            6: 'RTL', 7: 'CIRCLE', 9: 'LAND',
            16: 'POSHOLD', 17: 'BRAKE'
        }
        return modes.get(custom_mode, f'UNKNOWN_{custom_mode}')

    def download_mission(self):
        """Request mission from vehicle using MAVLink mission protocol"""
        try:
            print(f"[{self.agent_id}] Requesting mission...")

            # Request mission count
            self.mav.mav.mission_request_list_send(
                self.mav.target_system,
                self.mav.target_component
            )

            # Wait for MISSION_COUNT
            msg = self.mav.recv_match(type='MISSION_COUNT', blocking=True, timeout=5.0)
            if msg is None:
                print(f"[{self.agent_id}] No MISSION_COUNT received")
                return

            mission_count = msg.count
            print(f"[{self.agent_id}] Mission has {mission_count} items")

            if mission_count == 0:
                self.current_mission = []
                return

            mission = []

            # Request each waypoint
            for seq in range(mission_count):
                self.mav.mav.mission_request_int_send(
                    self.mav.target_system,
                    self.mav.target_component,
                    seq
                )

                # Wait for MISSION_ITEM_INT
                msg = self.mav.recv_match(type='MISSION_ITEM_INT', blocking=True, timeout=5.0)
                if msg is None:
                    print(f"[{self.agent_id}] Failed to receive waypoint {seq}")
                    break

                waypoint = {
                    'sequence': msg.seq,
                    'command': self._mavlink_cmd_to_string(msg.command),
                    'latitude': msg.x / 1e7,
                    'longitude': msg.y / 1e7,
                    'altitude': msg.z,
                    'param1': msg.param1,
                    'param2': msg.param2,
                    'param3': msg.param3,
                    'param4': msg.param4,
                    'is_current': msg.seq == self.current_waypoint,
                    'is_reached': msg.seq < self.current_waypoint
                }
                mission.append(waypoint)

            self.current_mission = mission
            print(f"[{self.agent_id}] Downloaded {len(mission)} waypoints")

        except Exception as e:
            print(f"[{self.agent_id}] Mission download error: {e}")

    def _mavlink_cmd_to_string(self, cmd: int) -> str:
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

    def _update_waypoint_status(self):
        """Update is_current and is_reached flags for all waypoints"""
        for wp in self.current_mission:
            wp['is_current'] = wp['sequence'] == self.current_waypoint
            wp['is_reached'] = wp['sequence'] < self.current_waypoint

    def get_status(self) -> Dict[str, Any]:
        """Get current agent status"""
        mission_status = "not_started"
        if self.current_mission:
            if self.current_waypoint == 0:
                mission_status = "not_started"
            elif self.current_waypoint >= len(self.current_mission):
                mission_status = "completed"
            else:
                mission_status = "in_progress"

        return {
            'agent_id': self.agent_id,
            'status': self.status,
            'position': self.position,
            'battery': self.battery,
            'flight_mode': self.flight_mode,
            'armed': self.armed,
            'waypoints': {
                'current': self.current_waypoint,
                'total': len(self.current_mission),
                'mission_status': mission_status,
                'waypoints': self.current_mission
            } if self.current_mission else None
        }

    def stop(self):
        """Stop telemetry loop and close connection"""
        print(f"[{self.agent_id}] Stopping...")
        self.running = False

        if self.thread:
            self.thread.join(timeout=2.0)

        if self.mav:
            self.mav.close()

        self.status = "disconnected"
        print(f"[{self.agent_id}] Stopped")
```

### Step 5: Create `app/routers/pilots.py`

```python
from fastapi import APIRouter, HTTPException
from typing import Dict
from app.models import PilotConfig, AgentInfo, AgentStatus
from app.agents.pilot_agent import PilotAgent
from datetime import datetime

router = APIRouter(prefix="/pilots", tags=["pilots"])

# Global agent registry
agents: Dict[str, PilotAgent] = {}

@router.post("", response_model=AgentInfo)
async def add_pilot(config: PilotConfig):
    """Add a new pilot agent with MAVLink connection"""

    agent_id = f"pilot_{config.vehicle_id}"

    # Check if agent already exists
    if agent_id in agents:
        raise HTTPException(status_code=409, detail=f"Agent {agent_id} already exists")

    # Create connection string
    connection_string = f"udp:127.0.0.1:{config.connection_port}"

    # Create agent
    agent = PilotAgent(
        vehicle_id=config.vehicle_id,
        connection_string=connection_string,
        cert_name=config.cert_name,
        vehicle_type=config.vehicle_type
    )

    # Connect to MAVLink
    try:
        agent.connect(timeout=10.0)
    except Exception as e:
        raise HTTPException(
            status_code=503,
            detail=f"Cannot connect to MAVLink at {connection_string}: {str(e)}. Is SITL running?"
        )

    # Add to registry
    agents[agent_id] = agent

    return AgentInfo(
        agent_id=agent_id,
        type="pilot",
        status=agent.status,
        details={
            "vehicle_id": config.vehicle_id,
            "vehicle_type": config.vehicle_type,
            "mavlink_port": config.connection_port,
            "heartbeat_received": True
        },
        created_at=agent.created_at
    )

@router.get("", response_model=list[AgentInfo])
async def get_pilots():
    """Get list of active pilot agents"""
    result = []

    for agent_id, agent in agents.items():
        result.append(AgentInfo(
            agent_id=agent_id,
            type="pilot",
            status=agent.status,
            details={
                "vehicle_id": agent.vehicle_id,
                "vehicle_type": agent.vehicle_type,
                "mavlink_port": agent.connection_string.split(":")[-1]
            },
            created_at=agent.created_at
        ))

    return result

@router.get("/{pilot_id}/status", response_model=AgentStatus)
async def get_pilot_status(pilot_id: str):
    """Get detailed status of a pilot agent"""

    if pilot_id not in agents:
        raise HTTPException(status_code=404, detail=f"Agent {pilot_id} not found")

    agent = agents[pilot_id]
    return AgentStatus(**agent.get_status())

@router.delete("/{pilot_id}")
async def remove_pilot(pilot_id: str):
    """Remove a pilot agent"""

    if pilot_id not in agents:
        raise HTTPException(status_code=404, detail=f"Agent {pilot_id} not found")

    agent = agents[pilot_id]
    agent.stop()
    del agents[pilot_id]

    return {"success": True}
```

### Step 6: Create `app/routers/certificates.py`

```python
from fastapi import APIRouter
import os
from app.config import config
from app.models import CertificateInfo

router = APIRouter(prefix="/certificates", tags=["certificates"])

@router.get("", response_model=list[CertificateInfo])
async def get_certificates():
    """Get list of available certificates"""

    cert_dir = config.CERT_DIR
    certs = []

    if not os.path.exists(cert_dir):
        os.makedirs(cert_dir)
        return []

    for filename in os.listdir(cert_dir):
        if filename.endswith('.p12'):
            cert_path = os.path.join(cert_dir, filename)
            certs.append(CertificateInfo(
                name=filename,
                path=cert_path,
                is_valid=os.path.isfile(cert_path)
            ))

    return certs
```

### Step 7: Create `app/routers/config.py`

```python
from fastapi import APIRouter
from app.config import config
from app.models import ServerConfig

router = APIRouter(prefix="/config", tags=["config"])

@router.get("/server", response_model=ServerConfig)
async def get_server_config():
    """Get TAK server configuration (read-only)"""

    # Count active MAVLink connections
    from app.routers.pilots import agents
    active_connections = len([a for a in agents.values() if a.status == "connected"])

    return ServerConfig(
        server_url=config.TAK_SERVER_URL,
        ssl_port=config.TAK_SSL_PORT,
        tcp_port=config.TAK_TCP_PORT,
        mavlink_enabled=True,
        active_connections=active_connections
    )
```

### Step 8: Create `app/main.py`

```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.routers import pilots, certificates, config
from app.config import config as app_config

app = FastAPI(
    title="Agent Manager API",
    description="MAVLink-based agent management for WinTAK plugin",
    version="1.0.0"
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include routers
app.include_router(config.router, prefix=app_config.API_BASE_URL)
app.include_router(certificates.router, prefix=app_config.API_BASE_URL)
app.include_router(pilots.router, prefix=app_config.API_BASE_URL)

@app.get("/")
async def root():
    return {
        "message": "Agent Manager API",
        "version": "1.0.0",
        "docs": "/docs"
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "app.main:app",
        host=app_config.API_HOST,
        port=app_config.API_PORT,
        reload=True
    )
```

---

## Testing

### 1. Start ArduPilot SITL

```bash
# Terminal 1 - Vehicle 1
cd ~/ardupilot/ArduCopter
sim_vehicle.py --instance 0 -v ArduCopter --map --console --out=udpout:127.0.0.1:14550

# Terminal 2 - Vehicle 2
cd ~/ardupilot/ArduCopter
sim_vehicle.py --instance 1 -v ArduCopter --map --console --out=udpout:127.0.0.1:14551
```

### 2. Load a Mission

In MAVProxy console (Terminal 1):
```
wp load ../missions/test_mission.txt
arm throttle
mode auto
```

### 3. Start Backend

```bash
# Terminal 3
cd agent_manager_backend
source venv/bin/activate
python -m app.main
```

### 4. Test API

```bash
# Check API availability
curl http://localhost:8000/api/v1/config/server

# Add pilot 1
curl -X POST http://localhost:8000/api/v1/pilots \
  -H "Content-Type: application/json" \
  -d '{
    "vehicle_id": 1,
    "cert_name": "pilot1.p12",
    "connection_port": 14550,
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

### 5. Test with WinTAK Plugin

1. Start WinTAK
2. Open Agent Manager plugin
3. Add Pilot:
   - Vehicle ID: 1
   - Certificate: pilot1.p12
   - Connection Port: 14550
   - Vehicle Type: quad
   - Altitude: 50
4. Click "📍 View Mission"
5. Should display live waypoints from SITL!

---

## Deployment

### Production Settings

Create `.env` file:
```bash
TAK_SERVER_URL=your-tak-server.com
TAK_SSL_PORT=8089
TAK_TCP_PORT=8087
TAK_CLIENT_PASSWORD=your_password
```

### Run with Gunicorn

```bash
pip install gunicorn

gunicorn app.main:app \
  --workers 4 \
  --worker-class uvicorn.workers.UvicornWorker \
  --bind 0.0.0.0:8000
```

### Systemd Service

Create `/etc/systemd/system/agent-manager.service`:
```ini
[Unit]
Description=Agent Manager Backend
After=network.target

[Service]
Type=notify
User=your_user
WorkingDirectory=/path/to/agent_manager_backend
Environment="PATH=/path/to/venv/bin"
ExecStart=/path/to/venv/bin/gunicorn app.main:app --workers 4 --worker-class uvicorn.workers.UvicornWorker --bind 0.0.0.0:8000
Restart=always

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable agent-manager
sudo systemctl start agent-manager
sudo systemctl status agent-manager
```

---

## Troubleshooting

### MAVLink Connection Errors

**Problem**: `503 Service Unavailable: Cannot connect to MAVLink`

**Solutions**:
1. Verify SITL is running: `ps aux | grep sim_vehicle`
2. Check UDP port: `netstat -an | grep 14550`
3. Test with MAVProxy: `mavproxy.py --master=udp:127.0.0.1:14550`

### No Mission Downloaded

**Problem**: Pilot connects but shows 0 waypoints

**Solutions**:
1. Load mission in MAVProxy: `wp load mission.txt`
2. Verify mission in MAVProxy: `wp list`
3. Re-download mission: Restart backend or click refresh

### High CPU Usage

**Problem**: Backend using 100% CPU

**Solutions**:
1. Reduce telemetry rate in `config.py`: `TELEMETRY_RATE = 2.0`
2. Add sleep in telemetry loop: `time.sleep(0.1)`
3. Use async MAVLink library (advanced)

---

## Next Steps

1. ✅ Implement Mission Control agent
2. ✅ Add ATAK CoT message broadcasting
3. ✅ Implement mission upload to vehicles
4. ✅ Add WebSocket for real-time updates
5. ✅ Add authentication (API keys)

---

## Resources

- **pymavlink Documentation**: https://mavlink.io/en/mavgen_python/
- **FastAPI Documentation**: https://fastapi.tiangolo.com/
- **ArduPilot SITL**: https://ardupilot.org/dev/docs/using-sitl-for-ardupilot-testing.html
- **MAVLink Protocol**: https://mavlink.io/en/services/mission.html

---

## Support

For issues or questions:
1. Check backend logs: `tail -f /var/log/agent-manager.log`
2. Review SITL output in MAVProxy console
3. Test endpoints with curl or Postman
4. Verify network connectivity: `ping 127.0.0.1`
