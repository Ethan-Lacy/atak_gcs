# WinTAK Plugin Development Guide

> **Comprehensive reference based on successful voice-to-chat plugin implementation**
> Last Updated: January 2025 | WinTAK SDK 5.3

---

## Table of Contents

1. [Project Configuration](#project-configuration)
2. [Core Plugin Architecture](#core-plugin-architecture)
3. [MEF Dependency Injection](#mef-dependency-injection)
4. [CoT Message Handling](#cot-message-handling)
5. [UI Design Patterns](#ui-design-patterns)
6. [Service Layer Architecture](#service-layer-architecture)
7. [External Dependencies](#external-dependencies)
8. [Best Practices](#best-practices)
9. [Common Pitfalls](#common-pitfalls)
10. [Code Templates](#code-templates)
11. [Resources](#resources)

---

## Project Configuration

### **Essential Setup**
```xml
<PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <Platform>x64</Platform>
    <LangVersion>7.3</LangVersion>
    <OutputType>Library</OutputType>
</PropertyGroup>
```

### **Required NuGet Packages**
```xml
<PackageReference Include="WinTak-Dependencies" Version="5.0.0.169" />
<PackageReference Include="Prism.Mef" Version="6.3.1" />
```

### **Required Assemblies**
```xml
<Reference Include="System.ComponentModel.Composition" />
<Reference Include="PresentationCore" />
<Reference Include="PresentationFramework" />
<Reference Include="WindowsBase" />
<Reference Include="System.Xaml" />
```

### **Post-Build Deployment** (Automatic)
```xml
<PostBuildEvent>
xcopy "$(TargetDir)$(TargetFileName)" "%appdata%\wintak\plugins\YourPlugin\" /y
xcopy "$(TargetDir)$(TargetName).pdb" "%appdata%\wintak\plugins\YourPlugin\" /y
</PostBuildEvent>
```

**Deploy Location**: `%appdata%\wintak\plugins\YourPluginName\`

---

## Core Plugin Architecture

### **File Structure**
```
YourPlugin/
├── YourPlugin.csproj
├── Module.cs                    # Entry point (IModule + ITakModule)
├── Buttons/
│   └── YourButton.cs           # Ribbon button
├── DockPanes/
│   ├── YourDockPane.cs         # Dock pane metadata
│   └── YourView.cs             # UserControl (UI)
├── Services/
│   ├── IYourService.cs         # Service interfaces
│   └── YourServiceImpl.cs      # Service implementations
└── Assets/
    └── Icon_32x32.png          # Button icons
```

---

## Core Plugin Components

### **1. Module.cs - Entry Point**

```csharp
using Prism.Mef.Modularity;
using Prism.Modularity;
using System.ComponentModel.Composition;

namespace YourPlugin
{
    [ModuleExport(typeof(YourModule), InitializationMode = InitializationMode.WhenAvailable)]
    internal class YourModule : IModule, WinTak.Framework.ITakModule
    {
        private readonly WinTak.Framework.Messaging.IMessageHub _messageHub;
        private readonly WinTak.Framework.Docking.IDockingManager _dockingManager;

        [ImportingConstructor]
        public YourModule(
            WinTak.Framework.Messaging.IMessageHub messageHub,
            WinTak.Framework.Docking.IDockingManager dockingManager)
        {
            _messageHub = messageHub;
            _dockingManager = dockingManager;
        }

        // Called during WinTAK startup
        public void Initialize()
        {
            // Subscribe to messages here
            _messageHub.Subscribe<WinTak.Common.Messaging.ClearContentMessage>(OnClearContent);
        }

        // Called after splash screen closes
        public void Startup()
        {
            // AVOID activating dock panes here due to MEF timing issues
            // Let ribbon buttons activate panes instead
        }

        // Called during WinTAK shutdown
        public void Terminate()
        {
            // Cleanup resources
        }

        private void OnClearContent(WinTak.Common.Messaging.ClearContentMessage args)
        {
            // Handle clear content request
        }
    }
}
```

**Key Points:**
- Implements both `IModule` and `WinTak.Framework.ITakModule`
- Use `[ImportingConstructor]` for dependency injection
- Three lifecycle methods: `Initialize()`, `Startup()`, `Terminate()`
- **Warning**: Don't activate dock panes in `Startup()` (MEF timing issues)

---

### **2. Ribbon Button**

```csharp
using System.ComponentModel.Composition;

namespace YourPlugin.Buttons
{
    [Export(typeof(WinTak.Framework.Tools.Button))]
    [WinTak.Framework.Tools.Attributes.Button("YourButtonId", "Button Label")]
    internal class YourButton : WinTak.Framework.Tools.Button
    {
        private readonly WinTak.Framework.Docking.IDockingManager _dockingManager;

        [ImportingConstructor]
        public YourButton(WinTak.Framework.Docking.IDockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        protected override void OnClick()
        {
            base.OnClick();

            var pane = _dockingManager.GetDockPane(DockPanes.YourDockPane.Id);
            if (pane != null)
                pane.Activate();
        }
    }
}
```

**Critical**: Use **simple alphanumeric IDs** (no dots/periods) to avoid `ArgumentException`

---

### **3. Dock Pane + View**

```csharp
using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace YourPlugin.DockPanes
{
    // Dock Pane Metadata
    [Export(typeof(WinTak.Framework.Docking.DockPane))]
    [WinTak.Framework.Docking.Attributes.DockPane(Id, "Your Title", Content = typeof(YourView))]
    internal class YourDockPane : WinTak.Framework.Docking.DockPane
    {
        internal const string Id = "YourDockPaneId"; // Simple alphanumeric!
    }

    // View (UserControl)
    [Export(typeof(YourView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class YourView : UserControl
    {
        private readonly WinTak.Framework.Messaging.IMessageHub _messageHub;

        [ImportingConstructor]
        public YourView(WinTak.Framework.Messaging.IMessageHub messageHub)
        {
            _messageHub = messageHub;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Build UI programmatically or load XAML
            var stackPanel = new StackPanel();
            Content = stackPanel;
        }
    }
}
```

---

## MEF Dependency Injection

### **Available WinTAK Services**

| Interface | Purpose | Common Uses |
|-----------|---------|-------------|
| `IMessageHub` | Pub/sub messaging | Subscribe to system messages |
| `IDockingManager` | Manage dock panes | Activate/deactivate panes |
| `ICommunicationService` | Send CoT to network | `BroadcastCot()`, `SendCot()` |
| `ICotMessageSender` | Internal CoT processing | Process CoT without network send |
| `IDevicePreferences` | User settings | Get callsign, preferences |
| `ILocationService` | GPS/position data | Get user position, UID |
| `ILogger` | Logging framework | Debug, Info, Warn, Error, Fatal |
| `IEventAggregator` | Event aggregation | Publish/subscribe events |
| `MapViewControl` | Map interactions | Mouse events, selection |

### **Injection Patterns**

**Constructor Injection** (Preferred):
```csharp
[ImportingConstructor]
public MyClass(IMessageHub messageHub, ICommunicationService commService)
{
    _messageHub = messageHub;
    _commService = commService;
}
```

**Property Injection** (Optional services):
```csharp
[Import]
public IOptionalService OptionalService { get; set; } // Allows recomposition
```

**Exporting Services**:
```csharp
[Export(typeof(IMyService))]
public class MyServiceImpl : IMyService { }
```

---

## CoT Message Handling

### **GeoChat Message Format** (Working Example)

```csharp
private void SendChatMessage(string chatRoom, string message, string callsign, string senderUid)
{
    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    string chatId = $"GeoChat.{senderUid}.{chatRoom}.{DateTime.UtcNow.Ticks}";

    string cotXml = $@"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<event version='2.0' uid='{chatId}' type='b-t-f' time='{timestamp}' start='{timestamp}' stale='{DateTime.UtcNow.AddHours(1):yyyy-MM-ddTHH:mm:ss.fffZ}' how='h-g-i-g-o'>
    <point lat='0.0' lon='0.0' hae='9999999.0' ce='9999999.0' le='9999999.0' />
    <detail>
        <__chat id='{chatRoom}' chatroom='{chatRoom}' senderCallsign='{System.Security.SecurityElement.Escape(callsign)}' groupOwner='false'>
            <chatgrp id='{chatRoom}' uid0='{senderUid}' />
        </__chat>
        <link uid='{senderUid}' type='a-f-G-E-V-A' relation='p-p' />
        <remarks source='YourPlugin' to='{chatRoom}' time='{timestamp}'>
            {System.Security.SecurityElement.Escape(message)}
        </remarks>
    </detail>
</event>";

    var cotDoc = new System.Xml.XmlDocument();
    cotDoc.LoadXml(cotXml);
    _communicationService.BroadcastCot(cotDoc); // Sends to network
}
```

### **Getting User Identity**

```csharp
[ImportingConstructor]
public MyView(IDevicePreferences devicePreferences, ILocationService locationService)
{
    // Get callsign
    string callsign = devicePreferences.Callsign ?? "Unknown";

    // Get UID from GPS object
    string uid = null;
    try
    {
        var gpsObject = locationService.GetGpsObject();
        if (gpsObject != null)
            uid = gpsObject.Uid;
    }
    catch { }

    // Fallback UID
    if (string.IsNullOrEmpty(uid))
        uid = $"{callsign}-{Environment.MachineName}";
}
```

### **CoT Message Structure**

**Event Attributes:**
- `version`: Always "2.0"
- `uid`: Unique identifier (format varies by type)
- `type`: CoT type (e.g., `b-t-f` = broadcast chat, `a-f-G` = friendly)
- `time`: Event timestamp (ISO 8601 UTC)
- `start`: Start time (usually same as `time`)
- `stale`: Expiration time (future timestamp)
- `how`: How derived (e.g., `h-g-i-g-o` = human-GPS-input)

**Point Element:**
- `lat`, `lon`: Coordinates (decimal degrees)
- `hae`: Height above ellipsoid (meters)
- `ce`, `le`: Circular/linear error (meters)

**Detail Element:**
- Contains type-specific data (chat, contacts, markers, etc.)
- Use `System.Security.SecurityElement.Escape()` for user input!

### **Sending Methods**

**Broadcast to all nodes:**
```csharp
_communicationService.BroadcastCot(xmlDocument);
```

**Send to specific contact:**
```csharp
_communicationService.SendCot(xmlDocument, contactUid);
```

**Internal processing only (no network):**
```csharp
_cotMessageSender.Process(cotEvent);
```

---

## UI Design Patterns

### **Tactical Dark Theme** (Proven Color Palette)

```csharp
// Color definitions
private static SolidColorBrush Blue = new SolidColorBrush(Color.FromRgb(70, 130, 200));
private static SolidColorBrush Dark = new SolidColorBrush(Color.FromRgb(30, 30, 30));
private static SolidColorBrush Darker = new SolidColorBrush(Color.FromRgb(40, 40, 40));
private static SolidColorBrush MidGrey = new SolidColorBrush(Color.FromRgb(60, 60, 60));
private static SolidColorBrush Grey150 = new SolidColorBrush(Color.FromRgb(150, 150, 150));
private static SolidColorBrush Grey100 = new SolidColorBrush(Color.FromRgb(100, 100, 100));
private static SolidColorBrush OkGreen = new SolidColorBrush(Color.FromRgb(0, 255, 100));
private static SolidColorBrush ErrRed = new SolidColorBrush(Color.FromRgb(255, 50, 50));
private static SolidColorBrush White = Brushes.White;
```

**Usage:**
- **Blue**: Primary actions, active status
- **OkGreen**: Success states, confirmations
- **ErrRed**: Errors, warnings, recording states
- **Dark**: Main background
- **Darker**: Container backgrounds
- **MidGrey**: Borders, disabled elements
- **White**: Primary text

### **Layout Structure**

```csharp
var scrollViewer = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    Background = Dark
};

var stackPanel = new StackPanel
{
    Margin = new Thickness(16) // Consistent spacing
};

// Bordered container
var border = new Border
{
    Background = Darker,
    BorderBrush = MidGrey,
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(4), // Rounded corners
    Padding = new Thickness(12)
};

scrollViewer.Content = stackPanel;
Content = scrollViewer;
```

### **Spacing Guidelines**
- **4px**: Tight spacing (adjacent buttons)
- **8px**: List item spacing
- **12px**: Section padding, element margins
- **16px**: Panel margins
- **20px**: Major section separation

### **State Management**

```csharp
// Visual feedback for states
private void UpdateStatus(string text, SolidColorBrush color)
{
    statusLabel.Text = text;
    statusLabel.Foreground = color;
}

// Example states
UpdateStatus("READY", Blue);
UpdateStatus("PROCESSING", new SolidColorBrush(Color.FromRgb(255, 200, 0)));
UpdateStatus("ERROR", ErrRed);
UpdateStatus("SUCCESS", OkGreen);
```

---

## Service Layer Architecture

### **Interface-First Design**

```csharp
// Define interface
public interface IYourService
{
    Task<string> ProcessAsync(string input);
    bool IsReady { get; }
}

// Implement with MEF export
[Export(typeof(IYourService))]
public class YourServiceImpl : IYourService
{
    [ImportingConstructor]
    public YourServiceImpl()
    {
    }

    public async Task<string> ProcessAsync(string input)
    {
        return await Task.Run(() => /* process */);
    }

    public bool IsReady => true;
}
```

### **Orchestrator Pattern** (Coordinate Multiple Services)

```csharp
[Export(typeof(IWorkflowOrchestrator))]
public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IServiceA _serviceA;
    private readonly IServiceB _serviceB;
    private readonly IServiceC _serviceC;

    [ImportingConstructor]
    public WorkflowOrchestrator(IServiceA serviceA, IServiceB serviceB, IServiceC serviceC)
    {
        _serviceA = serviceA;
        _serviceB = serviceB;
        _serviceC = serviceC;
    }

    public async Task ExecuteWorkflowAsync()
    {
        var dataA = await _serviceA.FetchDataAsync();
        var dataB = await _serviceB.ProcessAsync(dataA);
        await _serviceC.SaveAsync(dataB);
    }
}
```

---

## External Dependencies

### **Adding NuGet Packages**

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="NAudio" Version="2.2.1" />
```

### **Deploying External DLLs**

```xml
<PostBuildEvent>
xcopy "$(TargetDir)$(TargetFileName)" "%appdata%\wintak\plugins\YourPlugin\" /y
xcopy "$(TargetDir)$(TargetName).pdb" "%appdata%\wintak\plugins\YourPlugin\" /y
if exist "$(TargetDir)NAudio*.dll" xcopy "$(TargetDir)NAudio*.dll" "%appdata%\wintak\plugins\YourPlugin\" /y
if exist "$(TargetDir)Newtonsoft.Json.dll" xcopy "$(TargetDir)Newtonsoft.Json.dll" "%appdata%\wintak\plugins\YourPlugin\" /y
</PostBuildEvent>
```

---

## Best Practices

### ✅ **Do's**

1. **Use Interface-Based Design**
   - Define interfaces for all services
   - Export implementations with `[Export(typeof(IInterface))]`

2. **Implement Async/Await**
   - Use `async Task` for I/O operations
   - Avoid blocking UI thread

3. **Error Handling**
   ```csharp
   try
   {
       await PerformOperationAsync();
   }
   catch (Exception ex)
   {
       _logger.Error($"Operation failed: {ex.Message}", ex);
       MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
   }
   ```

4. **Resource Cleanup**
   ```csharp
   using (var stream = File.OpenRead(path))
   {
       // Use stream
   } // Automatically disposed
   ```

5. **XML Escaping**
   ```csharp
   string safe = System.Security.SecurityElement.Escape(userInput);
   ```

6. **Use ILogger** (Instead of Debug.WriteLine)
   ```csharp
   [ImportingConstructor]
   public MyClass(ILogger logger)
   {
       _logger = logger;
   }

   _logger.Info("Operation started");
   _logger.Error("Operation failed", exception);
   ```

7. **Simple IDs**
   - Use alphanumeric IDs: `"MyDockPane"` ✅
   - Avoid dots: `"My.Dock.Pane"` ❌

### ❌ **Don'ts**

1. **Don't activate panes in `Startup()`**
   - MEF timing issues
   - Use ribbon buttons instead

2. **Don't use `AnyCPU` platform**
   - Always use **x64**

3. **Don't hardcode paths**
   - Use `Environment.GetFolderPath()`, `Path.Combine()`

4. **Don't forget NuGet restore**
   - Ensure packages restore before build

5. **Don't ignore sharing violations**
   - Close WinTAK before rebuilding

---

## Common Pitfalls

### **Problem 1: ArgumentException with 'Name' property**

**Cause**: Dock pane or button ID contains dots/periods
**Solution**: Use simple alphanumeric IDs

```csharp
// BAD
const string Id = "MyPlugin.DockPane"; // ❌

// GOOD
const string Id = "MyPluginDockPane"; // ✅
```

---

### **Problem 2: MEF Timing Issues**

**Cause**: Activating panes in `Startup()` before MEF fully initializes
**Solution**: Let ribbon buttons activate panes

```csharp
// BAD
public void Startup()
{
    var pane = _dockingManager.GetDockPane(MyDockPane.Id);
    pane.Activate(); // ❌ MEF may not be ready
}

// GOOD
public void Startup()
{
    // Don't activate here - let button do it ✅
}
```

---

### **Problem 3: Sharing Violations During Build**

**Cause**: WinTAK has plugin DLL loaded
**Solution**: Close WinTAK before rebuilding

```bash
# Clean and rebuild
taskkill /IM WinTAK.exe /F
msbuild YourPlugin.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

---

### **Problem 4: Missing Dependencies**

**Cause**: NuGet packages not restored
**Solution**: Restore before build

```bash
msbuild YourPlugin.csproj /t:Restore
msbuild YourPlugin.csproj /p:Configuration=Debug /p:Platform=x64
```

---

### **Problem 5: CoT Messages Not Appearing**

**Checklist:**
- ✅ XML format correct (use `XmlDocument.LoadXml()` to validate)
- ✅ User input escaped (`SecurityElement.Escape()`)
- ✅ Using `BroadcastCot()` (not `Process()`)
- ✅ WinTAK connected to network/server
- ✅ CoT type correct (`b-t-f` for chat)

---

## Code Templates

### **Minimal Plugin Template**

```csharp
// Module.cs
[ModuleExport(typeof(MinimalModule), InitializationMode = InitializationMode.WhenAvailable)]
internal class MinimalModule : IModule, WinTak.Framework.ITakModule
{
    [ImportingConstructor]
    public MinimalModule(IMessageHub messageHub) { }
    public void Initialize() { }
    public void Startup() { }
    public void Terminate() { }
}

// Button.cs
[Export(typeof(WinTak.Framework.Tools.Button))]
[WinTak.Framework.Tools.Attributes.Button("MinimalButton", "Minimal")]
internal class MinimalButton : WinTak.Framework.Tools.Button
{
    private readonly IDockingManager _dockingManager;
    [ImportingConstructor]
    public MinimalButton(IDockingManager dockingManager) { _dockingManager = dockingManager; }
    protected override void OnClick()
    {
        base.OnClick();
        _dockingManager.GetDockPane(MinimalDockPane.Id)?.Activate();
    }
}

// DockPane.cs
[Export(typeof(WinTak.Framework.Docking.DockPane))]
[WinTak.Framework.Docking.Attributes.DockPane(Id, "Minimal", Content = typeof(MinimalView))]
internal class MinimalDockPane : WinTak.Framework.Docking.DockPane
{
    internal const string Id = "MinimalDockPane";
}

// View.cs
[Export(typeof(MinimalView))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class MinimalView : UserControl
{
    [ImportingConstructor]
    public MinimalView()
    {
        Content = new TextBlock { Text = "Hello WinTAK!" };
    }
}
```

---

### **Service Interface Template**

```csharp
// IMyService.cs
public interface IMyService
{
    Task<string> ProcessAsync(string input);
    bool IsAvailable { get; }
    event EventHandler<string> StatusChanged;
}

// MyServiceImpl.cs
[Export(typeof(IMyService))]
public class MyServiceImpl : IMyService
{
    public event EventHandler<string> StatusChanged;
    public bool IsAvailable => true;

    [ImportingConstructor]
    public MyServiceImpl() { }

    public async Task<string> ProcessAsync(string input)
    {
        StatusChanged?.Invoke(this, "Processing...");
        await Task.Delay(100); // Simulate work
        StatusChanged?.Invoke(this, "Complete");
        return $"Processed: {input}";
    }
}
```

---

### **CoT Subscription Template**

```csharp
[ImportingConstructor]
public MyModule(IMessageHub messageHub, ICommunicationService commService)
{
    _messageHub = messageHub;
    _commService = commService;
}

public void Initialize()
{
    // Subscribe to incoming CoT messages
    _messageHub.Subscribe<WinTak.Common.CoT.CotEventMessage>(OnCotReceived);
}

private void OnCotReceived(WinTak.Common.CoT.CotEventMessage message)
{
    var cotEvent = message.Event;
    string type = cotEvent.Type;
    string uid = cotEvent.Uid;

    // Filter by type
    if (type.StartsWith("b-t-f")) // Chat message
    {
        // Process chat message
    }
}
```

---

## Resources

### **Official Documentation**
- **TAK.gov**: Official WinTAK SDK (registration required)
- **WinTAK SDK Docs**: https://iq-blue.com/WinTAK_SDK_Documentation/
- **Learn WinTAK**: https://hellikandra.github.io/LearnWinTAK/

### **Community Resources**
- **Simple Plugin Example**: https://github.com/Cale-Torino/WinTAK_Simple_Usage_Plugin
- **Plugin Template**: https://github.com/Hellikandra/WinTAK-plugintemplate
- **TAK Protocol**: https://takproto.readthedocs.io/

### **Development Tools**
- **Visual Studio 2022**: https://visualstudio.microsoft.com/
- **.NET Framework 4.8**: Included with VS 2022
- **WinTAK VSIX**: Included in WinTAK SDK

### **CoT Protocol**
- **CoT Specification**: https://cot.mitre.org/
- **TAK Protocol Docs**: https://takproto.readthedocs.io/en/latest/tak_protocols/

---

## Version History

| Date | Changes |
|------|---------|
| Jan 2025 | Initial guide based on voice-to-chat plugin |

---

## Contributing

This guide is based on real-world implementation. If you discover improvements or corrections, please update this document!

**Based on successful implementation of WinTAK Voice-to-Chat Plugin**
Architecture patterns proven in production deployment 🎯
