using GleyTrafficSystem;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using UnityEngine;

namespace Venomaus.BigAmbitionsMods.QoLTweaks.Modules.Traffic
{
    internal class DrivingAI_Patches
    {
        /// <summary>
        /// Adds a randomized delay before cars start moving once a red light turns green (realism)
        /// </summary>
        [HarmonyPatch(typeof(DrivingAI), "StopStateChanged")]
        internal static class DrivingAI_StopStateChanged
        {
            private static readonly MethodInfo _newDriveActionArrived = typeof(DrivingAI).GetMethod("NewDriveActionArrived",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            [HarmonyPrefix]
            internal static bool Prefix(DrivingAI __instance, int index, bool stopState)
            {
                // Red light -> keep normal stop behavior
                if (stopState)
                    return true;

                // Green light -> delay reaction
                float delay = UnityEngine.Random.Range(0.5f, 1.5f);
                MelonCoroutines.Start(ResumeAfterDelay(__instance, index, delay));

                return false; // Skip original method
            }

            private static IEnumerator ResumeAfterDelay(DrivingAI ai, int index, float delay)
            {
                yield return new WaitForSeconds(delay);
                if (ai == null) yield break;

                try
                {
                    _newDriveActionArrived?.Invoke(ai, new object[] { index, SpecialDriveActionTypes.StopInPoint, false });
                }
                catch(Exception)
                {
                    // Its possible the target waypoint no longer exists when transporting to a repair station.
                }
            }
        }

        /// <summary>
        /// Reduces the speed for the driving ai by 15%
        /// </summary>
        [HarmonyPatch(typeof(DrivingAI), "ComputeMaxPossibleSpeed")]
        public static class DrivingAI_SpeedReductionPatch
        {
            [HarmonyPostfix]
            internal static void Postfix(ref float __result)
            {
                __result *= 1.0f - ModConfiguration.AIDrivingSpeedReduction.Value / 100f;
            }
        }

        /// <summary>
        /// Increase driving actions of cars to a more realistic feeling.
        /// </summary>
        [HarmonyPatch]
        public static class DrivingAI_RealisticDistancePatch
        {
            private static readonly FieldInfo _trafficVehiclesField = typeof(DrivingAI).GetField("trafficVehicles", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo _vehicleTypesField = typeof(TrafficManager).GetField("vehicleType", BindingFlags.NonPublic | BindingFlags.Instance);

            // ThreadStatic ensures thread safety if Unity ever calls on multiple threads
            [System.ThreadStatic] private static int _currentIndex;

            // Patch the three methods that call GetActionValue(int)
            [HarmonyPrefix]
            [HarmonyPatch(typeof(DrivingAI), "ApplyAction")]
            [HarmonyPatch(typeof(DrivingAI), "applyCurrentActiveAction")]
            [HarmonyPatch(typeof(DrivingAI), "VehicleActivated")]
            internal static void CaptureIndexPrefix(int index)
            {
                _currentIndex = index;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DrivingAI), "GetActionValue")]
            internal static bool Prefix(DrivingAI __instance, SpecialDriveActionTypes action, ref float __result)
            {
                __result = CalculateActionValue(__instance, action, _currentIndex);
                return false; // skip original
            }

            private static float CalculateActionValue(DrivingAI instance, SpecialDriveActionTypes action, int index)
            {
                TrafficVehicles trafficVehicles = (TrafficVehicles)_trafficVehiclesField.GetValue(instance);
                float vehicleLength = trafficVehicles.GetVehicleLength(index);

                var vehicleTypesArray = (NativeArray<VehicleTypes>)_vehicleTypesField.GetValue(TrafficManager.Instance);
                VehicleTypes vehicleType = vehicleTypesArray[index];

                float typeFactor = GetVehicleTypeFactor(vehicleType);
                float lengthFactor = vehicleLength * 0.5f;

                // subtle randomness (±20%)
                float randomFactor = UnityEngine.Random.Range(0.8f, 1f);

                // Normal actions: no change
                if (action == SpecialDriveActionTypes.Forward) return 0f;
                if (action == SpecialDriveActionTypes.Follow) return 1f;
                if (action == SpecialDriveActionTypes.TempStop) return 5f;

                // StopInDistance: lower number → more space
                if (action == SpecialDriveActionTypes.StopInDistance)
                {
                    float baseValue = UnityEngine.Random.Range(2.5f, 6f);
                    float scaled = baseValue / (1f + 0.5f * lengthFactor * typeFactor);
                    return scaled * randomFactor;
                }

                // StopNow: very urgent stop, smallest value
                if (action == SpecialDriveActionTypes.StopNow)
                {
                    float baseValue = UnityEngine.Random.Range(3f, 6f);
                    float scaled = baseValue / (1f + 0.5f * lengthFactor * typeFactor);
                    return scaled * randomFactor;
                }

                // Reverse / AvoidReverse
                if (action == SpecialDriveActionTypes.Reverse || action == SpecialDriveActionTypes.AvoidReverse)
                {
                    return 2f;
                }

                return float.PositiveInfinity;
            }

            private static float GetVehicleTypeFactor(VehicleTypes vehicleType)
            {
                if (vehicleType == VehicleTypes.Truck)
                    return 1.5f;
                return 1f;
            }
        }
    }
}
