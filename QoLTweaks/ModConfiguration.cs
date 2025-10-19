using MelonLoader;
using System.IO;

namespace Venomaus.BigAmbitionsMods.QoLTweaks
{
    internal static class ModConfiguration
    {
        // Categories
        private static MelonPreferences_Category _tweaksCategory;
        private static MelonPreferences_Category _dataCategory;
        
        // Modules
        internal static MelonPreferences_Entry<bool> Traffic { get; private set; }

        // Data of modules
        internal static MelonPreferences_Entry<int> PremiumSubscriptionBiWeeklyCost { get; private set; }
        internal static MelonPreferences_Entry<int> PremiumSubscriptionCoversRepairCostPercentage { get; private set; }
        internal static MelonPreferences_Entry<int> AIDrivingSpeedReduction { get; private set; }

        internal static void Setup()
        {
            // Path to userdata folder
            string userDataPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "UserData"));

            // Configuration file name
            string modConfigurationFilepath = Path.Combine(userDataPath, "QoLTweaks.cfg");
            bool configFileExists = File.Exists(modConfigurationFilepath);

            // Available categories
            _tweaksCategory = MelonPreferences.CreateCategory("Tweaks");
            _tweaksCategory.SetFilePath(modConfigurationFilepath, configFileExists, false);
            _dataCategory = MelonPreferences.CreateCategory("Data");
            _dataCategory.SetFilePath(modConfigurationFilepath, configFileExists, false);

            // Add configuration entry for each module
            Traffic = _tweaksCategory.CreateEntry("Traffic", true, description: "This module contains several QoL tweaks regarding traffic and vehicles.");

            // Data of modules entries
            PremiumSubscriptionBiWeeklyCost = _dataCategory.CreateEntry("PremiumSubscriptionBiWeeklyCost", 750, description: "Pay a portion of this cost each day, leads up to a total of this set price over two weeks (total / 14 per day). Repeats until canceled.");
            PremiumSubscriptionCoversRepairCostPercentage = _dataCategory.CreateEntry("PremiumSubscriptionCoversRepairCostPercentage", 80, description: "Defines by how much the repair cost is reduced percentage wise.");
            AIDrivingSpeedReduction = _dataCategory.CreateEntry("AIDrivingSpeedReduction", 20, description: "Defines by how much the AI drivers max speed is reduced percentage wise.");

            // Initial save
            if (!configFileExists)
            {
                Melon<Mod>.Logger.Msg("Created new configuration file.");
            }
            else
            {
                Melon<Mod>.Logger.Msg("Loaded configuration from existing file.");
            }
        }
    }
}