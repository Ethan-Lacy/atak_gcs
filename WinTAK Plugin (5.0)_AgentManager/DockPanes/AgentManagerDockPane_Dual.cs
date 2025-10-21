using System;
using System.Collections.Generic;
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
        // MAVLink agents
        private Dictionary<string, MavlinkAgent> _agents = new Dictionary<string, MavlinkAgent>();

        // UI Controls
        private TextBlock statusLabel;
        private ComboBox vehicleTypeCombo;
        private TextBox vehicleIdInput;
        private TextBox systemIdInput;
        private TextBox altitudeInput;
        private TextBox hostInput;
        private TextBox portInput;
        private Button addPilotButton;
        private Button refreshMissionsButton;
        private StackPanel activeAgentsPanel;
        private System.Windows.Threading.DispatcherTimer refreshTimer;

        // Color palette (dark theme)
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
            BuildUI();
            StartRefreshTimer();
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
                Text = "AGENT MANAGER (MAVLink Direct)",
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
                Fill = OkGreen,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusDot);

            statusLabel = new TextBlock
            {
                Text = "Ready",
                Foreground = Grey150,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusLabel);

            mainStack.Children.Add(headerPanel);

            // ===== ADD PILOT SECTION =====
            mainStack.Children.Add(CreateSectionHeader("Add Pilot Agent"));

            // Vehicle Type
            mainStack.Children.Add(CreateLabel("Vehicle Type"));
            vehicleTypeCombo = CreateComboBox(new[] { "quad", "vtol" }, "quad");
            mainStack.Children.Add(vehicleTypeCombo);

            // Vehicle ID
            mainStack.Children.Add(CreateLabel("Vehicle ID"));
            vehicleIdInput = CreateTextBox("1");
            mainStack.Children.Add(vehicleIdInput);

            // System ID
            mainStack.Children.Add(CreateLabel("MAVLink System ID"));
            systemIdInput = CreateTextBox("1");
            mainStack.Children.Add(systemIdInput);

            // Host
            mainStack.Children.Add(CreateLabel("Host"));
            hostInput = CreateTextBox("127.0.0.1");
            mainStack.Children.Add(hostInput);

            // Port
            mainStack.Children.Add(CreateLabel("UDP Port"));
            portInput = CreateTextBox("14550");
            mainStack.Children.Add(portInput);

            // Altitude
            mainStack.Children.Add(CreateLabel("Default Altitude (m)"));
            altitudeInput = CreateTextBox("50");
            mainStack.Children.Add(altitudeInput);

            // Add Pilot Button
            addPilotButton = new Button
            {
                Content = "🚁 Connect to Vehicle",
                Background = Blue,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 10, 0, 10),
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            addPilotButton.Click += AddPilotButton_Click;
            mainStack.Children.Add(addPilotButton);

            // ===== ACTIVE AGENTS SECTION =====
            mainStack.Children.Add(CreateSectionHeader("Active Agents"));

            // Refresh Missions Button
            refreshMissionsButton = new Button
            {
                Content = "🔄 Refresh Missions",
                Background = MidGrey,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            refreshMissionsButton.Click += RefreshMissionsButton_Click;
            mainStack.Children.Add(refreshMissionsButton);

            activeAgentsPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            mainStack.Children.Add(activeAgentsPanel);

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
                Margin = new Thickness(0, 24, 0, 12),
                BorderBottom = new Pen(Grey100, 1)
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Grey150,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 4)
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

        private ComboBox CreateComboBox(string[] items, string selected)
        {
            var combo = new ComboBox
            {
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

            foreach (var item in items)
            {
                combo.Items.Add(item);
            }

            if (!string.IsNullOrEmpty(selected))
            {
                combo.SelectedItem = selected;
            }

            return combo;
        }

        private async void AddPilotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                statusLabel.Text = "Connecting...";
                statusLabel.Foreground = Orange;

                var vehicleId = int.Parse(vehicleIdInput.Text);
                var systemId = int.Parse(systemIdInput.Text);
                var host = hostInput.Text;
                var port = int.Parse(portInput.Text);
                var vehicleType = vehicleTypeCombo.SelectedItem?.ToString() ?? "quad";

                var agentId = $"pilot_{vehicleId}";

                if (_agents.ContainsKey(agentId))
                {
                    MessageBox.Show($"Agent {agentId} already exists. Remove it first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var agent = new MavlinkAgent(vehicleId, systemId, host, port, vehicleType);
                await agent.ConnectAsync();

                _agents[agentId] = agent;

                // Request mission
                await agent.RequestMissionAsync();

                statusLabel.Text = $"Connected to {agentId}";
                statusLabel.Foreground = OkGreen;

                RefreshAgentList();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.Foreground = ErrRed;
                MessageBox.Show($"Failed to connect:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshMissionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                statusLabel.Text = "Refreshing missions...";
                statusLabel.Foreground = Orange;

                foreach (var agent in _agents.Values)
                {
                    await agent.RequestMissionAsync();
                }

                await System.Threading.Tasks.Task.Delay(1000); // Wait for mission download

                statusLabel.Text = "Missions refreshed";
                statusLabel.Foreground = OkGreen;

                RefreshAgentList();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.Foreground = ErrRed;
            }
        }

        private void RefreshAgentList()
        {
            activeAgentsPanel.Children.Clear();

            if (_agents.Count == 0)
            {
                var noAgents = new TextBlock
                {
                    Text = "No active agents. Connect to a vehicle above.",
                    Foreground = Grey100,
                    FontSize = 13,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                activeAgentsPanel.Children.Add(noAgents);
                return;
            }

            foreach (var kvp in _agents)
            {
                activeAgentsPanel.Children.Add(CreateAgentCard(kvp.Key, kvp.Value));
            }
        }

        private Border CreateAgentCard(string agentId, MavlinkAgent agent)
        {
            var card = new Border
            {
                Background = Darker,
                BorderBrush = Grey100,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(4)
            };

            var stack = new StackPanel();

            // Header
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = agentId,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = White
            });
            header.Children.Add(new TextBlock
            {
                Text = $" ({agent.Status})",
                FontSize = 12,
                Foreground = agent.Status == "Connected" ? OkGreen : Grey100,
                Margin = new Thickness(8, 0, 0, 0)
            });
            stack.Children.Add(header);

            // Telemetry
            if (agent.Position != null)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"📍 {agent.Position.Latitude:F6}, {agent.Position.Longitude:F6} @ {agent.Position.Altitude:F1}m",
                    Foreground = Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            if (agent.Battery != null)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"🔋 {agent.Battery.Percentage}% ({agent.Battery.Voltage:F1}V)",
                    Foreground = Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            if (!string.IsNullOrEmpty(agent.FlightMode))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"✈️ Mode: {agent.FlightMode} {(agent.Armed ? "(ARMED)" : "")}",
                    Foreground = agent.Armed ? Orange : Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Mission info
            if (agent.Waypoints != null && agent.Waypoints.Count > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"📋 Mission: {agent.CurrentWaypoint}/{agent.Waypoints.Count} waypoints ({agent.GetMissionStatus()})",
                    Foreground = Grey150,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // View Mission button
            var viewMissionBtn = new Button
            {
                Content = "📍 View Mission Plot",
                Background = Blue,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            viewMissionBtn.Click += (s, e) => ShowMissionPlot(agentId, agent);
            buttonPanel.Children.Add(viewMissionBtn);

            // Remove button
            var removeBtn = new Button
            {
                Content = "❌ Disconnect",
                Background = MidGrey,
                Foreground = White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            removeBtn.Click += (s, e) => RemoveAgent(agentId);
            buttonPanel.Children.Add(removeBtn);

            stack.Children.Add(buttonPanel);

            card.Child = stack;
            return card;
        }

        private void ShowMissionPlot(string agentId, MavlinkAgent agent)
        {
            try
            {
                if (agent.Waypoints == null || agent.Waypoints.Count == 0)
                {
                    MessageBox.Show("No mission waypoints available for this agent.\n\nClick 'Refresh Missions' to download the mission from the vehicle.", "No Mission", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var plotWindow = new MissionPlotWindow(agentId, agent.Waypoints);
                plotWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show mission plot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAgent(string agentId)
        {
            try
            {
                if (_agents.ContainsKey(agentId))
                {
                    _agents[agentId].Dispose();
                    _agents.Remove(agentId);
                }

                RefreshAgentList();
                statusLabel.Text = $"Disconnected {agentId}";
                statusLabel.Foreground = Grey100;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove agent:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartRefreshTimer()
        {
            refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            refreshTimer.Tick += (s, e) => RefreshAgentList();
            refreshTimer.Start();
        }
    }
}
