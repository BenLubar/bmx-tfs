using System;
using System.Reflection;
using System.Runtime.InteropServices;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif

[assembly: AssemblyTitle("TFS")]
[assembly: AssemblyDescription("Source Control and Issue Tracking integration for Team Foundation Server.")]

[assembly: ComVisible(false)]
[assembly: AssemblyCompany("Inedo, LLC")]
[assembly: AssemblyCopyright("Copyright © 2008 - 2016")]
[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyFileVersion("0.0")]
[assembly: CLSCompliant(false)]

[assembly: ScriptNamespace("TFS")]
