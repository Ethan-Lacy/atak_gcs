# WinTAK Agent Manager Plugin - Improvement Plan

## Priority 1: Enhanced Mission Visualization (QGroundControl-style)

### Current State
- Waypoints shown as individual CoT markers (b-m-p-s-m type)
- Route drawn as polyline using u-d-f CoT type with link elements
- Loiter circles generated for loiter commands
- Colors: Orange (pending), Green (current), Gray (reached)

### QGroundControl Comparison
**QGC Features We Want:**
- Flight path line connecting all waypoints
- Numbered waypoint markers (1, 2, 3...)
- Loiter radius circles at loiter waypoints
- Visual distinction between waypoint types (takeoff, land, waypoint, RTL)
- Smooth curves for loiter patterns
- Active waypoint highlighting
- Mission altitude profile visualization (optional)

### Proposed Improvements

#### 1.1 Enhanced Waypoint Markers
**Implementation: CoT XML improvements**

- **Better Icons**: Use distinct CoT types/icons for different commands:
  - Takeoff: `a-f-G-E-V-T` (vertical takeoff)
  - Land: `a-f-G-E-V-L` (vertical land)
  - Waypoint: `b-m-p-w` (waypoint marker)
  - RTL: `a-f-G-E-V-R` (return to launch)
  - Loiter: `b-m-p-c` (circular pattern)

- **Numbered Labels**: Add waypoint sequence numbers to callsign
  ```xml
  <contact callsign="WP-01 TAKEOFF" />
  <labels_on value="true" />
  ```

- **Enhanced Detail Tags**:
  ```xml
  <_waypoint_
    sequence="1"
    command="TAKEOFF"
    alt_m="50"
    param1="15"
    status="pending|current|reached"
  />
  ```

#### 1.2 Improved Route Line Visualization
**Current:** Single polyline connecting all waypoints

**Improvement Options:**

**Option A: Keep CoT Polyline (Easiest)**
- Enhance existing u-d-f polyline with better styling
- Thicker line (strokeWeight="6.0")
- Gradient colors along route (fade from green to gray for completed segments)
- Add arrows/direction indicators

**Option B: Separate Route Segments (Better Visual Control)**
- Create individual CoT polylines for each segment
- Color completed segments green, upcoming orange
- Allows more dynamic updates during mission

**Recommended: Option B**

```csharp
private void DrawRouteSegment(int fromSeq, int toSeq, bool isCompleted)
{
    string color = isCompleted ? "-16711936" : "-51361"; // Green : Orange
    // Create polyline CoT from waypoint[fromSeq] to waypoint[toSeq]
    // Set strokeColor, strokeWeight
}
```

#### 1.3 Advanced Loiter Visualization
**Current:** 16-point circle around loiter waypoint

**Improvements:**
- Show loiter direction (clockwise/counter-clockwise arrow)
- Display loiter radius value as text label
- Animate circle during active loiter (optional - may require custom renderer)
- Entry/exit tangent lines

#### 1.4 Altitude Profile View (Stretch Goal)
**Implementation:** New popup window or dock pane section

- 2D chart showing waypoint sequence (X) vs altitude (Y)
- Shows climb/descent segments
- Highlights terrain clearance issues
- Click waypoint to select on map

**Technology:** WPF Chart control or custom canvas drawing

### Implementation Steps for Priority 1

1. **Phase 1: Better Waypoint Icons & Labels**
   - Update `CreateWaypointCoT()` with command-specific types
   - Add numbered callsigns
   - Test with SITL missions ✅ Quick win

2. **Phase 2: Segment-Based Route Lines**
   - Add `DrawRouteSegments()` method
   - Track mission progress to color segments
   - Replace single polyline approach ⚠️ Moderate effort

3. **Phase 3: Enhanced Loiter Circles**
   - Add direction arrows to loiter CoT
   - Display radius labels
   - Improve circle smoothness (32 points instead of 16) ✅ Easy

4. **Phase 4: Altitude Profile (Optional)**
   - Create new WPF UserControl for chart
   - Add to dock pane or popup window
   - Implement waypoint click navigation ⚠️ High effort

---

## Priority 2: Aircraft State Control (Mode Switching & Commands)

### Current State
- **Read-Only**: Plugin receives telemetry but cannot send commands
- Flight mode displayed but not controllable
- No arm/disarm buttons
- No mode selection dropdown

### QGroundControl Comparison
**QGC Command Features:**
- Mode dropdown (GUIDED, AUTO, RTL, LOITER, etc.)
- ARM/DISARM buttons with safety checks
- Takeoff button with altitude input
- Land button
- RTL button
- Pause/Resume mission buttons
- Clear mission command
- Upload mission capability

### MAVLink Command Interface Design

#### 2.1 Command Sending Infrastructure
**Add to MavlinkConnectionManager.cs:**

```csharp
/// <summary>
/// Send COMMAND_LONG to drone (MAVLink #76)
/// </summary>
public void SendCommand(byte systemId, MAVLink.MAV_CMD command,
    float param1 = 0, float param2 = 0, float param3 = 0,
    float param4 = 0, float param5 = 0, float param6 = 0, float param7 = 0)
{
    var cmd = new MAVLink.mavlink_command_long_t
    {
        target_system = systemId,
        target_component = 1,
        command = (ushort)command,
        confirmation = 0,
        param1 = param1,
        param2 = param2,
        param3 = param3,
        param4 = param4,
        param5 = param5,
        param6 = param6,
        param7 = param7
    };

    var packet = _mavlink.GenerateMAVLinkPacket10(
        MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, cmd);
    SendPacket(packet);

    System.Diagnostics.Debug.WriteLine(
        $"Sent command {command} to Drone {systemId}");
}

/// <summary>
/// Set flight mode (MAVLink COMMAND_LONG #176 DO_SET_MODE)
/// </summary>
public void SetMode(byte systemId, string modeName)
{
    // Get numeric mode from vehicle type and mode name
    uint customMode = GetCustomModeNumber(systemId, modeName);

    SendCommand(systemId, MAVLink.MAV_CMD.DO_SET_MODE,
        param1: (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED,
        param2: customMode);
}

/// <summary>
/// ARM or DISARM drone (MAVLink COMMAND_LONG #400 COMPONENT_ARM_DISARM)
/// </summary>
public void SetArmed(byte systemId, bool arm)
{
    SendCommand(systemId, MAVLink.MAV_CMD.COMPONENT_ARM_DISARM,
        param1: arm ? 1.0f : 0.0f);  // 1 = arm, 0 = disarm
}

/// <summary>
/// Takeoff command (MAVLink COMMAND_LONG #22 NAV_TAKEOFF)
/// </summary>
public void Takeoff(byte systemId, float altitudeMeters)
{
    SendCommand(systemId, MAVLink.MAV_CMD.NAV_TAKEOFF,
        param7: altitudeMeters);  // param7 = altitude
}

/// <summary>
/// Return to launch (MAVLink COMMAND_LONG #20 NAV_RETURN_TO_LAUNCH)
/// </summary>
public void ReturnToLaunch(byte systemId)
{
    SendCommand(systemId, MAVLink.MAV_CMD.NAV_RETURN_TO_LAUNCH);
}
```

#### 2.2 Mode Mapping (Copter vs Plane)
**Already implemented in `ParseFlightMode()` - reverse it:**

```csharp
private Dictionary<string, uint> GetCopterModes() => new Dictionary<string, uint>
{
    {"STABILIZE", 0}, {"ACRO", 1}, {"ALT_HOLD", 2}, {"AUTO", 3},
    {"GUIDED", 4}, {"LOITER", 5}, {"RTL", 6}, {"CIRCLE", 7},
    {"LAND", 9}, {"BRAKE", 17}, {"SMART_RTL", 21}
};

private Dictionary<string, uint> GetPlaneModes() => new Dictionary<string, uint>
{
    {"MANUAL", 0}, {"CIRCLE", 1}, {"STABILIZE", 2}, {"TRAINING", 3},
    {"ACRO", 4}, {"FBWA", 5}, {"FBWB", 6}, {"CRUISE", 7},
    {"AUTO", 10}, {"RTL", 11}, {"LOITER", 12}, {"GUIDED", 15},
    {"QSTABILIZE", 17}, {"QHOVER", 18}, {"QLOITER", 19}, {"QRTL", 21}
};

private uint GetCustomModeNumber(byte systemId, string modeName)
{
    var drone = GetDrone(systemId);
    var modeMap = (drone.VehicleType == MAVLink.MAV_TYPE.FIXED_WING)
        ? GetPlaneModes() : GetCopterModes();

    return modeMap.ContainsKey(modeName) ? modeMap[modeName] : 0;
}
```

#### 2.3 UI Controls Design

**Add to Each Drone Card in AgentManagerDockPane.cs:**

```csharp
// Mode Selection Dropdown
var modePanel = new StackPanel { Orientation = Horizontal };
var modeLabel = new TextBlock { Text = "Mode:", Foreground = Grey150 };
var modeCombo = new ComboBox
{
    Width = 100,
    Items = { "GUIDED", "AUTO", "LOITER", "RTL", "STABILIZE", "ALT_HOLD" }
};
modeCombo.SelectionChanged += (s, e) =>
{
    string selectedMode = modeCombo.SelectedItem as string;
    _mavlinkManager.SetMode(drone.SystemId, selectedMode);
};

// ARM/DISARM Toggle Button
var armButton = new ToggleButton
{
    Content = drone.Armed ? "DISARM" : "ARM",
    Width = 80,
    Background = drone.Armed ? ErrRed : OkGreen
};
armButton.Checked += (s, e) => _mavlinkManager.SetArmed(drone.SystemId, true);
armButton.Unchecked += (s, e) => _mavlinkManager.SetArmed(drone.SystemId, false);

// Quick Action Buttons
var takeoffBtn = new Button { Content = "↑ Takeoff", Width = 70 };
takeoffBtn.Click += (s, e) =>
{
    var input = Prompt.ShowDialog("Enter takeoff altitude (meters):", "Takeoff");
    if (float.TryParse(input, out float alt))
        _mavlinkManager.Takeoff(drone.SystemId, alt);
};

var rtlBtn = new Button { Content = "⌂ RTL", Width = 60 };
rtlBtn.Click += (s, e) => _mavlinkManager.ReturnToLaunch(drone.SystemId);

var landBtn = new Button { Content = "↓ Land", Width = 60 };
landBtn.Click += (s, e) => _mavlinkManager.SetMode(drone.SystemId, "LAND");
```

#### 2.4 Safety Checks & Confirmations

**Add confirmation dialogs for critical actions:**

```csharp
private void ConfirmArm(byte systemId)
{
    var result = MessageBox.Show(
        $"ARM Drone {systemId}?\n\nPropellers will spin!",
        "Confirm ARM",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (result == MessageBoxResult.Yes)
        _mavlinkManager.SetArmed(systemId, true);
}
```

### Implementation Steps for Priority 2

1. **Phase 1: Basic Command Infrastructure**
   - Add `SendCommand()` method
   - Add `SetMode()` method
   - Test mode switching with SITL ✅ Critical foundation

2. **Phase 2: ARM/DISARM Control**
   - Add `SetArmed()` method
   - Add ARM/DISARM button to UI
   - Add safety confirmation dialog ⚠️ Safety critical

3. **Phase 3: Flight Mode Dropdown**
   - Add mode dropdown to each drone card
   - Filter modes by vehicle type
   - Show current mode in dropdown ✅ Easy win

4. **Phase 4: Quick Action Buttons**
   - Takeoff with altitude input
   - RTL button
   - Land button
   - Pause/Resume mission (requires mission protocol) ⚠️ Medium effort

5. **Phase 5: Command Acknowledgment**
   - Listen for COMMAND_ACK messages (MAVLink #77)
   - Show success/failure toasts
   - Retry on failure ⚠️ Important for reliability

---

## Priority 3: UI Aesthetics & Polish

### Current State
- Functional but basic WPF interface
- Dark theme with basic colors
- Simple stack panel layout
- No animations or transitions

### Modernization Goals
- Professional, polished appearance
- Smooth transitions and animations
- Better information hierarchy
- Responsive design

### UI Improvements

#### 3.1 Color Scheme & Typography
**Enhanced Dark Theme:**

```csharp
// Modern color palette
private static SolidColorBrush Background = new SolidColorBrush(Color.FromRgb(18, 18, 18));
private static SolidColorBrush Card = new SolidColorBrush(Color.FromRgb(30, 30, 30));
private static SolidColorBrush CardHover = new SolidColorBrush(Color.FromRgb(40, 40, 40));
private static SolidColorBrush Primary = new SolidColorBrush(Color.FromRgb(66, 165, 245)); // Blue
private static SolidColorBrush Success = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
private static SolidColorBrush Warning = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
private static SolidColorBrush Danger = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
private static SolidColorBrush TextPrimary = new SolidColorBrush(Color.FromRgb(255, 255, 255));
private static SolidColorBrush TextSecondary = new SolidColorBrush(Color.FromRgb(176, 176, 176));
private static SolidColorBrush Divider = new SolidColorBrush(Color.FromRgb(50, 50, 50));

// Typography
private static FontFamily MainFont = new FontFamily("Segoe UI");
private static FontFamily MonoFont = new FontFamily("Consolas");
```

#### 3.2 Drone Card Redesign
**Current:** Simple vertical stack
**Proposed:** Card-style layout with sections

```csharp
private Border CreateDroneCard(DroneState drone)
{
    var card = new Border
    {
        Background = Card,
        BorderBrush = Divider,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16),
        Margin = new Thickness(0, 0, 0, 12)
    };

    // Add hover effect
    card.MouseEnter += (s, e) => card.Background = CardHover;
    card.MouseLeave += (s, e) => card.Background = Card;

    // Add smooth shadow effect (requires DropShadowEffect)
    card.Effect = new DropShadowEffect
    {
        Color = Colors.Black,
        Opacity = 0.3,
        BlurRadius = 15,
        ShadowDepth = 3
    };

    // Layout sections...
    return card;
}
```

#### 3.3 Status Indicators & Badges
**Visual Status:**
- Pulsing dot for armed state
- Battery gauge with color coding
- Signal strength indicator
- Mode badge with colored background

```csharp
// Animated armed indicator
var armedDot = new Ellipse
{
    Width = 10, Height = 10,
    Fill = drone.Armed ? Danger : Success
};

// Add pulsing animation if armed
if (drone.Armed)
{
    var animation = new DoubleAnimation
    {
        From = 1.0, To = 0.3,
        Duration = TimeSpan.FromSeconds(0.8),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever
    };
    armedDot.BeginAnimation(UIElement.OpacityProperty, animation);
}
```

#### 3.4 Progress Bars & Gauges
**Battery Gauge:**

```csharp
private ProgressBar CreateBatteryGauge(int percentage)
{
    var gauge = new ProgressBar
    {
        Height = 8,
        Value = percentage,
        Maximum = 100,
        BorderThickness = new Thickness(0)
    };

    // Color based on level
    gauge.Foreground = percentage > 50 ? Success :
                       percentage > 20 ? Warning : Danger;

    return gauge;
}
```

#### 3.5 Smooth Transitions
**Collapse/Expand Animations:**

```csharp
private void AnimateExpand(UIElement element)
{
    var animation = new DoubleAnimation
    {
        From = 0,
        To = element.DesiredSize.Height,
        Duration = TimeSpan.FromMilliseconds(250),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    element.BeginAnimation(FrameworkElement.HeightProperty, animation);
}
```

#### 3.6 Icon Integration
**Use icon fonts or SVG icons:**
- Segoe MDL2 Assets (built into Windows)
- Font Awesome (requires package)

```csharp
// Example: Drone icon
var icon = new TextBlock
{
    Text = "\uE707", // Segoe MDL2 Assets airplane icon
    FontFamily = new FontFamily("Segoe MDL2 Assets"),
    FontSize = 20,
    Foreground = Primary
};
```

### Implementation Steps for Priority 3

1. **Phase 1: Color Scheme Update**
   - Apply new color palette
   - Update all existing UI elements ✅ Quick visual improvement

2. **Phase 2: Card Redesign**
   - Convert drone list items to card layout
   - Add shadows and rounded corners
   - Implement hover effects ⚠️ Moderate effort

3. **Phase 3: Status Indicators**
   - Add battery gauge
   - Add armed/disarmed animated indicator
   - Add mode badge ✅ High visual impact

4. **Phase 4: Animations & Transitions**
   - Add expand/collapse animations
   - Smooth scrolling
   - Button press feedback ⚠️ Polish - lower priority

5. **Phase 5: Icon Integration**
   - Replace text with icons where appropriate
   - Add drone type icons
   - Add command icons ✅ Professional appearance

---

## Recommended Implementation Order

### Sprint 1 (High Impact, Quick Wins)
1. ✅ Better waypoint icons & labels (P1 Phase 1)
2. ✅ Basic command infrastructure (P2 Phase 1)
3. ✅ Color scheme update (P3 Phase 1)
4. ✅ Mode dropdown (P2 Phase 3)

### Sprint 2 (Core Functionality)
5. ⚠️ Segment-based route lines (P1 Phase 2)
6. ⚠️ ARM/DISARM control (P2 Phase 2)
7. ⚠️ Card redesign (P3 Phase 2)
8. ✅ Enhanced loiter circles (P1 Phase 3)

### Sprint 3 (Polish & Advanced Features)
9. ⚠️ Quick action buttons (P2 Phase 4)
10. ✅ Status indicators (P3 Phase 3)
11. ⚠️ Command acknowledgment (P2 Phase 5)
12. ⚠️ Animations (P3 Phase 4)

### Sprint 4 (Stretch Goals)
13. ⚠️ Altitude profile view (P1 Phase 4)
14. ✅ Icon integration (P3 Phase 5)

**Legend:**
- ✅ = Easy/Quick win
- ⚠️ = Moderate effort or requires testing
- 🔴 = Complex or safety-critical

---

## Technical Considerations

### CoT Limitations
- WinTAK primarily uses CoT XML for visualization
- Limited custom rendering capabilities
- Cannot create truly custom UI elements on map
- Animation on map is limited

### MAVLink Command Safety
- Always confirm ARM operations
- Validate altitude inputs (min/max)
- Check for mode compatibility with vehicle type
- Implement command timeout and retry logic
- Listen for COMMAND_ACK responses

### Testing Requirements
- Test all commands with SITL before real hardware
- Verify mode transitions work for both copter and plane
- Test command rejection scenarios
- Validate safety checks prevent accidental arming

### Performance
- Limit CoT message rate to avoid flooding WinTAK
- Batch segment updates when mission progress changes
- Use efficient DroneState tracking (already implemented)
- Debounce UI updates (current 2-second timer is good)
