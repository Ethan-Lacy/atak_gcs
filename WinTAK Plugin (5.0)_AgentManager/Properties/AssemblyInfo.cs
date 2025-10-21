using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AgentManagerPlugin")]
[assembly: AssemblyDescription("WinTAK plugin for managing ATAK pilot and mission control agents via FastAPI backend")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AgentManagerPlugin")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("df39e53d-229c-4946-a80d-46579625591e")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Target WinTAK SDK 5.5 (installed version detected: 5.5.0.157)
[assembly: WinTak.Framework.TakSdkVersion("5.5.0.157")]
[assembly: WinTak.Framework.PluginName("Agent Manager")]
[assembly: WinTak.Framework.PluginDescription("Manage ATAK pilot and mission control agents through a FastAPI backend interface.")]
