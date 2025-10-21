using System.ComponentModel.Composition;

namespace AgentManagerPlugin.Buttons
{
    [Export(typeof(WinTak.Framework.Tools.Button))]
    [WinTak.Framework.Tools.Attributes.Button("AgentManagerButton", "Agent Manager")]
    internal class AgentManagerButton : WinTak.Framework.Tools.Button
    {
        private readonly WinTak.Framework.Docking.IDockingManager _dockingManager;

        [ImportingConstructor]
        public AgentManagerButton(WinTak.Framework.Docking.IDockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        protected override void OnClick()
        {
            base.OnClick();

            var pane = _dockingManager.GetDockPane(AgentManagerPlugin.DockPanes.AgentManagerDockPane.Id);
            if (pane != null)
                pane.Activate();
        }
    }
}
