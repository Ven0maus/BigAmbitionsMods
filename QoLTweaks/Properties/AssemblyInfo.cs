﻿using MelonLoader;
using System.Reflection;
using System.Runtime.InteropServices;
using Venomaus.BigAmbitionsMods.QoLTweaks;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("QoLTweaks")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("QoLTweaks")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("7e28be20-7ff4-43ce-9fcd-47ccb64449ea")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Mod information
[assembly: MelonAdditionalDependencies("Common")]
[assembly: MelonInfo(typeof(Mod), Mod.Name, Mod.Version, Mod.Author)]
[assembly: MelonGame("Hovgaard Games", "Big Ambitions")]

// Manual patching
[assembly: HarmonyDontPatchAll]
