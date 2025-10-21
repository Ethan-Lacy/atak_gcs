# Agent Manager Architecture

## Concept Separation

### **Drones** (MAVLink Connections)
- Direct UDP connection to SITL instances
- Real-time telemetry (position, battery, flight mode)
- Mission waypoint download and visualization
- Plot map display
- **Independent of backend**

### **Agents** (Backend API @ localhost:8000)
- Pilot agents coordinated by backend
- Mission control agents
- Certificate management
- TAK server integration
- **Uses backend API**

## Workflow

```
1. User adds Drones:
   - Drone ID: drone_1
   - System ID: 1
   - Host: 127.0.0.1
   - Port: 14550
   → Direct MAVLink connection established

2. User adds Agents (via backend):
   - Vehicle ID: 1
   - Select Drone: drone_1  ← Links to existing drone
   - Certificate: pilot1.p12
   → Backend creates agent, associates with drone's MAVLink connection

3. View Missions:
   - Click "View Mission" on Drone → Shows plot map
   - Agent status shows which drone it's controlling
```

## UI Layout

```
┌─────────────────────────────────────────┐
│  AGENT MANAGER                          │
├─────────────────────────────────────────┤
│                                         │
│  ┌──────────────────────────────────┐  │
│  │ DRONES (MAVLink)                 │  │
│  ├──────────────────────────────────┤  │
│  │ Add Drone                        │  │
│  │  - Drone ID                      │  │
│  │  - System ID                     │  │
│  │  - Host                          │  │
│  │  - Port                          │  │
│  │  [Connect to Drone]              │  │
│  ├──────────────────────────────────┤  │
│  │ Active Drones:                   │  │
│  │  • drone_1 (Connected)           │  │
│  │    📍 38.87, -77.05 @ 50m        │  │
│  │    🔋 78% (22.4V)                │  │
│  │    ✈️ AUTO (ARMED)               │  │
│  │    📋 Mission: 3/13 waypoints    │  │
│  │    [View Mission] [Disconnect]   │  │
│  └──────────────────────────────────┘  │
│                                         │
│  ┌──────────────────────────────────┐  │
│  │ AGENTS (Backend API)             │  │
│  ├──────────────────────────────────┤  │
│  │ Add Pilot Agent                  │  │
│  │  - Vehicle ID                    │  │
│  │  - Select Drone: [dropdown]      │  │
│  │  - Certificate: [dropdown]       │  │
│  │  - Altitude                      │  │
│  │  [Add Pilot Agent]               │  │
│  ├──────────────────────────────────┤  │
│  │ Active Agents:                   │  │
│  │  • pilot_1 (active)              │  │
│  │    Controlling: drone_1          │  │
│  │    Cert: pilot1.p12              │  │
│  │    [Remove Agent]                │  │
│  └──────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

## Benefits

1. **Drones are independent** - Can connect to SIT without backend
2. **Agents link to drones** - Reuse MAVLink connections
3. **Clear separation** - MAVLink vs Backend API
4. **Flexible** - Add/remove drones and agents independently
