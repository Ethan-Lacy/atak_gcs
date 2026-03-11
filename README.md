# WinTAK Agent Manager Plugin

MAVLink-based drone management plugin for WinTAK that displays multiple drones on the map with telemetry and mission data.

## Features

- **Multi-drone support** - Tracks all drones by System ID from a single MAVLink connection
- **Real-time telemetry** - Position, heading, speed, battery, flight mode
- **Mission display** - Downloads and shows waypoints with current/reached status
- **Map integration** - CoT markers on WinTAK map with bearing lines
- **Listen & Client modes** - Flexible UDP connection modes for different MAVProxy setups

## Prerequisites

- **WinTAK 5.5.0.157** installed at `C:\Program Files\WinTAK\`
- **Visual Studio 2022** with .NET Framework 4.8 development tools
- **MSBuild** (included with Visual Studio)
- **MAVProxy** or ArduPilot SITL for testing

## Building

**Use PowerShell with MSBuild targeting the .csproj file:**

```powershell
cd "WinTAK Plugin_Agent_Manager"

# Build
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'WinTAK Plugin (5.0)_AgentManager\AgentManagerPlugin.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

**Deploy (after closing WinTAK):**

```powershell
cd "WinTAK Plugin_Agent_Manager/WinTAK Plugin (5.0)_AgentManager/bin/x64/Debug"
Copy-Item AgentManagerPlugin.dll,AgentManagerPlugin.pdb,MAVLink.dll -Destination "$env:APPDATA\wintak\plugins\AgentManagerPlugin\" -Force -Verbose
```

**Important Notes:**
- Build warnings about "Prism.MefExtensions" are normal - compilation succeeds anyway
- Post-build event will fail if WinTAK is running (DLL in use) - just deploy manually
- Must use PowerShell, not bash (bash strips forward slashes from MSBuild arguments)
- Target the .csproj file directly, not the .sln file

## Usage

### Listen Mode (Default - Recommended)

**Plugin listens on UDP port 14551 for MAVProxy packets:**

1. Start MAVProxy with your vehicles:
   ```bash
   mavproxy.py --master=<connection> --out=udp:127.0.0.1:14551
   ```

2. Start WinTAK and open **Agent Manager** plugin

3. Click **"📡 Connect to MAVLink"** (uses port 14551 by default)

4. All drones will appear automatically as MAVProxy broadcasts their telemetry

### Client Mode (Alternative)

**Plugin connects TO MAVProxy's UDP server:**

1. Start MAVProxy with udpin server:
   ```bash
   mavproxy.py --master=<connection> --out=udpin:0.0.0.0:14551
   ```

2. In plugin UI, change code to use `listenMode: false` (requires rebuild)

## Testing with SITL

```bash
# Terminal 1: Start SITL vehicle 1 (System ID 1)
sim_vehicle.py --instance 0 -v ArduCopter --console --out=udp:127.0.0.1:14550

# Terminal 2: Start SITL vehicle 2 (System ID 2)
sim_vehicle.py --instance 1 -v ArduPlane --console --out=udp:127.0.0.1:14550

# Terminal 3: MAVProxy to aggregate and forward
mavproxy.py --master=udp:127.0.0.1:14550 --master=udp:127.0.0.1:14560 --out=udp:127.0.0.1:14551
```

## Troubleshooting

### Plugin shows "Connected" but no drones appear
- Verify MAVProxy is using `--out=udp:127.0.0.1:14551` (not `udpin`)
- Check firewall isn't blocking UDP port 14551
- Look for packet count increasing in status line (e.g., "Pkts: 123")
- Check Visual Studio Debug Output for MAVLink messages

### Build fails
- Ensure WinTAK is completely closed before building
- Use PowerShell, not bash
- Target the .csproj file, not the .sln

### Port already in use
- Close QGroundControl, Mission Planner, or other GCS applications
- Try a different port (14550, 14591, etc.)

## Architecture

- **MavlinkConnectionManager** - UDP connection, MAVLink parsing, drone state tracking
- **DroneMapService** - WinTAK CoT marker generation and map updates
- **AgentManagerDockPane** - UI for connection and drone list display
- **MissionPlotWindow** - Popup window showing waypoint details

## Connection Modes Explained

**Listen Mode:**
- Plugin binds to UDP port and waits for incoming packets
- MAVProxy sends packets to plugin's port
- Use: `--out=udp:127.0.0.1:14551`
- Plugin automatically learns sender address from first packet

**Client Mode:**
- Plugin connects TO MAVProxy's UDP server port
- MAVProxy listens for incoming connections
- Use: `--out=udpin:0.0.0.0:14551`
- Bidirectional from the start

## References

- WinTAK Plugin Development: https://github.com/Cale-Torino/WinTAK_Simple_Usage_Plugin
- MAVLink Protocol: https://mavlink.io/
- ArduPilot SITL: https://ardupilot.org/dev/docs/sitl-simulator-software-in-the-loop.html
