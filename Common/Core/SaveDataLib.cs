using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;
using ThreadState = System.Threading.ThreadState;

namespace Venomaus.BigAmbitionsMods.Common.Core
{
    /// <summary>
    /// Helper to save data to files based on the loaded savegame.
    /// </summary>
    public sealed class SaveDataLib
    {
        internal SaveDataLib() { }

        /// <summary>
        /// Raised right before any loading of the data is triggered.
        /// </summary>
        public event EventHandler<SaveFileArgs> OnBeforeLoad;
        /// <summary>
        /// Raised right after the data is loaded.
        /// </summary>
        public event EventHandler<SaveFileArgs> OnAfterLoad;
        /// <summary>
        /// Raised right before any saving of the data is triggered.
        /// </summary>
        public event EventHandler<SaveFileArgs> OnBeforeSave;
        /// <summary>
        /// Raised right after the data is saved.
        /// </summary>
        public event EventHandler<SaveFileArgs> OnAfterSave;

        // FNV prime and offset basis for 32-bit hash
        private const uint FnvPrime = 16777619;
        private const uint FnvOffsetBasis = 2166136261;

        /// <summary>
        /// Creates a unique FNV hash based on the provided string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public uint GetFnvHash(string value)
        {
            // Hash the value
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            uint hash = FnvOffsetBasis;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= FnvPrime;
            }
            return hash;
        }

        /// <summary>
        /// Returns the folder path, where mod files can be stored.
        /// </summary>
        /// <param name="hash">If provided, will append the folder for the provided hash.</param>
        /// <param name="assembly">Your executing assembly, sometimes it must be provided incase automated stacktrace retrieval is not accurate.</param>
        /// <returns></returns>
        public string GetSaveStoreFolderPath(Assembly assembly = null)
        {
            var modAssembly = assembly ?? Lib.Config.GetCallingModAssembly();
            var modName = Lib.Config.GetMelonNameFromAssembly(modAssembly);

            // Path to userdata folder
            string modFolderPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "UserData", modName, "Savestore"));

            if (!Directory.Exists(modFolderPath))
                Directory.CreateDirectory(modFolderPath);

            return modFolderPath;
        }

        /// <summary>
        /// Returns the folder path, where mod files of a specific savefile can be stored.
        /// </summary>
        /// <param name="saveFilePath"></param>
        /// <param name="assembly">Your executing assembly, sometimes it must be provided incase automated stacktrace retrieval is not accurate.</param>
        /// <returns></returns>
        internal string GetSaveStoreFolderPathForSaveFile(string saveFilePath, Assembly assembly = null)
        {
            var saveStorePath = GetSaveStoreFolderPath(assembly);
            var combined = Path.Combine(saveStorePath, GetFnvHash(saveFilePath).ToString());

            if (!Directory.Exists(combined))
                Directory.CreateDirectory(combined);

            return combined;
        }

        // LOADING
        [HarmonyPatch(typeof(SaveGameManager), nameof(SaveGameManager.Load))]
        internal static class SaveGameManager_Load
        {
            [HarmonyPrefix]
            internal static void Prefix()
            {
                var filePath = TransitionToSave.saveToLoadData.FilePath;
                MelonLogger.Msg("Invoke Before Load: " + filePath);
                Lib.SaveData.OnBeforeLoad?.Invoke(null, new SaveFileArgs(filePath));

                // Register a callback, gets auto cleared when invoked
                GlobalEvents.RegisterOnGameLoadedCallback(() =>
                {
                    MelonLogger.Msg("Invoke After Load: " + filePath);
                    Lib.SaveData.OnAfterLoad?.Invoke(null, new SaveFileArgs(filePath));
                });
            }
        }

        // SAVING
        [HarmonyPatch(typeof(SaveGameManager), nameof(SaveGameManager.Save))]
        internal static class SaveGameManager_Save
        {
            private static readonly FieldInfo _saveGameThread = typeof(SaveGameManager).GetField("_saveGameSaveThread", BindingFlags.NonPublic | BindingFlags.Static);

            [HarmonyPrefix]
            internal static void Prefix(SaveGameManager.SaveType saveType, string saveGameName = null)
            {
                if (saveType == SaveGameManager.SaveType.RecoverSave)
                    saveGameName = string.Format("Recover #{0}", SaveGameManager.Current.currentAutoSaveNumber);
                else if (saveType == SaveGameManager.SaveType.MidnightSave)
                    saveGameName = "Recover Midnight";

                var characterFolder = SaveGamePathHelper.GetCharacterFolderPath(SaveGameManager.Current.characterId);
                var saveGamePath = Path.Combine(characterFolder, saveGameName + "." + (InstanceBehavior<GameManager>.Instance.useSaveGameTypeJson ? "json" : "hsg"));

                MelonLogger.Msg("Invoke Before Save: " + saveGamePath);
                Lib.SaveData.OnBeforeSave?.Invoke(null, new SaveFileArgs(saveGamePath));
            }

            [HarmonyPostfix]
            internal static void Postfix(SaveGameManager.SaveType saveType, string saveGameName = null)
            {
                if (saveType == SaveGameManager.SaveType.RecoverSave)
                    saveGameName = string.Format("Recover #{0}", SaveGameManager.Current.currentAutoSaveNumber);
                else if (saveType == SaveGameManager.SaveType.MidnightSave)
                    saveGameName = "Recover Midnight";

                var characterFolder = SaveGamePathHelper.GetCharacterFolderPath(SaveGameManager.Current.characterId);
                var saveGamePath = Path.Combine(characterFolder, saveGameName + "." + (InstanceBehavior<GameManager>.Instance.useSaveGameTypeJson ? "json" : "hsg"));

                MelonCoroutines.Start(ExecuteAfterSave(saveGamePath));
            }

            private static IEnumerator ExecuteAfterSave(string filePath)
            {
                var thread = (Thread)_saveGameThread.GetValue(null);
                if (thread != null)
                {
                    MelonLogger.Msg("Waiting on save completion.");
                    while (thread.ThreadState == ThreadState.Running)
                        yield return new WaitForEndOfFrame();
                }
                MelonLogger.Msg("Invoke After Save: " + filePath);
                Lib.SaveData.OnAfterSave?.Invoke(null, new SaveFileArgs(filePath));
            }
        }

        /// <summary>
        /// The event args for save/load events.
        /// </summary>
        public sealed class SaveFileArgs : EventArgs
        {
            /// <summary>
            /// The path of the save file, don't use this to save your custom mod files, use <see cref="GetSaveStoreFolderPath"/> instead.
            /// </summary>
            public string SaveFilePath { get; }

            internal SaveFileArgs(string saveFilePath)
            {
                SaveFilePath = saveFilePath;
            }

            private void CreateMetadataFile(Assembly assembly = null)
            {
                var metadataPath = Path.Combine(Lib.SaveData.GetSaveStoreFolderPathForSaveFile(SaveFilePath, assembly), "metadata.meta");
                if (!File.Exists(metadataPath))
                {
                    var metadata = new
                    {
                        SaveFilePath
                    };

                    try
                    {
                        File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));
                    }
                    catch (Exception e)
                    {
                        Melon<Mod>.Logger.Msg($"Unable to create metadata.meta file at location \"{metadataPath}\": {e.Message}");
                    }
                }
            }

            /// <summary>
            /// The path to the savestore folder for this specific savefile, where you can place your mod files.
            /// </summary>
            /// <param name="assembly">Your executing assembly, sometimes it must be provided incase automated stacktrace retrieval is not accurate.</param>
            /// <returns></returns>
            public string GetSaveStoreFolderPath(Assembly assembly = null)
            {
                CreateMetadataFile(assembly);
                return Lib.SaveData.GetSaveStoreFolderPathForSaveFile(SaveFilePath, assembly);
            }
        }
    }
}
