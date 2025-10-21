using Prism.Mef.Modularity;
using Prism.Modularity;
using System.ComponentModel.Composition;

namespace AgentManagerPlugin
{
    [ModuleExport(typeof(AgentManagerModule), InitializationMode = InitializationMode.WhenAvailable)]
    internal class AgentManagerModule : IModule, WinTak.Framework.ITakModule
    {
        private readonly WinTak.Framework.Messaging.IMessageHub _messageHub;
        private readonly WinTak.Framework.Docking.IDockingManager _dockingManager;

        [ImportingConstructor]
        public AgentManagerModule(WinTak.Framework.Messaging.IMessageHub messageHub,
                                 WinTak.Framework.Docking.IDockingManager dockingManager)
        {
            _messageHub = messageHub;
            _dockingManager = dockingManager;
        }

        // Modules will be initialized during startup. Any work that needs to be done at startup can
        // be initiated from here.
        public void Initialize()
        {
            _messageHub.Subscribe<WinTak.Common.Messaging.ClearContentMessage>(OnClearContent);
        }

        // This method will be called when the WinTAK splash screen has closed and 'startup' is finished
        public void Startup()
        {
            // Avoid activating during Startup to prevent MEF timing issues; use the ribbon button instead
            // var pane = _dockingManager.GetDockPane(AgentManagerPlugin.DockPanes.AgentManagerDockPane.Id);
            // if (pane != null)
            //     pane.Activate();
        }

        // This method is called when WinTAK is shutting down. Any cleanup operations can be run from here.
        public void Terminate()
        {
        }

        // This message is invoked if the user has initiated clear content. Delete any user data
        private void OnClearContent(WinTak.Common.Messaging.ClearContentMessage args)
        {
        }
    }
}
