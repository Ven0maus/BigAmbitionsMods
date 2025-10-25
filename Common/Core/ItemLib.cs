using BigAmbitions.Items;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Venomaus.BigAmbitionsMods.Common.Core
{
    /// <summary>
    /// Helper to create and register new items within the game and retrieve them.
    /// </summary>
    public sealed class ItemLib
    {
        private static readonly Dictionary<ItemName, int> _registeredItems = new Dictionary<ItemName, int>();
        private static readonly FieldInfo _prefabCacheField = typeof(PrefabHelper).GetField("PrefabCache", BindingFlags.Static | BindingFlags.NonPublic);

        internal ItemLib()
        { }

        /// <summary>
        /// Registers the asset into the game.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns>The newly registered item name.</returns>
        public ItemName RegisterAssetAsItem(GameObject gameObject)
        {
            var instanceId = gameObject.GetInstanceID();
            if (_registeredItems.Values.Contains(instanceId))
                throw new Exception($"Asset \"{gameObject.name}\" is already registered.");

            // Setup item controller
            var ic = gameObject.GetComponent<ItemController>();
            if (ic == null)
            {
                // Add item controller and setup item
                ic = gameObject.AddComponent<ItemController>();
            }

            // Setup ItemName enum with new custom value
            var itemNameCount = ((ItemName[])Enum.GetValues(typeof(ItemName))).Select(a => (int)a).Max();
            ic.itemName = (ItemName)(itemNameCount + 1); // Register a new item type
            _registeredItems[ic.itemName] = instanceId;

            // Finally add to prefab cache so game knows about it
            AddToPrefabCache(instanceId, gameObject);

            return ic.itemName;
        }

        private void AddToPrefabCache(int instanceId, GameObject gameObject)
        {
            var cache = (Dictionary<string, UnityEngine.Object>)_prefabCacheField.GetValue(null);
            cache[instanceId.ToString()] = gameObject;
        }

        [HarmonyPatch(typeof(EnumHelpers), nameof(EnumHelpers.ToStringFast), new[] { typeof(ItemName) })]
        internal static class EnumHelpers_ToStringFast_Patch
        {
            [HarmonyPrefix]
            internal static bool Prefix(ItemName value, ref string __result)
            {
                if (_registeredItems.TryGetValue(value, out var instanceId))
                {
                    __result = instanceId.ToString();
                    return false;
                }
                return true;
            }
        }
    }
}
