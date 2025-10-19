using BigAmbitions.GameAnalytics;
using BigAmbitions.Items;
using BigAmbitions.SoundSystem;
using Extensions;
using HarmonyLib;
using Helpers;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Streets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UI.Elements;
using UI.Notification;
using UI.Overlays;
using UnityEngine;
using Vehicles.VehicleTypes;
using Venomaus.BigAmbitionsMods.Common.Core;

namespace Venomaus.BigAmbitionsMods.QoLTweaks.Modules.Traffic
{
    internal static class GasStationOverylay_Patches
    {
        private static readonly FieldInfo _gasStationTriggerField = typeof(GasStationOverlay).GetField("_currentStationTrigger", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Dictionary<Address, string> _subscriptions = new Dictionary<Address, string>();
        private static Dictionary<Address, float> _fuelPrices = new Dictionary<Address, float>();

        private const string Seperator = "|&|";

        /// <summary>
        /// Save data to the disk within the savefile
        /// </summary>
        internal static void Save(SaveDataLib.SaveFileArgs e)
        {
            try
            {
                var data = new
                {
                    Subscriptions = _subscriptions.Select(a => $"{a.Key}{Seperator}{a.Value}").ToArray(),
                    FuelPrices = _fuelPrices.Select(a => $"{a.Key}{Seperator}{a.Value}").ToArray()
                };
                var result = JsonConvert.SerializeObject(data, Formatting.None);
                var path = e.GetSaveStorePath(Path.Combine("Traffic", "GasStationData.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(e.GetSaveStorePath(Path.Combine("Traffic", "GasStationData.json")), result);

                Melon<Mod>.Logger.Msg("Saved gas station data.");
            }
            catch (Exception ex)
            {
                Melon<Mod>.Logger.Msg("Error while saving gas station data: " + ex.Message);
            }
        }

        /// <summary>
        /// Load data to the disk within the savefile
        /// </summary>
        internal static void Load(SaveDataLib.SaveFileArgs e)
        {
            var filePath = e.GetSaveStorePath(Path.Combine("Traffic", "GasStationData.json"));
            if (File.Exists(filePath))
            {
                var result = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<JObject>(result);

                var splitter = new[] { Seperator };
                var subscriptions = data["Subscriptions"]?.ToObject<string[]>() ?? Array.Empty<string>();
                _subscriptions = subscriptions.Select(a =>
                {
                    var parts = a.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                    var streetNameParts = parts[0].Split(' ');
                    return (new Address((StreetName)Enum.Parse(typeof(StreetName), streetNameParts[0]), int.Parse(streetNameParts[1])), parts[1]);
                }).ToDictionary(a => a.Item1, a => a.Item2);

                var fuelPrices = data["FuelPrices"]?.ToObject<string[]>() ?? Array.Empty<string>();
                _fuelPrices = fuelPrices.Select(a =>
                {
                    var parts = a.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                    var streetNameParts = parts[0].Split(' ');
                    return (new Address((StreetName)Enum.Parse(typeof(StreetName), streetNameParts[0]), int.Parse(streetNameParts[1])), float.Parse(parts[1]));
                }).ToDictionary(a => a.Item1, a => a.Item2);

                Melon<Mod>.Logger.Msg("Loaded gas station data.");
            }
        }

        /// <summary>
        /// Adds a premium subscription to the gas station overlay, that charges a static amount each day and provides a % cost reduction on all repairs.
        /// </summary>
        [HarmonyPatch(typeof(GasStationOverlay), nameof(GasStationOverlay.GetButtons))]
        internal static class GasStationOverlay_GetButtons
        {
            private static readonly FieldInfo _argumentsField = typeof(ButtonInfo).GetField("arguments", BindingFlags.Public | BindingFlags.Instance);
            private static readonly FieldInfo _keyField = typeof(ButtonInfo).GetField("key", BindingFlags.Public | BindingFlags.Instance);
            private static readonly int _subscriptionCost = Mathf.RoundToInt(ModConfiguration.PremiumSubscriptionBiWeeklyCost.Value / 14f);

            [HarmonyPostfix]
            internal static void Postfix(GasStationOverlay __instance, ref ButtonInfo[] __result)
            {
                // Find which gas station we're at.
                var gasStationTriger = (GasStationTrigger)_gasStationTriggerField.GetValue(__instance);
                if (gasStationTriger == null) return;
                if (gasStationTriger.cbc == null || gasStationTriger.cbc.building == null) return;

                var tempList = __result.ToList();
                AdjustButtonsForPremiumSubscription(gasStationTriger, tempList);
                AdjustButtonsForRefuelPrices(gasStationTriger, tempList);

                // Adjust final result with new temp list changes
                __result = tempList.ToArray();
            }

            private static void AdjustButtonsForRefuelPrices(GasStationTrigger gasStationTrigger, List<ButtonInfo> tempList)
            {
                var refuelButton = tempList.Find(b => b.name == "FillUpVehicle");
                if (refuelButton != null)
                {
                    var address = gasStationTrigger.cbc.building.Address;
                    if (!_fuelPrices.TryGetValue(address, out var pricePerLiter))
                    {
                        // Generate a new price if none exist yet for this station
                        _fuelPrices[address] = pricePerLiter = UnityEngine.Random.Range(0.8f, 2.2f);
                    }

                    // We need to change the arguments to the correct price
                    if (_argumentsField != null)
                    {
                        VehicleController vehicle = InstanceBehavior<GameManager>.Instance.selectedVehicle;
                        if (vehicle == null)
                            return;

                        var missingFuel = (vehicle.vehicleType.maxFuel - vehicle.GetCurrentFuel());
                        float fuelCost = missingFuel * pricePerLiter;

                        var newArgs = new 
                        { 
                            liters = missingFuel.ToString("F2"),
                            price = fuelCost.ToShortCurrencyFormat(false),
                            pricePerLiter = pricePerLiter.ToString("C2", GenericExtensions.cultureInfo)
                        };
                        _argumentsField.SetValue(refuelButton, newArgs); // Update args via reflection since its a readonly field
                    }

                    // Adjust all the key to contain the liter price
                    _keyField.SetValue(refuelButton, "Refuel {liters}L ({price} | {pricePerLiter}/L)");
                }

                // Adjust jerry can price cost based on liter price today
                var jerryCanButton = tempList.Find(b => b.name == "BuyJerryCan");
                if (jerryCanButton != null)
                {
                    var address = gasStationTrigger.cbc.building.Address;
                    if (!_fuelPrices.TryGetValue(address, out var pricePerLiter))
                    {
                        // Generate a new price if none exist yet for this station
                        _fuelPrices[address] = pricePerLiter = UnityEngine.Random.Range(0.8f, 2.2f);
                    }

                    // Jerry can is always 10L
                    if (_argumentsField != null)
                    {
                        var newArgs = new
                        {
                            price = (10 * pricePerLiter).ToShortCurrencyFormat(false)
                        };
                        _argumentsField.SetValue(jerryCanButton, newArgs); // Update args via reflection since its a readonly field
                    }

                    _keyField.SetValue(jerryCanButton, "Jerry Can 10L ({price})");
                }
            }

            private static void AdjustButtonsForPremiumSubscription(GasStationTrigger gasStationTriger, List<ButtonInfo> tempList)
            {
                var repairButton = tempList.Find(a => a.name == "RepairVehicle");
                if (repairButton != null)
                {
                    // Expand with premium subscription button if we can repair vehicles
                    var buildingAddress = gasStationTriger.cbc.building.Address;
                    var premiumSubscriptionButton = PremiumSubscriptionButton(gasStationTriger, buildingAddress, out var hasPremiumSubscription);
                    tempList.Insert(0, premiumSubscriptionButton);

                    if (hasPremiumSubscription)
                    {
                        // We need to change the arguments to the correct price if we have the subscription
                        var argsField = typeof(ButtonInfo).GetField("arguments", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (argsField != null)
                        {
                            // Apply price reduction
                            float price = VehicleHelper.GetCurrentVehicle().CalculateRepairCost();
                            price *= 1.0f - ModConfiguration.PremiumSubscriptionCoversRepairCostPercentage.Value / 100f;

                            var newArgs = new { price = price.ToShortCurrencyFormat(false) };
                            argsField.SetValue(repairButton, newArgs); // Update args via reflection since its a readonly field
                        }
                    }
                }
            }

            private static ButtonInfo PremiumSubscriptionButton(GasStationTrigger gasStationTrigger, Address buildingAddress, out bool hasPremiumSubscription)
            {
                // Collect data on premium subscription
                hasPremiumSubscription = _subscriptions.ContainsKey(buildingAddress);
                if (!hasPremiumSubscription)
                {
                    // Initial cost, but also an automated daily cost
                    return new ButtonInfo("Buy Premium Subscription", "Buy Premium Subscription ({initial} | {daily}/day)", new
                    {
                        initial = ModConfiguration.PremiumSubscriptionBiWeeklyCost.Value.ToShortCurrencyFormat(false),
                        daily = _subscriptionCost.ToShortCurrencyFormat(false)
                    }, "blue", () => BuyPremiumSubscription(gasStationTrigger, buildingAddress, ModConfiguration.PremiumSubscriptionBiWeeklyCost.Value), PlayerAction.Interact, true);
                }
                else
                {
                    // No cost, cancels daily cost.
                    return new ButtonInfo("Cancel Premium Subscription", "Cancel Premium Subscription", "blue", 
                        () => CancelPremiumSubscription(gasStationTrigger, buildingAddress), PlayerAction.Interact, true);
                }
            }

            private static void BuyPremiumSubscription(GasStationTrigger gasStationTrigger, Address buildingAddress, int price)
            {
                var data = new Transaction.DataHolder
                {
                    businessName = gasStationTrigger.cbc.buildingRegistration.BusinessName
                };

                if (GameManager.ChangeMoneySafe(-price, Transaction.TransactionType.ItemPurchase, data, null, null, false, true))
                {
                    InstanceBehavior<SfxManager>.Instance.PlayAudio(SoundType.PurchaseSuccess, gasStationTrigger.transform.position, 1f, true, null, -1f);
                    _subscriptions[buildingAddress] = gasStationTrigger.cbc.buildingRegistration.BusinessName;
                    GameEvent.Undefined.Invoke();
                }

                GasStationOverlay.Show(gasStationTrigger);
            }

            internal static void CancelPremiumSubscription(GasStationTrigger gasStationTrigger, Address address)
            {
                if (_subscriptions.Remove(address))
                    GameEvent.Undefined.Invoke();

                if (gasStationTrigger != null)
                    GasStationOverlay.Show(gasStationTrigger);
            }

            /// <summary>
            /// Updates all known fuel prices.
            /// </summary>
            internal static void UpdateFuelPrices()
            {
                // Adjusts all fuel prices by following the daily trend movements
                foreach (var kvp in _fuelPrices.ToDictionary(a => a.Key, a => a.Value))
                {
                    _fuelPrices[kvp.Key] = GenerateNextFuelPrice(kvp.Value);
                }
            }

            /// <summary>
            /// Handles all daily costs for premium subscriptions.
            /// </summary>
            internal static void HandlePremiumSubscriptionCosts()
            {
                foreach (var subscription in _subscriptions)
                {
                    var data = new Transaction.DataHolder
                    {
                        businessName = subscription.Value
                    };

                    var cost = _subscriptionCost;
                    if (GameManager.ChangeMoneySafe(-cost, Transaction.TransactionType.ItemPurchase, data, SaveGameManager.Current.Day, subscription.Key, false, false))
                    {
                        GameEvent.Undefined.Invoke();
                    }
                    else
                    {
                        // Cancel the subscription when not enough money
                        CancelPremiumSubscription(null, subscription.Key);

                        // Show a notification in-game about the cancelled subscription
                        Notifications.Show(NotificationType.Info, $"Insufficient funds to pay \"{subscription.Value} Premium\", subscription was automatically cancelled.", secondsToShow: 10f);
                    }
                }
            }

            private static float GenerateNextFuelPrice(float currentPrice)
            {
                // Define long-term trend movement (slow)
                const float TREND_STEP = 0.002f; // ~0.2 cents per day
                const float RANDOM_FLUCTUATION = 0.015f; // ±1.5 cents per day
                const float MIN_PRICE = 0.80f;
                const float MAX_PRICE = 2.20f;

                int dayOfYear = (SaveGameManager.Current.Day % 365);
                if (dayOfYear == 0)
                    dayOfYear = 365;

                // Slowly oscillate global trend every ~15-30 days
                float trendDirection = (float)Math.Sin(dayOfYear / 20.0);
                float trend = trendDirection * TREND_STEP;

                // Add small random fluctuation
                float noise = UnityEngine.Random.Range(-RANDOM_FLUCTUATION, RANDOM_FLUCTUATION);

                // Compute new price
                float newPrice = currentPrice + trend + noise;

                // Clamp and smooth
                return Mathf.Clamp(newPrice, MIN_PRICE, MAX_PRICE);
            }
        }

        /// <summary>
        /// Adapt the repair cost based on if we have a subscription or not
        /// </summary>
        [HarmonyPatch(typeof(GasStationOverlay), "Repair")]
        internal static class GasStationOverlay_Repair
        {
            [HarmonyPrefix]
            internal static bool Prefix(GasStationOverlay __instance)
            {
                // Find which gas station we're at.
                var gasStationTrigger = (GasStationTrigger)_gasStationTriggerField.GetValue(__instance);
                if (gasStationTrigger == null) return false;
                if (gasStationTrigger.cbc == null) return false;

                // If we don't own premium, then execute base method normally
                if (!_subscriptions.ContainsKey(gasStationTrigger.cbc.building.Address))
                    return true;

                VehicleController vehicle = InstanceBehavior<GameManager>.Instance.selectedVehicle;
                if (!(vehicle == null))
                {
                    VehicleTypeName vehicleTypeName = vehicle.vehicleType.vehicleTypeName;
                    if (vehicleTypeName != VehicleTypeName.HandTruck && vehicleTypeName != VehicleTypeName.Flatbed)
                    {
                        float repairCost = vehicle.vehicleInstance.CalculateRepairCost();
                        if (Mathf.RoundToInt(repairCost) == 0)
                        {
                            return false;
                        }

                        // a % of the repair cost is covered by the subscription.
                        repairCost *= 1.0f - ModConfiguration.PremiumSubscriptionCoversRepairCostPercentage.Value / 100f;

                        if (GameManager.ChangeMoneySafe(-repairCost, Transaction.TransactionType.ItemPurchase, new Transaction.DataHolder
                        {
                            businessName = gasStationTrigger.cbc.buildingRegistration.BusinessName
                        }, null, null, false, true))
                        {
                            InstanceBehavior<SfxManager>.Instance.PlayAudio(SoundType.CarRepair, gasStationTrigger.transform.position, 1f, true, null, -1f);
                            vehicle.Repair();
                            SaveGameManager.Current.achievementsData.totalRepairCost += repairCost;
                            GameEvent.Undefined.Invoke();
                        }
                        GasStationOverlay.Show(gasStationTrigger);
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Adapt the refuel cost based on daily prices
        /// </summary>
        [HarmonyPatch(typeof(GasStationOverlay), "Refuel")]
        internal static class GasStationOverlay_Refuel
        {
            [HarmonyPrefix]
            internal static bool Prefix(GasStationOverlay __instance)
            {
                VehicleController vehicle = InstanceBehavior<GameManager>.Instance.selectedVehicle;
                if (vehicle == null)
                    return false;

                // Find which gas station we're at.
                var gasStationTrigger = (GasStationTrigger)_gasStationTriggerField.GetValue(__instance);
                if (gasStationTrigger == null) return false;
                if (gasStationTrigger.cbc == null) return false;

                var address = gasStationTrigger.cbc.building.Address;
                if (!_fuelPrices.TryGetValue(address, out var pricePerLiter))
                {
                    // Generate a new price if none exist yet for this station
                    _fuelPrices[address] = pricePerLiter = UnityEngine.Random.Range(0.8f, 2.2f);
                }

                float fuelCost = (vehicle.vehicleType.maxFuel - vehicle.GetCurrentFuel()) * pricePerLiter;
                if (Mathf.RoundToInt(fuelCost) == 0)
                    return false;

                if (GameManager.ChangeMoneySafe(-fuelCost, Transaction.TransactionType.ItemPurchase, new Transaction.DataHolder
                {
                    businessName = gasStationTrigger.cbc.buildingRegistration.BusinessName
                }, null, null, false, true))
                {
                    InstanceBehavior<SfxManager>.Instance.PlayAudio(SoundType.CarRefuel, gasStationTrigger.transform.position, 1f, true, null, -1f);
                    vehicle.SetFuel(vehicle.vehicleType.maxFuel);
                    SaveGameManager.Current.achievementsData.totalGasCost += fuelCost;
                    GameEvent.Undefined.Invoke();
                }
                GasStationOverlay.Show(gasStationTrigger);
                return false;
            }
        }

        /// <summary>
        /// Adapt the JerryCan cost based on daily prices
        /// </summary>
        [HarmonyPatch(typeof(GasStationOverlay), "BuyJerryCan")]
        internal static class GasStationOverlay_BuyJerryCan
        {
            [HarmonyPrefix]
            internal static bool Prefix(GasStationOverlay __instance)
            {
                VehicleController vehicle = InstanceBehavior<GameManager>.Instance.selectedVehicle;

                // Find which gas station we're at.
                var gasStationTrigger = (GasStationTrigger)_gasStationTriggerField.GetValue(__instance);
                if (gasStationTrigger == null) return false;
                if (gasStationTrigger.cbc == null) return false;

                var address = gasStationTrigger.cbc.building.Address;
                if (!_fuelPrices.TryGetValue(address, out var pricePerLiter))
                {
                    // Generate a new price if none exist yet for this station
                    _fuelPrices[address] = pricePerLiter = UnityEngine.Random.Range(0.8f, 2.2f);
                }

                // Jerry can is always 10L
                var fuelCost = 10 * pricePerLiter;

                if (vehicle == null)
                {
                    if (PlayerHelper.ItemInHands != null)
                    {
                        Notifications.ShowError("playeritempurchaser_notification_hands_full", null, true);
                        return false;
                    }
                    if (GameManager.ChangeMoneySafe(-fuelCost, Transaction.TransactionType.ItemPurchase, new Transaction.DataHolder
                    {
                        businessName = gasStationTrigger.cbc.buildingRegistration.BusinessName
                    }, null, null, false, true))
                    {
                        InstanceBehavior<SfxManager>.Instance.PlayAudio(SoundType.PurchaseSuccess, gasStationTrigger.transform.position, 1f, true, null, -1f);
                        ItemInstance itemInstance = ItemHelper.InitializeNewInstance(ItemName.JerryCan);
                        itemInstance.priceOnPurchase = fuelCost;
                        PlayerHelper.ItemInHands = itemInstance;
                        SaveGameManager.Current.achievementsData.totalGasCost += fuelCost;
                        GameEvent.Undefined.Invoke();
                    }
                }
                else
                {
                    if (vehicle.vehicleInstance.cargoInstances.Count >= vehicle.vehicleType.maxCargoCapacity)
                    {
                        Notifications.Show(NotificationType.Error, "managecargo_notification_vehicle_full", new NotificationData
                        {
                            type = vehicle.GetName().ToString()
                        }, 4f, null, null, true, true);
                        return false;
                    }
                    if (GameManager.ChangeMoneySafe(-fuelCost, Transaction.TransactionType.ItemPurchase, new Transaction.DataHolder
                    {
                        businessName = gasStationTrigger.cbc.buildingRegistration.BusinessName
                    }, null, null, false, true))
                    {
                        CargoInstance cargoInstance = new CargoInstance(ItemName.JerryCan, 1, fuelCost);
                        vehicle.vehicleInstance.AddToCargo(cargoInstance);
                        SaveGameManager.Current.achievementsData.totalGasCost += fuelCost;
                        GameEvent.Undefined.Invoke();
                    }
                }
                GameAnalytics.TrackBuyJerryCan(gasStationTrigger.cbc.buildingRegistration.Address.ToAnalyticsString());
                GasStationOverlay.Show(gasStationTrigger);
                return false;
            }
        }
    }
}
