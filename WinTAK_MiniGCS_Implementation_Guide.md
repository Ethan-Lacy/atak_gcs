# WinTAK Mini-GCS (MAVLink) — Implementation Guide

**Audience:** WinTAK/ATAK plugin developers  
**Goal:** Implement a lightweight, low-CPU “mini-GCS” that can discover/connect to vehicles via MAVProxy, **skip parameter bulk download**, and reliably **download & render missions** and **basic telemetry** (position, heading, speed, active leg), supporting **multiple vehicles**.

---

## 0) TL;DR (copy/paste plan)

1. **MAVProxy plumbing (two-way):**
   - Run MAVProxy with your masters and **two outs**: one for QGC (optional) and one as a UDP server for your plugin.
   ```bash
   py -3.13 -m MAVProxy.mavproxy ^
     --master=tcp:127.0.0.1:5760 --master=tcp:127.0.0.1:5770 ^
     --master=tcp:127.0.0.1:5780 --master=tcp:127.0.0.1:5790 ^
     --out=udp:127.0.0.1:14550 ^        # optional: for QGC default UDP autoconnect
     --out=udpin:0.0.0.0:14551 ^        # your WinTAK plugin connects here
     --no-console
   ```

2. **WinTAK plugin:**
   - Open a UDP client socket to `127.0.0.1:14551` (one socket per plugin instance is enough; disambiguate vehicles by **SYSID**).
   - Implement a MAVLink session per vehicle (`SYSID`→`VehicleSession`).

3. **Skip parameters** at startup. Do **not** send `PARAM_REQUEST_LIST`. Use `PARAM_REQUEST_READ` later only if you need specific values.

4. **Mission download** after receiving the first `HEARTBEAT` for a SYSID:
   - Send `MISSION_REQUEST_LIST` → receive `MISSION_COUNT (n)`.
   - Pipeline `MISSION_REQUEST_INT(i)` for `i=0..n-1` with small in-flight window (2–4), per-item timeout/retry (3x).
   - Accept fallback `MISSION_ITEM` if autopilot does not support `_INT` forms.
   - On completion, send `MISSION_ACK (ACCEPTED)` and update UI.

5. **Telemetry subscribe & render:**
   - Use `GLOBAL_POSITION_INT`, `ATTITUDE`, `VFR_HUD`, `MISSION_CURRENT`.
   - Map arrows from yaw/heading, speed from `VFR_HUD.groundspeed`.

6. **Multi-vehicle:**
   - Ensure vehicles have **unique `SYSID_THISMAV`**; keep per-SYSID models and layers.

7. **Testing:**
   - Unit-test mission state machine with synthetic sequences, timeouts, loss injection.
   - Verify with SITL and logs from MAVProxy `--show-errors --aircraft` dirs.

---

## 1) Architecture

```
WinTAK Plugin
├─ MavlinkUdpClient (UDP to 127.0.0.1:14551)
│  ├─ Rx loop → FrameDecoder → MessageParser (MAVLink v2 framing, CRC, signature optional)
│  └─ Tx queue (rate-limited)
├─ VehicleRegistry (Map<int sysid, VehicleSession>)
│  └─ VehicleSession
│     ├─ Heartbeat/Alive tracking
│     ├─ MissionDownloader (state machine)
│     ├─ TelemetryCache (pos/att/speed/mission_current)
│     └─ UI Binding (map overlays for arrows, waypoints, lines)
└─ Logging & Metrics (per-sysid logs, mission timing, retry counts)
```

**Concurrency model:** one Rx thread, one Tx thread, and per-vehicle state machines driven by events (messages, timers). Use a single high-resolution timer wheel/scheduler to avoid per-item threads.

---

## 2) Networking (MAVProxy & sockets)

### 2.1 MAVProxy patterns

- **Recommended:** expose a UDP **server** from MAVProxy and connect from the plugin:
  - MAVProxy: `--out=udpin:0.0.0.0:14551`
  - Plugin: UDP client to `127.0.0.1:14551`

- **Also OK:** plugin hosts UDP server, MAVProxy uses client:
  - MAVProxy: `--out=udp:127.0.0.1:14551`
  - Plugin: UDP server binding `0.0.0.0:14551` (must reply to the learned peer)

**Tip:** You can still run QGC in parallel via `--out=udp:127.0.0.1:14550` without affecting the plugin.

### 2.2 WinTAK plugin UDP client (C#)

```csharp
public sealed class MavlinkUdpClient : IDisposable
{
    private readonly UdpClient _udp;
    private IPEndPoint _mavProxyEp = new IPEndPoint(IPAddress.Loopback, 14551);
    private readonly BlockingCollection<byte[]> _txQ = new( new ConcurrentQueue<byte[]>() );
    private readonly CancellationTokenSource _cts = new();

    public MavlinkUdpClient()
    {
        _udp = new UdpClient(); // ephemeral local port
        _udp.Connect(_mavProxyEp);
        Task.Run(RxLoop, _cts.Token);
        Task.Run(TxLoop, _cts.Token);
    }

    private async Task RxLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            UdpReceiveResult res = await _udp.ReceiveAsync(_cts.Token);
            OnBytesReceived(res.Buffer); // hand to MAVLink decoder
        }
    }

    private async Task TxLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            byte[] frame = _txQ.Take(_cts.Token);
            await _udp.SendAsync(frame, frame.Length);
        }
    }

    public void Send(byte[] mavlinkFrame) => _txQ.Add(mavlinkFrame);
    public void Dispose() { _cts.Cancel(); _udp.Dispose(); }
}
```

Use any robust C# MAVLink implementation, or generate code from `common.xml` (MAVLink v2).

---

## 3) MAVLink session & message plumbing

### 3.1 Session map

- Maintain `Dictionary<int, VehicleSession> bySysId`.
- On first valid `HEARTBEAT` from `sysid`, create a `VehicleSession` (with `targetComp=1` by default; update on first `HEARTBEAT`’s `autopilot`/`type` if needed).

### 3.2 Core messages to handle

- `HEARTBEAT` → create/refresh `VehicleSession`, mark alive.
- `GLOBAL_POSITION_INT` → position/alt/heading (`hdg`), velocity if present.
- `ATTITUDE` → roll/pitch/yaw (for arrow heading if you prefer attitude yaw).
- `VFR_HUD` → `groundspeed`, `throttle`, `alt` (used for speed readout).
- `MISSION_CURRENT` → highlight current leg.
- Mission protocol: `MISSION_COUNT`, `MISSION_ITEM_INT`/`MISSION_ITEM`, `MISSION_ACK`.
- Parameter (on-demand only): `PARAM_VALUE` after `PARAM_REQUEST_READ`.

---

## 4) Skipping parameter sync safely

At startup **do not** send `PARAM_REQUEST_LIST`. This avoids bandwidth spikes and CPU spikes. If a later feature needs a specific parameter, request it explicitly with `PARAM_REQUEST_READ(param_id)` and cache `PARAM_VALUE` replies in the session.

Benefits: lower CPU/IO, fewer packet drops during mission transfer, simpler UX.

---

## 5) Mission Download: state machine

A compact, loss-tolerant downloader that prefers `*_INT` messages but gracefully falls back.

### 5.1 Types

```csharp
enum MdState { Idle, RequestList, Receiving, Done, Failed }

sealed class MissionDownloader
{
    public MdState State { get; private set; } = MdState.Idle;
    private readonly IMavlinkIo _io;        // abstraction to send frames
    private readonly int _sys, _comp;       // target
    private readonly List<MissionItem> _items = new();
    private int _count = -1;
    private int _next = 0;
    private int _inFlight = 0;
    private const int MaxInFlight = 3;      // 2..4 recommended
    private int[] _retries = Array.Empty<int>(); // sized after count known
    private readonly TimerWheel _timers;    // shared scheduler
    private bool _useInt = true;            // start with _INT, allow fallback
    private const int MaxRetries = 3;
    private static readonly TimeSpan TList = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan TItem = TimeSpan.FromMilliseconds(900);

    public event Action<IReadOnlyList<MissionItem>>? Completed;
    public event Action<string>? Failed;
    // ctor omitted
}
```

### 5.2 Start & list

```csharp
public void Start()
{
    if (State != MdState.Idle) return;
    State = MdState.RequestList;
    SendMissionRequestList();
    _timers.SetTimeout("md:list", TList, OnListTimeout);
}

private void SendMissionRequestList()
{
    var msg = new mavlink_mission_request_list_t
    {
        target_system = (byte)_sys,
        target_component = (byte)_comp
    };
    _io.Send(msg);
}
```

### 5.3 Handle `MISSION_COUNT`

```csharp
public void OnMissionCount(int count)
{
    if (State != MdState.RequestList) return;
    _count = count;
    _timers.Cancel("md:list");

    if (count <= 0)
    {
        State = MdState.Done;
        Completed?.Invoke(Array.Empty<MissionItem>());
        return;
    }

    _retries = new int[count];
    State = MdState.Receiving;
    PumpRequests();
}
```

### 5.4 Pipeline requests (`*_INT` preferred)

```csharp
private void PumpRequests()
{
    while (_inFlight < MaxInFlight && _next < _count)
    {
        int i = _next++;
        RequestItem(i);
        _inFlight++;
        ArmItemTimer(i);
    }
}

private void RequestItem(int index)
{
    if (_useInt)
    {
        var req = new mavlink_mission_request_int_t
        {
            target_system = (byte)_sys,
            target_component = (byte)_comp,
            seq = (ushort)index,
            mission_type = (byte)MAV_MISSION_TYPE.MAV_MISSION_TYPE_MISSION
        };
        _io.Send(req);
    }
    else
    {
        var req = new mavlink_mission_request_t { target_system = (byte)_sys, target_component = (byte)_comp, seq = (ushort)index };
        _io.Send(req);
    }
}

private void ArmItemTimer(int index)
{
    _timers.SetTimeout($"md:item:{index}", TItem, () => OnItemTimeout(index));
}
```

### 5.5 Handle items (`MISSION_ITEM_INT` or fallback)

```csharp
public void OnMissionItemInt(int seq, MissionItem item)
{
    if (State != MdState.Receiving) return;
    AcceptItem(seq, item);
}

public void OnMissionItem(int seq, MissionItem item)
{
    if (State != MdState.Receiving) return;
    _useInt = false; // observed fallback
    AcceptItem(seq, item);
}

private void AcceptItem(int seq, MissionItem item)
{
    EnsureSize(_items, _count);
    _items[seq] = item;
    _timers.Cancel($"md:item:{seq}");
    _inFlight--;

    if (_items.All(x => x != null))
    {
        var ack = new mavlink_mission_ack_t
        {
            target_system = (byte)_sys,
            target_component = (byte)_comp,
            type = (byte)MAV_MISSION_RESULT.MAV_MISSION_ACCEPTED
        };
        _io.Send(ack);
        State = MdState.Done;
        Completed?.Invoke(_items);
        return;
    }

    PumpRequests();
}
```

### 5.6 Timeouts & retries

```csharp
private void OnItemTimeout(int index)
{
    if (State != MdState.Receiving) return;
    if (_retries[index]++ < MaxRetries)
    {
        RequestItem(index);
        ArmItemTimer(index);
    }
    else
    {
        State = MdState.Failed;
        Failed?.Invoke($"MISSION item {index} failed after retries");
    }
}

private void OnListTimeout()
{
    if (State != MdState.RequestList) return;
    State = MdState.Failed;
    Failed?.Invoke("MISSION_REQUEST_LIST timeout");
}
```

This downloader requires only HEARTBEAT + mission messages and is independent of parameter syncing.

---

## 6) Rendering in WinTAK

- **Vehicle overlays:** one CoT/overlay layer per `sysid`.  
- **Arrow/heading:** from `ATTITUDE.yaw` (radians) or `GLOBAL_POSITION_INT.hdg` (cdeg).  
- **Speed:** from `VFR_HUD.groundspeed` (m/s).  
- **Mission plan:** polyline from mission items (`MISSION_ITEM_INT` lat/lon/alt) with waypoint markers and an “active leg” highlight using `MISSION_CURRENT.seq`.

**Coordinate conversion:**  
- MAVLink lat/lon in `1e7` scaled integers.  
- Alt: use `frame` to decide AMSL vs relative; if ambiguous, display as “AGL unknown” and let users choose a mode.

---

## 7) Multiple vehicles

- Each vehicle must have a **unique SYSID** (autopilot parameter `SYSID_THISMAV`).  
- The plugin should distinguish per `sysid`; never merge message streams.  
- Expose a simple selector UI (dropdown/list) to pin the “primary” vehicle if you add command features later.

---

## 8) Robustness & performance

- **Backpressure:** limit `_txQ` size and drop stale mission requests if a newer retry supersedes them.
- **Bandwidth:** keep in-flight mission requests small (2–4).  
- **Timers:** use a single scheduler/timer wheel to scale to 10–20 vehicles.  
- **Logging:** log mission timing (`count`, total ms, retries); log dropped frames.  
- **Loss injection:** add a developer toggle to drop N% of incoming `MISSION_ITEM_*` to test retry paths.

---

## 9) Optional: targeted parameter reads

If a feature truly needs a value (e.g., frame type), issue:
- `PARAM_REQUEST_READ { param_id = "FRAME_CLASS" }`
- Start a per-request timeout (1s, 3 retries). Cache `PARAM_VALUE` in the `VehicleSession`.  
Avoid `PARAM_REQUEST_LIST` unless running on a powerful box.

---

## 10) Testing recipe

1. **SITL or real autopilots** → connect via your `--master=...` list.  
2. Start MAVProxy with the **exact command** in §0.  
3. Start plugin; verify `HEARTBEAT` is seen for each `sysid`.  
4. Trigger mission download on each; validate correct `count`, timing, and UI render.  
5. Kill/restart a MAVProxy master; ensure session resumes and mission can re-download.  
6. Loss-test (drop 20–40% mission items); ensure completion via retries.  
7. With QGC also connected on 14550, confirm no interference (both UIs behave).

---

## 11) Minimal MAVLink message map (common fields)

- `HEARTBEAT`: `system_status`, `autopilot`, `type`  
- `GLOBAL_POSITION_INT`: `lat` (1e7), `lon` (1e7), `alt` (mm AMSL), `relative_alt` (mm), `vx/vy/vz`, `hdg` (cdeg)  
- `ATTITUDE`: `roll`, `pitch`, `yaw` (rad), `rollspeed/pitchspeed/yawspeed`  
- `VFR_HUD`: `groundspeed` (m/s), `airspeed`, `throttle`, `alt`  
- `MISSION_CURRENT`: `seq`  
- `MISSION_ITEM_INT`: `seq`, `frame`, `x` (lat*1e7), `y` (lon*1e7), `z` (alt), `command`, `param1..4`

---

## 12) Notes for future features

- **Mission upload** (the reverse sequence): start with `MISSION_COUNT`, then answer `MISSION_REQUEST_INT(i)` with `MISSION_ITEM_INT(i)`, finish with `MISSION_ACK`.  
- **Geofence / Rally** use the same mission sub-protocol (`mission_type`).  
- **Signed MAVLink v2**: optionally implement signature verification for command surfaces.

---

## Appendix A — Interfaces (sketch)

```csharp
public interface IMavlinkIo
{
    void Send<T>(T msg) where T : struct; // packs MAVLink v2 frame
}

public sealed record MissionItem(
    ushort Seq, byte Frame, int LatE7, int LonE7, float Alt,
    ushort Command, float P1, float P2, float P3, float P4);
```

---

## Appendix B — TimerWheel (sketch)

```csharp
public sealed class TimerWheel
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _map = new();
    public void SetTimeout(string key, TimeSpan due, Action cb)
    {
        Cancel(key);
        var cts = new CancellationTokenSource();
        _map[key] = cts;
        Task.Delay(due, cts.Token).ContinueWith(t => { if (!t.IsCanceled) cb(); });
    }
    public void Cancel(string key)
    {
        if (_map.TryRemove(key, out var cts)) cts.Cancel();
    }
}
```

---

## Appendix C — Troubleshooting quick hits

- **No mission download:** confirm your socket can **send** to MAVProxy and you **receive** `MISSION_COUNT` after `MISSION_REQUEST_LIST`. If not, check UDP direction (`udpin` vs `udp`).  
- **Items stall mid-way:** reduce `MaxInFlight`, increase `TItem` to 1.2–1.5s, verify retries.  
- **Multiple vehicles mix:** ensure all aircraft have distinct `SYSID_THISMAV`.  
- **UI drift:** prefer `GLOBAL_POSITION_INT` heading (`hdg`) if available; fall back to `ATTITUDE.yaw`.

---

## References (put these in your ticket or README)

- MAVLink Mission Protocol (download/upload sequences, `*_INT` forms).
- MAVLink Parameter Protocol (supports targeted `PARAM_REQUEST_READ`; bulk `PARAM_REQUEST_LIST` optional).
- QGroundControl communication flow (autoconnect UDP, order: params then mission — included for parity only).
- MAVProxy `udpin`/`udpout` patterns; QGC default UDP 14550.
- SYSID uniqueness for multi-vehicle networks.
