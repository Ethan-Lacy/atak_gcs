using System.ComponentModel.Composition;
using WinTak.Framework.Docking;
using WinTak.Framework.Docking.Attributes;
using WinTAK_Plugin_Agent_Manager.Views;

namespace WinTAK_Plugin_Agent_Manager.DockPanes
{
    [DockPane(Id, "WinTAK_Plugin_Agent_Manager", Content = typeof(WinTAK_Plugin_Agent_ManagerView))]
    internal class WinTAK_Plugin_Agent_ManagerDockPane : DockPane
    {
        internal const string Id = "WinTAK_Plugin_Agent_Manager_DockPanes_WinTAK_Plugin_Agent_ManagerDockPane";

        [ImportingConstructor]
        public WinTAK_Plugin_Agent_ManagerDockPane()
        {
        }
    }
}
