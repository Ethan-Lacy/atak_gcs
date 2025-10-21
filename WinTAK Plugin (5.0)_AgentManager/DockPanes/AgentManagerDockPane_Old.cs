using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentManagerPlugin.Models;
using AgentManagerPlugin.Services;

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
        private readonly IAgentApiClient _apiClient;

        // UI Controls
        private TextBlock statusLabel;
        private ComboBox vehicleTypeCombo;
        private TextBox vehicleIdInput;
        private TextBox altitudeInput;
        private ComboBox pilotCertCombo;
        private TextBox portInput;
        private Button addPilotButton;
        private ComboBox mcCertCombo;
        private Button startMcButton;
        private StackPanel activeAgentsPanel;
        private System.Windows.Threading.DispatcherTimer refreshTimer;

        // Color palette (dark theme from voice plugin)
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
        public AgentManagerView()
        {
            _apiClient = new AgentApiClient();
            BuildUI();
            InitializeAsync();
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
                Text = "AGENT MANAGER",
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
                Fill = Blue,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusDot);

            statusLabel = new TextBlock
            {
                Text = "CONNECTING...",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Orange,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusLabel);

            mainStack.Children.Add(headerPanel);

            // ===== ADD PILOT SECTION =====
            var pilotExpander = new Expander
            {
                Header = CreateSectionHeader("▼ Add Pilot"),
                IsExpanded = true,
                Foreground = Grey150,
                Background = Darker,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var pilotStack = new StackPanel
            {
                Margin = new Thickness(12),
                Background = Darker
            };

            // Vehicle Type
            pilotStack.Children.Add(CreateLabel("Vehicle Type:"));
            vehicleTypeCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = MidGrey,
                Foreground = White,
                BorderBrush = Grey100,
                BorderThickness = new Thickness(1),
                Height = 32,
                FontSize = 14,
                Padding = new Thickness(8, 4, 8, 4)
            };
            vehicleTypeCombo.Items.Add("quad");
            vehicleTypeCombo.Items.Add("vtol");
            vehicleTypeCombo.SelectedIndex = 0;
            pilotStack.Children.Add(vehicleTypeCombo);

            // Vehicle ID
            pilotStack.Children.Add(CreateLabel("Vehicle ID:"));
            vehicleIdInput = CreateTextBox("1");
            pilotStack.Children.Add(vehicleIdInput);

            // Altitude
            pilotStack.Children.Add(CreateLabel("Altitude (m):"));
            altitudeInput = CreateTextBox("50");
            pilotStack.Children.Add(altitudeInput);

            // Certificate
            pilotStack.Children.Add(CreateLabel("Certificate:"));
            pilotCertCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = MidGrey,
                Foreground = White,
                BorderBrush = Grey100,
                BorderThickness = new Thickness(1),
                Height = 32,
                FontSize = 14,
                Padding = new Thickness(8, 4, 8, 4)
            };
            pilotStack.Children.Add(pilotCertCombo);

            // Port
            pilotStack.Children.Add(CreateLabel("Connection Port:"));
            portInput = CreateTextBox("14591");
            pilotStack.Children.Add(portInput);

            // Add Pilot Button
            addPilotButton = new Button
            {
                Content = "+ ADD PILOT",
                Height = 40,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Background = Blue,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 12, 0, 0)
            };
            addPilotButton.Click += AddPilotButton_Click;
            pilotStack.Children.Add(addPilotButton);

            pilotExpander.Content = pilotStack;
            mainStack.Children.Add(pilotExpander);

            // ===== MISSION CONTROL SECTION =====
            var mcExpander = new Expander
            {
                Header = CreateSectionHeader("▼ Mission Control"),
                IsExpanded = false,
                Foreground = Grey150,
                Background = Darker,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var mcStack = new StackPanel
            {
                Margin = new Thickness(12),
                Background = Darker
            };

            mcStack.Children.Add(CreateLabel("Certificate:"));
            mcCertCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = MidGrey,
                Foreground = White,
                BorderBrush = Grey100,
                BorderThickness = new Thickness(1),
                Height = 32,
                FontSize = 14,
                Padding = new Thickness(8, 4, 8, 4)
            };
            mcStack.Children.Add(mcCertCombo);

            startMcButton = new Button
            {
                Content = "▶ START MISSION CONTROL",
                Height = 40,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Background = OkGreen,
                Foreground = Dark,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 12, 0, 0)
            };
            startMcButton.Click += StartMcButton_Click;
            mcStack.Children.Add(startMcButton);

            mcExpander.Content = mcStack;
            mainStack.Children.Add(mcExpander);

            // ===== ACTIVE AGENTS SECTION =====
            var agentsTitle = new TextBlock
            {
                Text = "Active Agents",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Grey150,
                Margin = new Thickness(0, 12, 0, 8)
            };
            mainStack.Children.Add(agentsTitle);

            var agentsBorder = new Border
            {
                Background = Darker,
                BorderBrush = MidGrey,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                MinHeight = 100
            };

            activeAgentsPanel = new StackPanel();
            agentsBorder.Child = activeAgentsPanel;
            mainStack.Children.Add(agentsBorder);

            scrollViewer.Content = mainStack;
            Content = scrollViewer;

            // Setup refresh timer
            refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Grey150
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = Grey150,
                Margin = new Thickness(0, 4, 0, 4)
            };
        }

        private TextBox CreateTextBox(string defaultValue)
        {
            return new TextBox
            {
                Text = defaultValue,
                Background = MidGrey,
                Foreground = White,
                BorderBrush = Grey100,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 14,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private async void InitializeAsync()
        {
            try
            {
                // Check API availability
                bool isAvailable = await _apiClient.IsApiAvailableAsync();
                if (!isAvailable)
                {
                    statusLabel.Text = "API OFFLINE";
                    statusLabel.Foreground = ErrRed;
                    MessageBox.Show(
                        "Cannot connect to Agent Manager API at http://localhost:8000\n\n" +
                        "Please ensure the FastAPI backend is running.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                statusLabel.Text = "READY";
                statusLabel.Foreground = OkGreen;

                // Load certificates
                var certs = await _apiClient.GetCertificatesAsync();
                foreach (var cert in certs)
                {
                    pilotCertCombo.Items.Add(cert.Name);
                    mcCertCombo.Items.Add(cert.Name);
                }

                if (certs.Any())
                {
                    pilotCertCombo.SelectedIndex = 0;
                    mcCertCombo.SelectedIndex = 0;
                }

                // Load active agents
                await RefreshActiveAgentsAsync();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ERROR";
                statusLabel.Foreground = ErrRed;
                MessageBox.Show($"Initialization error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddPilotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                addPilotButton.IsEnabled = false;
                statusLabel.Text = "ADDING PILOT...";
                statusLabel.Foreground = Orange;

                if (!int.TryParse(vehicleIdInput.Text, out int vehicleId))
                {
                    MessageBox.Show("Vehicle ID must be a number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(altitudeInput.Text, out int altitude))
                {
                    MessageBox.Show("Altitude must be a number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(portInput.Text, out int port))
                {
                    MessageBox.Show("Port must be a number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = new PilotConfig
                {
                    VehicleId = vehicleId,
                    CertName = pilotCertCombo.SelectedItem?.ToString() ?? "",
                    ConnectionPort = port,
                    VehicleType = vehicleTypeCombo.SelectedItem?.ToString() ?? "quad",
                    Altitude = altitude
                };

                var agentInfo = await _apiClient.AddPilotAsync(config);

                statusLabel.Text = "PILOT ADDED";
                statusLabel.Foreground = OkGreen;

                MessageBox.Show($"Pilot agent started successfully!\n\nAgent ID: {agentInfo.AgentId}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshActiveAgentsAsync();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ERROR";
                statusLabel.Foreground = ErrRed;
                MessageBox.Show($"Failed to add pilot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                addPilotButton.IsEnabled = true;
                statusLabel.Text = "READY";
                statusLabel.Foreground = Blue;
            }
        }

        private async void StartMcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                startMcButton.IsEnabled = false;
                statusLabel.Text = "STARTING MC...";
                statusLabel.Foreground = Orange;

                var config = new MissionControlConfig
                {
                    CertName = mcCertCombo.SelectedItem?.ToString() ?? ""
                };

                var agentInfo = await _apiClient.StartMissionControlAsync(config);

                statusLabel.Text = "MC STARTED";
                statusLabel.Foreground = OkGreen;

                MessageBox.Show($"Mission Control started successfully!\n\nAgent ID: {agentInfo.AgentId}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshActiveAgentsAsync();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ERROR";
                statusLabel.Foreground = ErrRed;
                MessageBox.Show($"Failed to start Mission Control:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                startMcButton.IsEnabled = true;
                statusLabel.Text = "READY";
                statusLabel.Foreground = Blue;
            }
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshActiveAgentsAsync();
        }

        private async System.Threading.Tasks.Task RefreshActiveAgentsAsync()
        {
            try
            {
                var agents = await _apiClient.GetActiveAgentsAsync();

                activeAgentsPanel.Children.Clear();

                if (!agents.Any())
                {
                    var emptyText = new TextBlock
                    {
                        Text = "No active agents",
                        Foreground = Grey100,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 8, 0, 8)
                    };
                    activeAgentsPanel.Children.Add(emptyText);
                    return;
                }

                foreach (var agent in agents)
                {
                    var agentCard = CreateAgentCard(agent);
                    activeAgentsPanel.Children.Add(agentCard);
                }
            }
            catch
            {
                // Silent fail on refresh
            }
        }

        private Border CreateAgentCard(AgentInfo agent)
        {
            var card = new Border
            {
                Background = MidGrey,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Agent header
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = OkGreen,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(statusDot);

            var agentName = new TextBlock
            {
                Text = $"{agent.Type.ToUpper()} - {agent.AgentId}",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = White
            };
            header.Children.Add(agentName);

            stack.Children.Add(header);

            // Agent details
            var details = new TextBlock
            {
                Text = $"Status: {agent.Status}",
                FontSize = 11,
                Foreground = Grey150,
                Margin = new Thickness(18, 4, 0, 8)
            };
            stack.Children.Add(details);

            // Add mission waypoints viewer button
            if (agent.Type == "pilot")
            {
                var viewMissionBtn = new Button
                {
                    Content = "📍 View Mission",
                    Height = 28,
                    FontSize = 11,
                    Background = Blue,
                    Foreground = White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(18, 0, 0, 8)
                };
                viewMissionBtn.Click += async (s, e) =>
                {
                    await ShowMissionDetailsAsync(agent.AgentId);
                };
                stack.Children.Add(viewMissionBtn);
            }

            // Remove button
            var removeBtn = new Button
            {
                Content = agent.Type == "pilot" ? "Remove Pilot" : "Stop MC",
                Height = 28,
                FontSize = 11,
                Background = ErrRed,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(18, 0, 0, 0)
            };
            removeBtn.Click += async (s, e) =>
            {
                try
                {
                    bool success = agent.Type == "pilot"
                        ? await _apiClient.RemovePilotAsync(agent.AgentId)
                        : await _apiClient.StopMissionControlAsync(agent.AgentId);

                    if (success)
                    {
                        await RefreshActiveAgentsAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove agent:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            stack.Children.Add(removeBtn);

            card.Child = stack;
            return card;
        }

        private async System.Threading.Tasks.Task ShowMissionDetailsAsync(string agentId)
        {
            try
            {
                var agentStatus = await _apiClient.GetAgentStatusAsync(agentId);

                if (agentStatus?.Waypoints?.Waypoints == null || !agentStatus.Waypoints.Waypoints.Any())
                {
                    MessageBox.Show("No mission waypoints available for this agent.", "Mission", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create mission display window
                var missionWindow = new Window
                {
                    Title = $"Mission - {agentId}",
                    Width = 500,
                    Height = 600,
                    Background = Dark,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = Dark,
                    Padding = new Thickness(16)
                };

                var missionStack = new StackPanel();

                // Mission header
                var headerText = new TextBlock
                {
                    Text = $"Mission Plan - {agentStatus.Waypoints.Current}/{agentStatus.Waypoints.Total} waypoints",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = White,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                missionStack.Children.Add(headerText);

                // Waypoint list (qGroundControl style)
                foreach (var wp in agentStatus.Waypoints.Waypoints.OrderBy(w => w.Sequence))
                {
                    var wpCard = CreateWaypointCard(wp);
                    missionStack.Children.Add(wpCard);
                }

                scrollViewer.Content = missionStack;
                missionWindow.Content = scrollViewer;
                missionWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load mission:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateWaypointCard(MissionWaypoint wp)
        {
            var card = new Border
            {
                Background = wp.IsCurrent ? Blue : (wp.IsReached ? MidGrey : Darker),
                BorderBrush = wp.IsCurrent ? OkGreen : Grey100,
                BorderThickness = new Thickness(wp.IsCurrent ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Waypoint header
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var seqBox = new Border
            {
                Background = wp.IsCurrent ? OkGreen : (wp.IsReached ? Grey100 : MidGrey),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0)
            };

            var seqText = new TextBlock
            {
                Text = wp.Sequence.ToString(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = wp.IsCurrent ? Dark : White
            };
            seqBox.Child = seqText;
            header.Children.Add(seqBox);

            var commandText = new TextBlock
            {
                Text = wp.Command,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = White,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(commandText);

            if (wp.IsCurrent)
            {
                var currentIndicator = new TextBlock
                {
                    Text = " ◀ CURRENT",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = OkGreen,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                header.Children.Add(currentIndicator);
            }
            else if (wp.IsReached)
            {
                var reachedIndicator = new TextBlock
                {
                    Text = " ✓",
                    FontSize = 14,
                    Foreground = OkGreen,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                header.Children.Add(reachedIndicator);
            }

            stack.Children.Add(header);

            // Coordinates
            var coordsText = new TextBlock
            {
                Text = $"📍 {wp.Latitude:F6}, {wp.Longitude:F6}",
                FontSize = 11,
                Foreground = Grey150,
                Margin = new Thickness(36, 4, 0, 2)
            };
            stack.Children.Add(coordsText);

            // Altitude
            var altText = new TextBlock
            {
                Text = $"⬆ {wp.Altitude:F1} m",
                FontSize = 11,
                Foreground = Grey150,
                Margin = new Thickness(36, 0, 0, 0)
            };
            stack.Children.Add(altText);

            card.Child = stack;
            return card;
        }
    }
}
