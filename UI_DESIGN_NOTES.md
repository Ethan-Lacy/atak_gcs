# Voice Plugin UI Design - Aesthetic Improvements

## Color Palette
```csharp
// Dark theme colors
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

## Main Container
- **ScrollViewer** background: Dark (RGB 30, 30, 30)
- **StackPanel** margin: 16px all around

## Header Design
- Horizontal StackPanel layout
- **Title**: "Voice Control"
  - FontSize: 20
  - FontWeight: Bold
  - Foreground: White
- **Status Dot** (Ellipse):
  - Width/Height: 8px
  - Fill: Blue (RGB 70, 130, 200)
  - Margin: 10px left, 8px right
- **Status Label**: "READY"
  - FontSize: 12
  - FontWeight: Bold
  - Foreground: Blue (changes based on state)
  - States:
    - Ready: Blue
    - Recording: ErrRed (255, 50, 50)
    - Processing: Orange-ish (255, 200, 0)

## Control Buttons Layout (Grid)
Two columns:
- Column 1: Star width (main button)
- Column 2: 48px fixed (secondary button)

### Record/Stop Toggle Button
- **Content**: "● REC" / "■ STOP"
- Height: 56px
- FontSize: 16
- FontWeight: Bold
- Foreground: White
- Background: Blue (normal) / ErrRed (recording)
- BorderThickness: 0
- Margin: 0, 0, 4, 0 (4px right spacing)

### Playback Button
- **Content**: "▶" (play symbol)
- Height: 56px
- Width: 48px
- FontSize: 20
- Background: Darker (RGB 40, 40, 40)
- Foreground: White
- BorderThickness: 0

## Transcription Display
- **Title**: "Transcription"
  - FontSize: 13
  - FontWeight: Bold
  - Foreground: Grey150
  - Margin: 12px top, 6px bottom

### Transcription Container (Border)
- Background: Darker (RGB 40, 40, 40)
- BorderBrush: MidGrey (RGB 60, 60, 60)
- BorderThickness: 1
- CornerRadius: 4
- Padding: 12
- Margin: 0, 0, 0, 12 (12px bottom)

### Transcription Text
- TextWrapping: Wrap
- FontSize: 14
- Foreground: White (normal) / OkGreen (sent) / ErrRed (recording) / Orange (no speech)
- MinHeight: 60px

### Character Count
- FontSize: 11
- Foreground: Grey100
- Margin: 6px top

## Send Button
- **Content**: "SEND TO CHAT"
- Height: 48px
- FontSize: 14
- FontWeight: Bold
- Background: OkGreen (RGB 0, 255, 100) when enabled / MidGrey when disabled
- Foreground: Dark (RGB 30, 30, 30)
- BorderThickness: 0
- CornerRadius: 4

## Incoming Messages Section
- **Title**: "Incoming Messages"
  - FontSize: 13
  - FontWeight: Bold
  - Foreground: Grey150
  - Margin: 20px top, 8px bottom

### Messages Container (Border + ScrollViewer)
- Border:
  - Background: Darker (RGB 40, 40, 40)
  - BorderBrush: MidGrey (RGB 60, 60, 60)
  - BorderThickness: 1
  - CornerRadius: 4
  - Padding: 8
- ScrollViewer:
  - MaxHeight: 300px
  - VerticalScrollBarVisibility: Auto

### Individual Message Layout
- StackPanel per message
- Margin: 0, 0, 0, 8 (8px bottom spacing)
- **Sender name**:
  - FontWeight: Bold
  - FontSize: 11
  - Foreground: Blue (RGB 70, 130, 200)
- **Timestamp**:
  - FontSize: 10
  - Foreground: Grey100
- **Message text**:
  - TextWrapping: Wrap
  - FontSize: 12
  - Foreground: Light grey (RGB 200, 200, 200)
- **Separator line** (Rectangle):
  - Height: 1
  - Fill: MidGrey
  - Margin: 8px top

## Key Design Principles
1. **Dark theme** throughout - matches tactical software aesthetic
2. **Clear visual hierarchy** - larger titles, grouped sections
3. **State indication** - color-coded status (Blue=ready, Red=recording, Green=success)
4. **Rounded corners** on interactive elements (4px radius)
5. **Consistent spacing** - 8px, 12px, 16px, 20px increments
6. **High contrast** - white text on dark backgrounds for readability
7. **Tactical feel** - monochrome with accent colors (blue/green/red)
