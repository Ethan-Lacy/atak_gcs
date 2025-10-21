using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Views
{
    /// <summary>
    /// Simple plot map for visualizing mission waypoints
    /// </summary>
    public class MissionPlotWindow : Window
    {
        private Canvas _plotCanvas;
        private readonly List<MissionWaypoint> _waypoints;
        private readonly SolidColorBrush _dark = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        private readonly SolidColorBrush _white = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush _grey = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        private readonly SolidColorBrush _blue = new SolidColorBrush(Color.FromRgb(66, 135, 245));
        private readonly SolidColorBrush _green = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private readonly SolidColorBrush _orange = new SolidColorBrush(Color.FromRgb(255, 152, 0));

        public MissionPlotWindow(string agentId, List<MissionWaypoint> waypoints)
        {
            _waypoints = waypoints ?? new List<MissionWaypoint>();

            Title = $"Mission Plot - {agentId}";
            Width = 800;
            Height = 600;
            Background = _dark;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InitializeUI();
            PlotMission();
        }

        private void InitializeUI()
        {
            var mainGrid = new Grid();

            // Header
            var header = new TextBlock
            {
                Text = $"Mission Waypoints ({_waypoints.Count} total)",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = _white,
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Plot canvas
            _plotCanvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Margin = new Thickness(16, 60, 16, 80)
            };

            // Legend
            var legend = CreateLegend();

            mainGrid.Children.Add(_plotCanvas);
            mainGrid.Children.Add(header);
            mainGrid.Children.Add(legend);

            Content = mainGrid;
        }

        private StackPanel CreateLegend()
        {
            var legend = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 20)
            };

            legend.Children.Add(CreateLegendItem("Current", _green));
            legend.Children.Add(CreateLegendItem("Reached", _blue));
            legend.Children.Add(CreateLegendItem("Pending", _grey));
            legend.Children.Add(CreateLegendItem("Takeoff", _orange));
            legend.Children.Add(CreateLegendItem("Land", _orange));

            return legend;
        }

        private StackPanel CreateLegendItem(string label, SolidColorBrush color)
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 16, 0)
            };

            var circle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = color,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var text = new TextBlock
            {
                Text = label,
                Foreground = _white,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            item.Children.Add(circle);
            item.Children.Add(text);

            return item;
        }

        private void PlotMission()
        {
            if (_waypoints == null || _waypoints.Count == 0)
            {
                var noData = new TextBlock
                {
                    Text = "No mission waypoints available",
                    Foreground = _grey,
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(noData, (_plotCanvas.ActualWidth - 200) / 2);
                Canvas.SetTop(noData, (_plotCanvas.ActualHeight - 20) / 2);
                _plotCanvas.Children.Add(noData);
                return;
            }

            // Calculate bounds
            var latitudes = _waypoints.Select(w => w.Latitude).ToList();
            var longitudes = _waypoints.Select(w => w.Longitude).ToList();

            var minLat = latitudes.Min();
            var maxLat = latitudes.Max();
            var minLon = longitudes.Min();
            var maxLon = longitudes.Max();

            // Add padding (10% margin)
            var latRange = maxLat - minLat;
            var lonRange = maxLon - minLon;
            var padding = 0.1;

            minLat -= latRange * padding;
            maxLat += latRange * padding;
            minLon -= lonRange * padding;
            maxLon += lonRange * padding;

            // Handle single point or very small range
            if (latRange < 0.0001) { minLat -= 0.001; maxLat += 0.001; }
            if (lonRange < 0.0001) { minLon -= 0.001; maxLon += 0.001; }

            var canvasWidth = 750.0;
            var canvasHeight = 450.0;

            // Draw grid lines
            DrawGrid(canvasWidth, canvasHeight, minLat, maxLat, minLon, maxLon);

            // Draw path lines
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                var wp1 = _waypoints[i];
                var wp2 = _waypoints[i + 1];

                var x1 = MapLongitudeToX(wp1.Longitude, minLon, maxLon, canvasWidth);
                var y1 = MapLatitudeToY(wp1.Latitude, minLat, maxLat, canvasHeight);
                var x2 = MapLongitudeToX(wp2.Longitude, minLon, maxLon, canvasWidth);
                var y2 = MapLatitudeToY(wp2.Latitude, minLat, maxLat, canvasHeight);

                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = _grey,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                };

                _plotCanvas.Children.Add(line);
            }

            // Draw waypoints
            foreach (var wp in _waypoints)
            {
                var x = MapLongitudeToX(wp.Longitude, minLon, maxLon, canvasWidth);
                var y = MapLatitudeToY(wp.Latitude, minLat, maxLat, canvasHeight);

                var color = GetWaypointColor(wp);
                var size = wp.IsCurrent ? 16.0 : 12.0;

                // Waypoint circle
                var circle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = color,
                    Stroke = _white,
                    StrokeThickness = 2
                };

                Canvas.SetLeft(circle, x - size / 2);
                Canvas.SetTop(circle, y - size / 2);
                _plotCanvas.Children.Add(circle);

                // Sequence number
                var label = new TextBlock
                {
                    Text = wp.Sequence.ToString(),
                    Foreground = _white,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };

                Canvas.SetLeft(label, x + size / 2 + 4);
                Canvas.SetTop(label, y - 8);
                _plotCanvas.Children.Add(label);

                // Command type for special waypoints
                if (wp.Command == "TAKEOFF" || wp.Command == "LAND")
                {
                    var cmdLabel = new TextBlock
                    {
                        Text = wp.Command,
                        Foreground = _orange,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold
                    };

                    Canvas.SetLeft(cmdLabel, x + size / 2 + 4);
                    Canvas.SetTop(cmdLabel, y + 6);
                    _plotCanvas.Children.Add(cmdLabel);
                }
            }

            // Draw coordinate labels
            DrawCoordinateLabels(canvasWidth, canvasHeight, minLat, maxLat, minLon, maxLon);
        }

        private void DrawGrid(double width, double height, double minLat, double maxLat, double minLon, double maxLon)
        {
            var gridColor = new SolidColorBrush(Color.FromRgb(60, 60, 60));

            // Vertical lines (longitude)
            for (int i = 0; i <= 5; i++)
            {
                var x = (width / 5) * i;
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = gridColor,
                    StrokeThickness = 1
                };
                _plotCanvas.Children.Add(line);
            }

            // Horizontal lines (latitude)
            for (int i = 0; i <= 5; i++)
            {
                var y = (height / 5) * i;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = gridColor,
                    StrokeThickness = 1
                };
                _plotCanvas.Children.Add(line);
            }
        }

        private void DrawCoordinateLabels(double width, double height, double minLat, double maxLat, double minLon, double maxLon)
        {
            // Bottom-left corner (min lat, min lon)
            var minLabel = new TextBlock
            {
                Text = $"{minLat:F6}, {minLon:F6}",
                Foreground = _grey,
                FontSize = 10
            };
            Canvas.SetLeft(minLabel, 5);
            Canvas.SetTop(minLabel, height - 20);
            _plotCanvas.Children.Add(minLabel);

            // Top-right corner (max lat, max lon)
            var maxLabel = new TextBlock
            {
                Text = $"{maxLat:F6}, {maxLon:F6}",
                Foreground = _grey,
                FontSize = 10
            };
            Canvas.SetLeft(maxLabel, width - 150);
            Canvas.SetTop(maxLabel, 5);
            _plotCanvas.Children.Add(maxLabel);
        }

        private SolidColorBrush GetWaypointColor(MissionWaypoint wp)
        {
            if (wp.Command == "TAKEOFF" || wp.Command == "LAND")
                return _orange;

            if (wp.IsCurrent)
                return _green;

            if (wp.IsReached)
                return _blue;

            return _grey;
        }

        private double MapLongitudeToX(double longitude, double minLon, double maxLon, double canvasWidth)
        {
            return ((longitude - minLon) / (maxLon - minLon)) * canvasWidth;
        }

        private double MapLatitudeToY(double latitude, double minLat, double maxLat, double canvasHeight)
        {
            // Invert Y axis (canvas Y increases downward, but latitude increases upward)
            return canvasHeight - ((latitude - minLat) / (maxLat - minLat)) * canvasHeight;
        }
    }
}
