using GleyTrafficSystem;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Venomaus.BigAmbitionsMods.QoLTweaks.Modules.Traffic
{
    internal class TrafficLight_Patches
    {
        /// <summary>
        /// Adds a small delay between red to green change. (realism)
        /// </summary>
        [HarmonyPatch(typeof(TrafficLightsIntersection), nameof(TrafficLightsIntersection.UpdateIntersection))]
        internal class TrafficLightsIntersection_UpdateIntersection
        {
            // Static cached reflection objects
            private static readonly FieldInfo _yellowLightField = AccessTools.Field(typeof(TrafficLightsIntersection), "yellowLight");
            private static readonly FieldInfo _currentTimeField = AccessTools.Field(typeof(TrafficLightsIntersection), "currentTime");
            private static readonly FieldInfo _yellowTimeField = AccessTools.Field(typeof(TrafficLightsIntersection), "yellowLightTime");
            private static readonly FieldInfo _currentRoadField = AccessTools.Field(typeof(TrafficLightsIntersection), "currentRoad");
            private static readonly FieldInfo _stopUpdateField = AccessTools.Field(typeof(TrafficLightsIntersection), "stopUpdate");
            private static readonly FieldInfo _carsInIntersectionField = AccessTools.Field(typeof(TrafficLightsIntersection), "carsInIntersection");
            private static readonly FieldInfo _exitWaypointsField = AccessTools.Field(typeof(TrafficLightsIntersection), "exitWaypoints");

            private static readonly MethodInfo _changeColorsMethod = AccessTools.Method(typeof(TrafficLightsIntersection), "ChangeCurrentRoadColors");
            private static readonly MethodInfo _applyChangesMethod = AccessTools.Method(typeof(TrafficLightsIntersection), "ApplyColorChanges");
            private static readonly MethodInfo _getValidMethod = AccessTools.Method(typeof(TrafficLightsIntersection), "GetValidValue");

            [HarmonyPrefix]
            internal static bool Prefix(TrafficLightsIntersection __instance, float realtimeSinceStartup)
            {
                bool yellowLight = (bool)_yellowLightField.GetValue(__instance);
                bool stopUpdate = (bool)_stopUpdateField.GetValue(__instance);
                float currentTime = (float)_currentTimeField.GetValue(__instance);
                float yellowTime = (float)_yellowTimeField.GetValue(__instance);
                int currentRoad = (int)_currentRoadField.GetValue(__instance);

                if (stopUpdate)
                    return true;

                var carsInIntersection = (ICollection)_carsInIntersectionField.GetValue(__instance);
                var exitWaypoints = (ICollection)_exitWaypointsField.GetValue(__instance);

                if (yellowLight && (realtimeSinceStartup - currentTime > yellowTime) &&
                    (carsInIntersection.Count == 0 || exitWaypoints.Count == 0))
                {
                    MelonCoroutines.Start(DelayedGreenRoutine(__instance, currentRoad, realtimeSinceStartup));
                    return false;
                }

                return true;
            }

            private static IEnumerator DelayedGreenRoutine(TrafficLightsIntersection instance, int currentRoad, float realtimeSinceStartup)
            {
                // First, turn current road red
                _changeColorsMethod.Invoke(instance, new object[] { currentRoad, Enum.Parse(typeof(TrafficLightsColor), "Red") });
                _applyChangesMethod.Invoke(instance, null);

                // Wait 2-3 seconds
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 3f));

                // Switch to next green road
                int nextRoad = (int)_getValidMethod.Invoke(instance, new object[] { currentRoad + 1 });
                _currentRoadField.SetValue(instance, nextRoad);
                _changeColorsMethod.Invoke(instance, new object[] { nextRoad, Enum.Parse(typeof(TrafficLightsColor), "Green") });

                // Update flags
                _yellowLightField.SetValue(instance, false);
                _currentTimeField.SetValue(instance, realtimeSinceStartup);
                _applyChangesMethod.Invoke(instance, null);
            }
        }
    }
}
