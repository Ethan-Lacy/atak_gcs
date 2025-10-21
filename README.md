# WinTAK Simple Plugin

A minimal WinTAK plugin demonstrating basic plugin development with a dockable pane and ribbon button.

## Features

- Ribbon button in the Plugins tab
- Dockable pane that displays on the right side
- Test button to verify plugin functionality

## Prerequisites

- **WinTAK 5.5.0.157** installed at `C:\Program Files\WinTAK\`
- **Visual Studio 2022** with .NET Framework 4.8 development tools
- **MSBuild** (included with Visual Studio)

## Building

### Option 1: Visual Studio GUI
1. Open `WinTAK Plugin (5.0)_voice.sln` in Visual Studio 2022
2. Select **x64** platform and **Debug** or **Release** configuration
3. Build > Build Solution (or press `Ctrl+Shift+B`)
4. The plugin DLL will automatically copy to `%appdata%\wintak\plugins\SimpleWinTAKPlugin\`

### Option 2: Command Line (MSBuild)

**Standard Build:**
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "WinTAK Plugin (5.0)_voice/WinTAK Plugin (5.0)_voice.csproj" /p:Configuration=Debug /p:Platform=x64
```

**Clean Build (recommended after code changes):**
```bash
# First clean
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "WinTAK Plugin (5.0)_voice/WinTAK Plugin (5.0)_voice.csproj" /t:Clean /p:Configuration=Debug /p:Platform=x64

# Then rebuild
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "WinTAK Plugin (5.0)_voice/WinTAK Plugin (5.0)_voice.csproj" /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

**If build fails with "Sharing violation":**
- Close WinTAK completely
- Rebuild the project
- The post-build event will copy the new DLL to the plugin folder

## Testing

1. Start WinTAK
2. Look for **"Simple Plugin"** button in the **Plugins** ribbon tab
3. Click the button to open the dock pane on the right side
4. Click the **"Test Button"** to verify functionality

## Troubleshooting

### Plugin doesn't appear in WinTAK
- Check that DLL exists at: `%appdata%\wintak\plugins\SimpleWinTAKPlugin\WinTAK Plugin (5.0)_voice.dll`
- Verify WinTAK version matches (5.5.0.157)
- Check WinTAK logs for loading errors

### Build fails with package restore errors
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "WinTAK Plugin (5.0)_voice/WinTAK Plugin (5.0)_voice.csproj" /t:Restore
```

### ArgumentException with 'Name' property
- Ensure plugin IDs don't contain dots/periods
- Button and DockPane names must be simple alphanumeric strings

## Structure

- **Module.cs** - Main plugin entry point implementing `IModule` and `ITakModule`
- **DockPanes/DockPane.cs** - Dockable pane UI and view
- **Buttons/Button.cs** - Ribbon button that activates the dock pane

## References

Based on: https://github.com/Cale-Torino/WinTAK_Simple_Usage_Plugin
