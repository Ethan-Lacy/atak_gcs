using System;
using System.ComponentModel.Composition;
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
        private System.Windows.Threading.DispatcherTimer refreshTimer;
        private System.Windows.Threading.DispatcherTimer mapUpdateTimer;

        // Color palette
        private static SolidColorBrush Blue = new SolidColorBrush(Color.FromRgb(70, 130, 200));
        private static SolidColorBrush Dark = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        private static SolidColorBrush Darker = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        private static SolidColorBrush MidGrey = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        private static SolidColorBrush Grey150 = new SolidColorBrush(Color.FromRgb(150, 150, 150));
        private static SolidColorBrush Grey100 = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        private static SolidColorBrush OkGreen = new SolidColorBrush(Color.FromRgb(0, 255, 100));
        private static SolidColorBrush ErrRed = new SolidColorBrush(Color.FromRgb(255, 50, 50));
        private static SolidColorBrush Orange = new SolidColorBrush(Color.FromRgb(255, 165, 0));
        private static SolidColorBrush White = Brushes.White;

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
                Background = Dark
            };

            var mainStack = new StackPanel
            {
                Margin = new Thickness(16)
            };

            // ===== HEADER =====
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var title = new TextBlock
            {
                Text = "DRONE MANAGER",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = White,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(title);

            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Grey100,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusDot);

            statusLabel = new TextBlock
            {
                Text = "Disconnected",
                Foreground = Grey150,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusLabel);

            mainStack.Children.Add(headerPanel);

            // ===== CONNECTION SECTION =====
            mainStack.Children.Add(CreateSectionHeader("MAVLink Connection"));

            var connectionInfo = new TextBlock
            {
                Text = "Connect to MAVProxy UDP output to receive telemetry from all drones",
                Foreground = Grey150,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(connectionInfo);

            // Port selection panel
            var portPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var portLabel = new TextBlock
            {
                Text = "UDP Port:",
                Foreground = Grey150,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            portPanel.Children.Add(portLabel);

            portTextBox = new TextBox
            {
                Text = "14550",
                Width = 70,
                Background = Darker,
                Foreground = White,
                BorderBrush = MidGrey,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            portPanel.Children.Add(portTextBox);

            // Quick port buttons
            var port14551Btn = CreateQuickPortButton("14551");
            portPanel.Children.Add(port14551Btn);

            var port14591Btn = CreateQuickPortButton("14591");
            portPanel.Children.Add(port14591Btn);

            mainStack.Children.Add(portPanel);

            // Connect Button
            connectButton = new Button
            {
                Content = "📡 Connect to MAVLink",
                Background = Blue,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 10, 0, 10),
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            connectButton.Click += ConnectButton_Click;
            mainStack.Children.Add(connectButton);

            // Refresh Missions Button
            refreshMissionsButton = new Button
            {
                Content = "🔄 Refresh All Missions",
                Background = MidGrey,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                IsEnabled = false
            };
            refreshMissionsButton.Click += RefreshMissionsButton_Click;
            mainStack.Children.Add(refreshMissionsButton);

            // ===== DRONES SECTION =====
            mainStack.Children.Add(CreateSectionHeader("Active Drones"));

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
                Background = MidGrey,
                Foreground = White,
                BorderThickness = new Thickness(1),
                BorderBrush = Grey100,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 11,
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

                await _mavlinkManager.ConnectAsync("127.0.0.1", port);

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

        private Border CreateDroneCard(DroneState drone)
        {
            var card = new Border
            {
                Background = Darker,
                BorderBrush = drone.IsAlive() ? OkGreen : Grey100,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(4)
            };

            var stack = new StackPanel();

            // Header
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = $"Drone (Sys ID: {drone.SystemId})",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = White
            });

            if (!drone.IsAlive())
            {
                header.Children.Add(new TextBlock
                {
                    Text = " [OFFLINE]",
                    FontSize = 12,
                    Foreground = ErrRed,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            }

            stack.Children.Add(header);

            // Last seen
            var lastSeenText = drone.IsAlive() ?
                "Online" :
                $"Last seen: {(DateTime.UtcNow - drone.LastSeen).TotalSeconds:F0}s ago";

            stack.Children.Add(new TextBlock
            {
                Text = $"⏱️ {lastSeenText}",
                Foreground = Grey150,
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Position
            if (drone.Position != null)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"📍 {drone.Position.Latitude:F6}, {drone.Position.Longitude:F6} @ {drone.Position.Altitude:F1}m",
                    Foreground = Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            // Battery
            if (drone.Battery != null)
            {
                var batteryColor = drone.Battery.Percentage < 20 ? ErrRed :
                                  drone.Battery.Percentage < 50 ? Orange : Grey150;

                stack.Children.Add(new TextBlock
                {
                    Text = $"🔋 {drone.Battery.Percentage}% ({drone.Battery.Voltage:F1}V)",
                    Foreground = batteryColor,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Flight mode
            if (!string.IsNullOrEmpty(drone.FlightMode))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"✈️ Mode: {drone.FlightMode} {(drone.Armed ? "(ARMED)" : "")}",
                    Foreground = drone.Armed ? Orange : Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Mission info
            if (drone.Waypoints != null && drone.Waypoints.Count > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"📋 Mission: {drone.CurrentWaypoint}/{drone.Waypoints.Count} waypoints ({drone.GetMissionStatus()})",
                    Foreground = Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                // View Mission button
                var viewMissionBtn = new Button
                {
                    Content = "📍 View Mission Plot",
                    Background = Blue,
                    Foreground = White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 8, 0, 0),
                    FontSize = 12,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                viewMissionBtn.Click += (s, e) => ShowMissionPlot(drone);
                stack.Children.Add(viewMissionBtn);
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "📋 No mission uploaded",
                    Foreground = Grey100,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            card.Child = stack;
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
