using Prism.Mef.Modularity;
using Prism.Modularity;
using System.ComponentModel.Composition;
using WinTak.Common.Messaging;
using WinTak.Framework;
using WinTak.Framework.Messaging;

namespace WinTAK_Plugin_Agent_Manager
{
    [ModuleExport(typeof(WinTAK_Plugin_Agent_ManagerModule), InitializationMode = InitializationMode.WhenAvailable)]
    internal class WinTAK_Plugin_Agent_ManagerModule : IModule, ITakModule
    {
        private readonly IMessageHub _messageHub;

        [ImportingConstructor]
        public WinTAK_Plugin_Agent_ManagerModule(IMessageHub messageHub)
        {
            _messageHub = messageHub;
        }

        // Modules will be initialized during startup. Any work that needs to be done at startup can
        // be initiated from here.
        public void Initialize()
        {
            _messageHub.Subscribe<ClearContentMessage>(OnClearContent);
        }

        // This method will be called when the WinTAK splash screen has closed and 'startup' is finished
        public void Startup()
        {
        }

        // This method is called when WinTAK is shutting down. Any cleanup operations can be run from here.
        public void Terminate()
        {
        }

        // Thes message is invoked if the user has initiated clear content. Delete any user data
        private void OnClearContent(ClearContentMessage args)
        {
        }
    }
}
