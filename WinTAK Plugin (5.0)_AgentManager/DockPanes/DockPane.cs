using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;

namespace SimpleWinTAKPlugin.DockPanes
{
    // Export the DockPane so MEF can compose it
    [Export(typeof(WinTak.Framework.Docking.DockPane))]
    // IMPORTANT: provide Content type via attribute so WinTAK can resolve it
    [WinTak.Framework.Docking.Attributes.DockPane(Id, "Simple Plugin", Content = typeof(SimplePluginView))]
    internal class SimplePluginDockPane : WinTak.Framework.Docking.DockPane
    {
        internal const string Id = "SimplePluginDockPane";

        [ImportingConstructor]
        public SimplePluginDockPane()
        {
        }
    }

    // Export the view with the same contract type that the DockPane attribute provided
    [Export(typeof(SimplePluginView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class SimplePluginView : UserControl
    {
        public SimplePluginView()
        {
            var stack = new StackPanel { Margin = new Thickness(12) };

            stack.Children.Add(new TextBlock
            {
                Text = "Simple WinTAK Plugin Loaded",
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });

            stack.Children.Add(new TextBlock
            {
                Text = "This plugin is now active and visible in the Plugins tab.",
                Margin = new Thickness(0, 8, 0, 0)
            });

            var btn = new Button
            {
                Content = "Test Button",
                Width = 100,
                Margin = new Thickness(0, 12, 0, 0)
            };
            btn.Click += (s, e) => MessageBox.Show("Plugin is working correctly!");
            stack.Children.Add(btn);

            Content = stack;
        }
    }
}
