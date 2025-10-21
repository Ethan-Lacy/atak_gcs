# Mission Download Fix - CRITICAL SETUP CHANGE

## ✅ What Was Fixed

Based on the `WinTAK_MiniGCS_Implementation_Guide.md`, the plugin now:

1. **Uses UDP Connect()** instead of Bind() - connects as a CLIENT to MAVProxy's UDP SERVER
2. **Sends commands properly** using the connected socket
3. **Auto-requests missions** on first heartbeat
4. **Supports both MISSION_ITEM_INT and MISSION_ITEM** (legacy fallback)

## 🔧 REQUIRED: Update Your MAVProxy Command

### ❌ OLD (One-Way, Broken):
```bash
--out=udp:127.0.0.1:14551
```

### ✅ NEW (Bidirectional, Working):
```bash
--out=udpin:0.0.0.0:14551
```

## 📝 Full MAVProxy Command

Replace your current MAVProxy command with:

```bash
py -3.13 -m MAVProxy.mavproxy ^
  --master=tcp:127.0.0.1:5760 ^
  --master=tcp:127.0.0.1:5770 ^
  --master=tcp:127.0.0.1:5780 ^
  --master=tcp:127.0.0.1:5790 ^
  --out=udp:127.0.0.1:14550 ^
  --out=udpin:0.0.0.0:14551 ^
  --no-console
```

**Key difference:**
- `--out=udpin:0.0.0.0:14551` - MAVProxy **LISTENS** on port 14551 (UDP server)
- Plugin connects TO port 14551 as a client
- **Bidirectional communication works!**

## 🧪 Testing Steps

1. **Stop current MAVProxy**
2. **Start MAVProxy with NEW command** (with `udpin`)
3. **Start WinTAK**
4. **Open Agent Manager plugin**
5. **Connect to port 14551**
6. **Watch for:**
   - Drones detected
   - Status shows packets/messages increasing
   - **Mission should auto-download after first heartbeat!**

7. **Upload a mission in QGC**
8. **Click "Refresh All Missions"** in WinTAK plugin
9. **Mission should appear with waypoints**

## 🔍 Debug Info

The plugin now logs to debug output:
- "Connected UDP client to MAVProxy at 127.0.0.1:14551"
- "*** REQUESTING MISSION LIST for Drone X ***"
- "Sending MISSION_REQUEST_LIST packet"
- "Drone X: Mission has N waypoints"
- "Drone X: Received waypoint..."

## 📋 What Still Needs Implementation

Per the guide, for robust mission download we still need:

1. **MISSION_REQUEST_INT** loop (currently only sends MISSION_REQUEST_LIST)
2. **Per-item requests** with sequence numbers
3. **Retry/timeout logic** for individual waypoints
4. **Pipelining** (request 2-4 items at once)
5. **State machine** to track download progress

**Current implementation:**
- ✅ Sends MISSION_REQUEST_LIST
- ✅ Receives MISSION_COUNT
- ✅ Receives MISSION_ITEM_INT / MISSION_ITEM
- ❌ Missing individual item requests (expects autopilot to send all items)

This may work with some autopilots but is not robust. Full state machine needed for production use.

## 🎯 Next Phase: Map Markers

Once missions are working, implement Phase 2:
- CoT markers for drone positions
- Real-time position updates on map
- Mission route polylines
- Waypoint markers with numbers
