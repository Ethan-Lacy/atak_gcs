# Claude Code Session Notes - WinTAK Agent Manager Plugin

## How to Build (CORRECT METHOD)

**Use MSBuild via PowerShell targeting the .csproj file directly:**

```powershell
cd "WinTAK Plugin_Agent_Manager"

# Build
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'WinTAK Plugin (5.0)_AgentManager\AgentManagerPlugin.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

**Deploy after closing WinTAK:**

```powershell
cd "WinTAK Plugin_Agent_Manager/WinTAK Plugin (5.0)_AgentManager/bin/x64/Debug"
Copy-Item AgentManagerPlugin.dll,AgentManagerPlugin.pdb,MAVLink.dll -Destination "$env:APPDATA\wintak\plugins\AgentManagerPlugin\" -Force -Verbose
```

## Important Notes

- **Build will show warnings** about Prism.MefExtensions not being found, but compilation succeeds
- **Post-build event will fail** if WinTAK is running (DLL in use) - this is expected, just deploy manually
- **Target the .csproj file**, not the .sln file
- **Use PowerShell**, not bash, for MSBuild commands (forward slashes get stripped in bash)

## Project Structure
- Language: C# / .NET Framework 4.8
- Platform: x64 only
- Dependencies: WinTAK, Prism.Mef, MAVLink
- Output: `%APPDATA%\wintak\plugins\AgentManagerPlugin\`

## Key Files
- `Services/MavlinkConnectionManager.cs` - MAVLink UDP connection handling (listen & client modes)
- `DockPanes/AgentManagerDockPane.cs` - Main UI panel
- `Services/DroneMapService.cs` - WinTAK map integration

## Connection Modes (March 2026 Update)

**Listen Mode (Default - Port 14551):**
- Plugin binds to UDP port and waits for MAVProxy packets
- MAVProxy command: `--out=udp:127.0.0.1:14551`
- Use this to see all drones automatically

**Client Mode (Port 14550/custom):**
- Plugin connects TO MAVProxy's udpin server
- MAVProxy command: `--out=udpin:0.0.0.0:14551`
- Bidirectional communication

## Recent Fixes (March 11, 2026)

**Problem:** Plugin said "Connected" but no drones appeared

**Root Cause:** Plugin was trying to CONNECT to port 14551, but MAVProxy with `--out=udp:` sends packets (needs plugin to LISTEN)

**Solution:**
1. Added `ConnectAsync(int port, bool listenMode = true)` overload
2. Added `SendPacket()` method that handles both modes:
   - Client mode: uses `_udpClient.Send(packet, length)`
   - Listen mode: uses `_udpClient.Send(packet, length, _lastReceivedFrom)`
3. Changed default port from 14550 to 14551
4. Updated UI text to show correct MAVProxy command
