using System.ComponentModel.Composition;

namespace SimpleWinTAKPlugin.Buttons
{
    [Export(typeof(WinTak.Framework.Tools.Button))]
    [WinTak.Framework.Tools.Attributes.Button("SimplePluginButton", "Simple Plugin")]
    internal class SimplePluginButton : WinTak.Framework.Tools.Button
    {
        private readonly WinTak.Framework.Docking.IDockingManager _dockingManager;

        [ImportingConstructor]
        public SimplePluginButton(WinTak.Framework.Docking.IDockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        protected override void OnClick()
        {
            base.OnClick();

            var pane = _dockingManager.GetDockPane(SimpleWinTAKPlugin.DockPanes.SimplePluginDockPane.Id);
            if (pane != null)
                pane.Activate();
        }
    }
}
