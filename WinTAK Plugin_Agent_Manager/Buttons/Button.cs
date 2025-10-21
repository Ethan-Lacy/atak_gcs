using System.ComponentModel.Composition;
using WinTak.Framework.Docking;
using WinTak.Framework.Tools;
using WinTak.Framework.Tools.Attributes;
using WinTAK_Plugin_Agent_Manager.DockPanes;

namespace WinTAK_Plugin_Agent_Manager.Buttons
{
    [Button("WinTAK_Plugin_Agent_Manager_Buttons_WinTAK_Plugin_Agent_ManagerButton", "WinTAK_Plugin_Agent_Manager Plugin",
        LargeImage = "pack://application:,,,/WinTAK_Plugin_Agent_Manager;component/Assets/Settings_32x32.svg",
        SmallImage = "pack://application:,,,/WinTAK_Plugin_Agent_Manager;component/Assets/Settings_16x16.png")]
    internal class WinTAK_Plugin_Agent_ManagerButton : Button
    {
        private IDockingManager _dockingManager;

        [ImportingConstructor]
        public WinTAK_Plugin_Agent_ManagerButton(IDockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        protected override void OnClick()
        {
            base.OnClick();

            var pane = _dockingManager.GetDockPane(WinTAK_Plugin_Agent_ManagerDockPane.Id);
            if (pane != null)
                pane.Activate();
        }
    }
}
