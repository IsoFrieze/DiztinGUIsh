using System.Reflection;
using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("2a2dd3c1-9e64-4cd7-98a5-310d9fed2ca3")]

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
// [assembly: AssemblyVersion("1.0.1.6")]
// [assembly: AssemblyFileVersion("1.0.1.6")]

[assembly: AssemblyTitle("Diz")]
[assembly: AssemblyDescription("DiztinGUIsh: SNES Disassembly Tool Suite")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("DiztinGUIsh")]
[assembly: AssemblyCopyright("(c)2021, GPL licensed")]

[assembly: AssemblyVersion (ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch)]
[assembly: AssemblyInformationalVersion (
    ThisAssembly.Git.SemVer.Major + "." +
    ThisAssembly.Git.SemVer.Minor + "." +
    ThisAssembly.Git.SemVer.Patch + "-" +
    ThisAssembly.Git.SemVer.Label // + "--" +
    // ThisAssembly.Git.Branch + "+" +
    // ThisAssembly.Git.Commit
    )]

[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]