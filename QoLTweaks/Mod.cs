using MelonLoader;
using MelonLoader.Logging;
using System;
using System.Reflection;
using Venomaus.BigAmbitionsMods.Common;
using Venomaus.BigAmbitionsMods.Common.Core;
using Venomaus.BigAmbitionsMods.Common.Helpers;
using Venomaus.BigAmbitionsMods.QoLTweaks.Modules.Traffic;

namespace Venomaus.BigAmbitionsMods.QoLTweaks
{
    /// <summary>
    /// Entrypoint to the QoLTweaks mod.
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public const string Name = "QoLTweaks";
        public const string Author = "Venomaus";
        public const string Version = "1.0.0";
        
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"Initialising..");

            // Mod configuration data setup
            ModConfiguration.Setup();

            // Start patching
            ApplyHarmonyPatches();

            // Start any other onetime related actions
            SubscribeEvents();
            
            LoggerInstance.Msg("Initialisation completed.");
        }

        private void SubscribeEvents()
        {
            Lib.Time.OnDayPassed += TimeEvents_OnDayPassed;
            Lib.SaveData.OnBeforeLoad += SaveData_OnBeforeLoad;
            Lib.SaveData.OnBeforeSave += SaveData_OnBeforeSave;
        }

        private void SaveData_OnBeforeLoad(object sender, SaveDataLib.SaveFileArgs e)
        {
            GasStationOverylay_Patches.Load(e);
        }

        private void SaveData_OnBeforeSave(object sender, SaveDataLib.SaveFileArgs e)
        {
            GasStationOverylay_Patches.Save(e);
        }

        private void TimeEvents_OnDayPassed(object sender, TimeLib.TimeArgs e)
        {
            GasStationOverylay_Patches.GasStationOverlay_GetButtons.HandlePremiumSubscriptionCosts();
            GasStationOverylay_Patches.GasStationOverlay_GetButtons.UpdateFuelPrices();
        }

        private void ApplyHarmonyPatches()
        {
            ApplyNonModulePatches();
            ApplyModulePatches();
        }

        private void ApplyModulePatches()
        {
            var moduleGroups = new (string, Type[] PatchTypes)[]
            {
                ("Modules.Traffic", new Type[]
                {
                    typeof(TrafficLight_Patches),
                    typeof(DrivingAI_Patches),
                    typeof(GasStationOverylay_Patches)
                })
            };

            LoggerInstance.Msg($"[MODULES]");
            foreach (var (entry, patchTypes) in moduleGroups)
            {
                // If module entry is enabled
                var moduleEnabled = ModConfiguration.Get<bool>(entry);
                if (moduleEnabled)
                {
                    foreach (var patchType in patchTypes)
                    {
                        // So all supplied types are "container" classes that contain the patches, so we need to collect the nested types and patch on those.
                        var nestedTypes = patchType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var nestedType in nestedTypes)
                            HarmonyInstance.PatchAll(nestedType);
                    }
                }

                // Some fancy colored logging in the console.
                ColoredStringBuilder.Create($"  (")
                    .Append($"{entry.Split('.')[1]}", ColorARGB.Magenta)
                    .Append("): ")
                    .Append($"{(moduleEnabled ? "enabled" : "disabled").ToUpper()}", moduleEnabled ? ColorARGB.LightGreen : ColorARGB.Red)
                    .SendMessageToConsole(LoggerInstance);
            }
        }

        private void ApplyNonModulePatches()
        {
            // Currently nothing yet
            var nonModuleGroups = new Type[]
            {

            };

            // Patch non-module patches
            foreach (var group in nonModuleGroups)
            {
                var nestedTypes = group.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var nestedType in nestedTypes)
                    HarmonyInstance.PatchAll(nestedType);
            }
        }
    }
}
