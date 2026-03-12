using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentManagerPlugin.Services;
using AgentManagerPlugin.Views;

namespace AgentManagerPlugin.DockPanes
{
    [Export(typeof(WinTak.Framework.Docking.DockPane))]
    [WinTak.Framework.Docking.Attributes.DockPane(Id, "Agent Manager", Content = typeof(AgentManagerView))]
    internal class AgentManagerDockPane : WinTak.Framework.Docking.DockPane
    {
        internal const string Id = "AgentManagerDockPane";

        [ImportingConstructor]
        public AgentManagerDockPane()
        {
        }
    }

    [Export(typeof(AgentManagerView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class AgentManagerView : UserControl
    {
        // Single MAVLink connection
        private MavlinkConnectionManager _mavlinkManager;

        // Map service for CoT markers
        private DroneMapService _mapService;

        // UI Controls
        private TextBlock statusLabel;
        private Button connectButton;
        private Button refreshMissionsButton;
        private StackPanel dronesPanel;
        private TextBox portTextBox;
        private TextBlock debugOutput;
        private TextBlock onlineCountLabel;
        private TextBlock totalCountLabel;
        private System.Windows.Threading.DispatcherTimer refreshTimer;
        private System.Windows.Threading.DispatcherTimer mapUpdateTimer;

        // Modern color palette (muted, professional colors)
        private static SolidColorBrush BackgroundColor = new SolidColorBrush(Color.FromRgb(18, 18, 18));
        private static SolidColorBrush CardBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        private static SolidColorBrush CardHover = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        private static SolidColorBrush Primary = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // Modern dark slate blue
        private static SolidColorBrush Success = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
        private static SolidColorBrush Warning = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
        private static SolidColorBrush Danger = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        private static SolidColorBrush TextPrimary = Brushes.White;
        private static SolidColorBrush TextSecondary = new SolidColorBrush(Color.FromRgb(158, 158, 158));
        private static SolidColorBrush Divider = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        private static SolidColorBrush InputBackground = new SolidColorBrush(Color.FromRgb(40, 40, 40));

        // Legacy colors for gradual migration
        private static SolidColorBrush Blue = new SolidColorBrush(Color.FromRgb(66, 165, 245));
        private static SolidColorBrush Dark = BackgroundColor;
        private static SolidColorBrush Darker = InputBackground;
        private static SolidColorBrush MidGrey = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        private static SolidColorBrush Grey150 = TextSecondary;
        private static SolidColorBrush Grey100 = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        private static SolidColorBrush OkGreen = Success;
        private static SolidColorBrush ErrRed = Danger;
        private static SolidColorBrush Orange = Warning;
        private static SolidColorBrush White = TextPrimary;

        [ImportingConstructor]
        public AgentManagerView(DroneMapService mapService)
        {
            _mavlinkManager = new MavlinkConnectionManager();
            _mapService = mapService;

            BuildUI();
            StartRefreshTimer();
            StartMapUpdateTimer();
        }

        private void BuildUI()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = BackgroundColor
            };

            var mainStack = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // ===== HEADER =====
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 24)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left side: Title and subtitle
            var titleStack = new StackPanel();

            var title = new TextBlock
            {
                Text = "Agent Manager",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary
            };
            titleStack.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Fleet Control System",
                FontSize = 13,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(subtitle);

            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Right side: Status indicator
            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Success,
                Margin = new Thickness(0, 0, 8, 0)
            };
            statusPanel.Children.Add(statusDot);

            statusLabel = new TextBlock
            {
                Text = "TAK Network",
                Foreground = Success,
                FontSize = 13,
                FontWeight = FontWeights.Medium
            };
            statusPanel.Children.Add(statusLabel);

            Grid.SetColumn(statusPanel, 1);
            headerGrid.Children.Add(statusPanel);

            mainStack.Children.Add(headerGrid);

            // ===== CONNECTION SECTION =====
            var connectionCard = new Border
            {
                Background = CardBackground,
                BorderBrush = Divider,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var connectionStack = new StackPanel();

            // Section header with status
            var connectionHeader = new Grid();
            connectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            connectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var connectionTitle = new StackPanel { Orientation = Orientation.Horizontal };
            var connectionIcon = new TextBlock
            {
                Text = "📡",
                FontSize = 18,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            connectionTitle.Children.Add(connectionIcon);

            var connectionTitleText = new TextBlock
            {
                Text = "MAVLink Connection",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            connectionTitle.Children.Add(connectionTitleText);

            Grid.SetColumn(connectionTitle, 0);
            connectionHeader.Children.Add(connectionTitle);

            connectionStack.Children.Add(connectionHeader);

            var connectionInfo = new TextBlock
            {
                Text = "Listen for MAVLink packets from MAVProxy.\nMAVProxy should use: --out=udp:127.0.0.1:14551",
                Foreground = TextSecondary,
                FontSize = 12,
                Margin = new Thickness(0, 12, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            connectionStack.Children.Add(connectionInfo);

            // Port selection panel
            var portPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var portLabel = new TextBlock
            {
                Text = "UDP Port:",
                Foreground = TextSecondary,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            portPanel.Children.Add(portLabel);

            portTextBox = new TextBox
            {
                Text = "14551",
                Width = 80,
                Background = InputBackground,
                Foreground = TextPrimary,
                BorderBrush = Divider,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            portPanel.Children.Add(portTextBox);

            // Quick port buttons
            var port14551Btn = CreateQuickPortButton("14551");
            portPanel.Children.Add(port14551Btn);

            var port14591Btn = CreateQuickPortButton("14591");
            portPanel.Children.Add(port14591Btn);

            connectionStack.Children.Add(portPanel);

            // Buttons row
            var buttonsGrid = new Grid();
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Pixel) }); // Spacing
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Connect Button (initially disconnected state)
            connectButton = new Button
            {
                Content = "📡 Connect to MAVLink",
                Background = InputBackground,
                Foreground = TextPrimary,
                BorderBrush = Divider,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 0, 12),
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            connectButton.Click += ConnectButton_Click;
            Grid.SetColumn(connectButton, 0);
            buttonsGrid.Children.Add(connectButton);

            // Refresh Missions Button
            refreshMissionsButton = new Button
            {
                Content = "🔄 Refresh Missions",
                Background = InputBackground,
                Foreground = TextPrimary,
                BorderBrush = Divider,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 0, 12),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                IsEnabled = false
            };
            refreshMissionsButton.Click += RefreshMissionsButton_Click;
            Grid.SetColumn(refreshMissionsButton, 2);
            buttonsGrid.Children.Add(refreshMissionsButton);

            connectionStack.Children.Add(buttonsGrid);

            connectionCard.Child = connectionStack;
            mainStack.Children.Add(connectionCard);

            // ===== DRONES SECTION =====
            var dronesHeader = new Grid
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            dronesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dronesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dronesTitle = new TextBlock
            {
                Text = "Active Drones",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary
            };
            Grid.SetColumn(dronesTitle, 0);
            dronesHeader.Children.Add(dronesTitle);

            // Drone counter (right side)
            var droneCounter = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            onlineCountLabel = new TextBlock
            {
                Text = "0 online",
                Foreground = Success,
                FontSize = 13,
                FontWeight = FontWeights.Medium
            };
            droneCounter.Children.Add(onlineCountLabel);

            var separator = new TextBlock
            {
                Text = " | ",
                Foreground = TextSecondary,
                FontSize = 13,
                Margin = new Thickness(8, 0, 8, 0)
            };
            droneCounter.Children.Add(separator);

            totalCountLabel = new TextBlock
            {
                Text = "0 total",
                Foreground = TextSecondary,
                FontSize = 13
            };
            droneCounter.Children.Add(totalCountLabel);

            Grid.SetColumn(droneCounter, 1);
            dronesHeader.Children.Add(droneCounter);

            mainStack.Children.Add(dronesHeader);

            dronesPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            mainStack.Children.Add(dronesPanel);

            scrollViewer.Content = mainStack;
            Content = scrollViewer;
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = White,
                Margin = new Thickness(0, 24, 0, 12)
            };
        }

        private Button CreateQuickPortButton(string port)
        {
            var btn = new Button
            {
                Content = port,
                Background = Primary,
                Foreground = TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) => portTextBox.Text = port;
            return btn;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse port from TextBox
                if (!int.TryParse(portTextBox.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port number (1-65535)", "Invalid Port", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                statusLabel.Text = $"Connecting to port {port}...";
                statusLabel.Foreground = Orange;
                connectButton.IsEnabled = false;
                portTextBox.IsEnabled = false;

                // Use listen mode by default - MAVProxy should use: --out=udp:127.0.0.1:14551
                await _mavlinkManager.ConnectAsync(port, listenMode: true);

                statusLabel.Text = $"Connected (Port: {port}) - Pkts: {_mavlinkManager.PacketsReceived} Msgs: {_mavlinkManager.MessagesProcessed}";
                statusLabel.Foreground = OkGreen;
                connectButton.Content = "✅ Connected";
                connectButton.Background = OkGreen;
                refreshMissionsButton.IsEnabled = true;

                RefreshDronesList();

                // Show connection stats in UI
                System.Windows.Threading.DispatcherTimer statsTimer = new System.Windows.Threading.DispatcherTimer();
                statsTimer.Interval = TimeSpan.FromSeconds(2);
                statsTimer.Tick += (s, ev) => {
                    if (_mavlinkManager != null)
                    {
                        statusLabel.Text = $"Connected (Port: {port}) - Pkts: {_mavlinkManager.PacketsReceived} Msgs: {_mavlinkManager.MessagesProcessed}";
                    }
                };
                statsTimer.Start();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.Foreground = ErrRed;
                connectButton.IsEnabled = true;
                portTextBox.IsEnabled = true;
                MessageBox.Show($"Failed to connect:\n{ex.Message}\n\nTry a different port (14551, 14591) or close other GCS programs.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshMissionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                statusLabel.Text = "Refreshing missions...";
                statusLabel.Foreground = Orange;

                var drones = _mavlinkManager.GetAllDrones();
                foreach (var drone in drones)
                {
                    // Clear existing mission state so it can be re-requested
                    _mavlinkManager.ClearMissionState(drone.SystemId);
                    _mapService.ClearMissionMarkers(drone.SystemId);

                    // Request mission from drone
                    await _mavlinkManager.RequestMissionForDrone(drone.SystemId);
                }

                await System.Threading.Tasks.Task.Delay(2000); // Wait for responses

                statusLabel.Text = $"Missions refreshed ({drones.Count} drones)";
                statusLabel.Foreground = OkGreen;

                RefreshDronesList();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.Foreground = ErrRed;
            }
        }

        private void RefreshDronesList()
        {
            dronesPanel.Children.Clear();

            var drones = _mavlinkManager.GetAllDrones();

            // Update drone counters
            int totalDrones = drones.Count;
            int onlineDrones = drones.Where(d => d.IsAlive()).Count();

            if (onlineCountLabel != null)
            {
                onlineCountLabel.Text = $"{onlineDrones} online";
            }
            if (totalCountLabel != null)
            {
                totalCountLabel.Text = $"{totalDrones} total";
            }

            if (drones.Count == 0)
            {
                var noDrones = new TextBlock
                {
                    Text = $"No drones detected.\n\nPackets received: {_mavlinkManager.PacketsReceived}\nMessages processed: {_mavlinkManager.MessagesProcessed}\n\nMake sure MAVProxy is sending to port {_mavlinkManager.Port}.\n\nNOTE: Mission uploads from QGC may not appear because MAVProxy --out=udp is ONE-WAY.\nFor mission support, connect plugin directly to SITL TCP ports (5760+) or use QGC for missions.",
                    Foreground = Grey100,
                    FontSize = 11,
                    Margin = new Thickness(0, 8, 0, 8),
                    TextWrapping = TextWrapping.Wrap
                };
                dronesPanel.Children.Add(noDrones);
                return;
            }

            foreach (var drone in drones)
            {
                dronesPanel.Children.Add(CreateDroneCard(drone));
            }
        }

        private string GetVehicleIcon(DroneState drone)
        {
            // Return appropriate icon based on vehicle type
            switch (drone.VehicleType)
            {
                case MAVLink.MAV_TYPE.FIXED_WING:
                    return "✈"; // Plane icon
                case MAVLink.MAV_TYPE.QUADROTOR:
                    return "✚"; // Quadcopter icon (using + as placeholder)
                case MAVLink.MAV_TYPE.VTOL_DUOROTOR:
                case MAVLink.MAV_TYPE.VTOL_QUADROTOR:
                case MAVLink.MAV_TYPE.VTOL_TILTROTOR:
                    return "⇅"; // VTOL icon
                default:
                    return "◈"; // Generic drone icon
            }
        }

        private string GetVehicleTypeName(DroneState drone)
        {
            switch (drone.VehicleType)
            {
                case MAVLink.MAV_TYPE.QUADROTOR:
                    return "Quadcopter";
                case MAVLink.MAV_TYPE.VTOL_DUOROTOR:
                case MAVLink.MAV_TYPE.VTOL_QUADROTOR:
                case MAVLink.MAV_TYPE.VTOL_TILTROTOR:
                case MAVLink.MAV_TYPE.FIXED_WING: // Treat as VTOL (should be overridden in HEARTBEAT handler)
                    return "VTOL";
                default:
                    return "Unknown";
            }
        }

        private string GetDroneName(DroneState drone)
        {
            // Naming convention:
            // - Quadcopters: "Hammer {sys_id}"
            // - VTOLs: "Longbow_{sys_id}"
            // - Fixed Wing: "Recon Alpha" (or similar)
            // TODO: Make these configurable in settings

            switch (drone.VehicleType)
            {
                case MAVLink.MAV_TYPE.QUADROTOR:
                    return $"Hammer {drone.SystemId}";

                case MAVLink.MAV_TYPE.VTOL_DUOROTOR:
                case MAVLink.MAV_TYPE.VTOL_QUADROTOR:
                case MAVLink.MAV_TYPE.VTOL_TILTROTOR:
                    return $"Longbow_{drone.SystemId}";

                case MAVLink.MAV_TYPE.FIXED_WING:
                    return $"Recon Alpha {drone.SystemId}";

                default:
                    return $"Drone {drone.SystemId}";
            }
        }

        private Border CreateDroneCard(DroneState drone)
        {
            var card = new Border
            {
                Background = CardBackground,
                BorderBrush = Divider,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Add hover effect
            card.MouseEnter += (s, e) => card.Background = CardHover;
            card.MouseLeave += (s, e) => card.Background = CardBackground;

            var grid = new Grid();

            // Define columns: Left (Icon+Name+Armed+SysID) | Battery Icon | Mode (always visible)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 }); // Left: Name info (reduced from 150)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Battery icon only
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 110 }); // Mode dropdown (PRIORITY - always visible)

            // LEFT SIDE: Icon + Name + Status
            var leftPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Vehicle Icon
            var iconBorder = new Border
            {
                Width = 40,
                Height = 40,
                Background = InputBackground,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 16, 0)
            };
            var icon = new TextBlock
            {
                Text = GetVehicleIcon(drone),
                FontSize = 20,
                Foreground = Primary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = icon;
            leftPanel.Children.Add(iconBorder);

            // Name and subtitle
            var nameStack = new StackPanel();

            var namePanel = new StackPanel { Orientation = Orientation.Horizontal };

            var droneName = new TextBlock
            {
                Text = GetDroneName(drone),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary
            };
            namePanel.Children.Add(droneName);

            // Status dot (online/offline)
            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = drone.IsAlive() ? Success : TextSecondary,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            namePanel.Children.Add(statusDot);

            // Armed status indicator
            var armedIndicator = new TextBlock
            {
                Text = drone.Armed ? "🟢" : "🔴",
                FontSize = 10,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = drone.Armed ? "Armed" : "Disarmed"
            };
            namePanel.Children.Add(armedIndicator);

            nameStack.Children.Add(namePanel);

            var subtitle = new TextBlock
            {
                Text = $"{GetVehicleTypeName(drone)} | SYS ID: {drone.SystemId}",
                FontSize = 12,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 2, 0, 0)
            };
            nameStack.Children.Add(subtitle);

            leftPanel.Children.Add(nameStack);
            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);

            // COLUMN 1: BATTERY ICON ONLY (no percentage text)
            if (drone.Battery != null)
            {
                var batteryColor = drone.Battery.Percentage < 20 ? Danger :
                                  drone.Battery.Percentage < 50 ? Warning : Success;

                var batteryIcon = new TextBlock
                {
                    Text = "🔋",
                    FontSize = 16,
                    Foreground = batteryColor,
                    Margin = new Thickness(12, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"Battery: {drone.Battery.Percentage}%" // Show percentage in tooltip
                };

                Grid.SetColumn(batteryIcon, 1);
                grid.Children.Add(batteryIcon);
            }

            // COLUMN 2: MODE DROPDOWN (PRIORITY - always visible)
            if (!string.IsNullOrEmpty(drone.FlightMode))
            {
                var modeComboBox = new System.Windows.Controls.ComboBox
                {
                    MinWidth = 100,
                    MaxWidth = 130,
                    Background = InputBackground,
                    BorderBrush = Divider,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 13,
                    Foreground = TextPrimary,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Populate modes based on vehicle type
                var isVTOL = drone.VehicleType == MAVLink.MAV_TYPE.VTOL_DUOROTOR ||
                            drone.VehicleType == MAVLink.MAV_TYPE.VTOL_QUADROTOR ||
                            drone.VehicleType == MAVLink.MAV_TYPE.VTOL_TILTROTOR ||
                            drone.VehicleType == MAVLink.MAV_TYPE.FIXED_WING;

                var modes = isVTOL
                    ? new[] { "QSTABILIZE", "QHOVER", "QLOITER", "QRTL", "AUTO", "RTL", "GUIDED", "FBWA", "FBWB", "CRUISE" }
                    : new[] { "STABILIZE", "ALT_HOLD", "LOITER", "RTL", "AUTO", "GUIDED", "LAND", "BRAKE" };

                foreach (var mode in modes)
                {
                    modeComboBox.Items.Add(mode);
                }

                // Set current mode as selected
                modeComboBox.SelectedItem = drone.FlightMode;

                // Wire up mode change event
                var droneId = drone.SystemId;
                modeComboBox.SelectionChanged += (s, e) =>
                {
                    if (modeComboBox.SelectedItem != null && _mavlinkManager != null)
                    {
                        var newMode = modeComboBox.SelectedItem.ToString();
                        System.Diagnostics.Debug.WriteLine($"User selected mode: {newMode} for Drone {droneId}");
                        _mavlinkManager.SetMode(droneId, newMode);
                    }
                };

                Grid.SetColumn(modeComboBox, 2);
                grid.Children.Add(modeComboBox);
            }

            card.Child = grid;
            return card;
        }

        private void ShowMissionPlot(DroneState drone)
        {
            try
            {
                if (drone.Waypoints == null || drone.Waypoints.Count == 0)
                {
                    MessageBox.Show("No mission waypoints available for this drone.\n\nClick 'Refresh All Missions' to download missions from all drones.", "No Mission", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var plotWindow = new MissionPlotWindow($"Drone {drone.SystemId}", drone.Waypoints);
                plotWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show mission plot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartRefreshTimer()
        {
            refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            refreshTimer.Tick += (s, e) => RefreshDronesList();
            refreshTimer.Start();
        }

        private void StartMapUpdateTimer()
        {
            // Update map markers every 1 second
            mapUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            mapUpdateTimer.Tick += (s, e) => UpdateMapMarkers();
            mapUpdateTimer.Start();
        }

        private void UpdateMapMarkers()
        {
            try
            {
                if (_mavlinkManager == null || _mapService == null) return;

                var drones = _mavlinkManager.GetAllDrones();
                foreach (var drone in drones)
                {
                    // Update drone position marker
                    if (drone.IsAlive() && drone.Position != null)
                    {
                        _mapService.UpdateDroneMarker(drone);
                    }

                    // Draw mission waypoints if available
                    if (drone.Waypoints != null && drone.Waypoints.Count > 0)
                    {
                        _mapService.DrawMission(drone.SystemId, drone.Waypoints);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating map markers: {ex.Message}");
            }
        }
    }
}
