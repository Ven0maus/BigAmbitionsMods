using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Diagnostics;
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
        /// <returns></returns>
        public string GetSaveStorePath()
        {
            var modAssembly = GetCallingModAssembly();
            var modName = GetModNameFromAssembly(modAssembly);

            // Path to userdata folder
            string modFolderPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "UserData", modName, "Savestore"));
            if (!Directory.Exists(modFolderPath))
                Directory.CreateDirectory(modFolderPath);
            return modFolderPath;
        }

        private static Assembly GetCallingModAssembly()
        {
            StackTrace trace = new StackTrace();
            foreach (var frame in trace.GetFrames())
            {
                MethodBase method = frame.GetMethod();
                Assembly asm = method.DeclaringType?.Assembly;

                if (asm == null || asm == Assembly.GetExecutingAssembly())
                    continue;

                // First assembly that is not Common.dll is likely the mod
                return asm;
            }

            return Assembly.GetCallingAssembly(); // fallback
        }

        private static string GetModNameFromAssembly(Assembly asm)
        {
            // Try to read MelonInfoAttribute
            var attr = (MelonInfoAttribute)asm.GetCustomAttribute(typeof(MelonInfoAttribute));
            if (attr != null)
                return attr.Name;

            // fallback: assembly name
            return asm.GetName().Name;
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

        public sealed class SaveFileArgs : EventArgs
        {
            /// <summary>
            /// A unique FNV hash that is generated from the loaded savegamefile's path.
            /// <br>Append to your custom data files like {filename}_{hash}.extension that way you can find it back on load easily.</br>
            /// </summary>
            public string Hash { get; }

            internal SaveFileArgs(string path)
            {
                Hash = Lib.SaveData.GetFnvHash(path).ToString();
            }

            /// <summary>
            /// Returns a path of the specified filename to the savestore linked to this savefile.
            /// <br>Will look something like UserData/Mod/Savestore/{hash}_{filenameWithExtension}</br>
            /// <br>The directory is automatically created.</br>
            /// </summary>
            /// <param name="filenameWithExtension">The filename you want to store</param>
            /// <returns></returns>
            public string GetSaveStorePath(string filenameWithExtension)
            {
                string basePath = Lib.SaveData.GetSaveStorePath();

                // Normalize input (remove any leading/trailing slashes)
                filenameWithExtension = filenameWithExtension.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Split path into directory and filename
                string directory = Path.GetDirectoryName(filenameWithExtension);
                string filename = Path.GetFileName(filenameWithExtension);

                // Prefix hash to the filename
                string hashedFilename = $"{Hash}_{filename}";

                // Combine everything back
                string fullPath = directory != null && directory.Length > 0
                    ? Path.Combine(basePath, directory, hashedFilename)
                    : Path.Combine(basePath, hashedFilename);

                // Ensure directory exists
                string folder = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return fullPath;
            }
        }
    }
}
