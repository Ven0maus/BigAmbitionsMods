using System.Reflection;
using Venomaus.BigAmbitionsMods.Common;
using Venomaus.BigAmbitionsMods.Common.Objects;

namespace Venomaus.BigAmbitionsMods.QoLTweaks
{
    internal static class ModConfiguration
    {
        private static Configuration _configuration;

        // Data of modules
        internal static int PremiumSubscriptionBiWeeklyCost => Get<int>("Traffic.PremiumSubscriptionBiWeeklyCost");
        internal static int PremiumSubscriptionCoversRepairCostPercentage => Get<int>("Traffic.PremiumSubscriptionCoversRepairCostPercentage");
        internal static int AIDrivingSpeedReduction => Get<int>("Traffic.AIDrivingSpeedReduction");

        internal static void Setup()
        {
            _configuration = Lib.Config.GetOrCreate(Assembly.GetExecutingAssembly());

            // Modules
            _configuration.SetEntry("Modules.Traffic", true, "This module contains several QoL tweaks regarding traffic and vehicles.");

            // Traffic module data
            _configuration.SetEntry("Traffic.PremiumSubscriptionBiWeeklyCost", 750, "Pay a portion of this cost each day, leads up to a total of this set price over two weeks (total / 14 per day). Repeats until canceled");
            _configuration.SetEntry("Traffic.PremiumSubscriptionCoversRepairCostPercentage", 80, "Defines by how much the repair cost is reduced percentage wise.");
            _configuration.SetEntry("Traffic.AIDrivingSpeedReduction", 20, "Defines by how much the AI drivers max speed is reduced percentage wise.");

            _configuration.Save();
        }

        internal static T Get<T>(string key) => _configuration.GetEntry<T>(key);
    }
}