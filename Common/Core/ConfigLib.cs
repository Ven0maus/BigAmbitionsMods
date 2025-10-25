using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Venomaus.BigAmbitionsMods.Common.Helpers;
using Venomaus.BigAmbitionsMods.Common.Objects;

namespace Venomaus.BigAmbitionsMods.Common.Core
{
    /// <summary>
    /// Provides methods for building and retrieving persistent melon configuration.
    /// <br/>Use this for configuration data that should persist between sessions.
    /// <br/>Do not use this for temporary or non-persistent data.</br>
    /// </summary>
    public sealed class ConfigLib
    {
        internal ConfigLib() { }

        /// <summary>
        /// Stores all possible configurations of any calling mod.
        /// </summary>
        private readonly Dictionary<string, Configuration> _configurations = new Dictionary<string, Configuration>();

        /// <summary>
        /// Returns the configuration for your mod.
        /// </summary>
        /// <param name="assembly">Your executing assembly, sometimes it must be provided incase automated stacktrace retrieval is not accurate.</param>
        /// <returns></returns>
        public Configuration GetOrCreate(Assembly assembly)
        {
            var melonName = GetMelonNameFromAssembly(assembly);

            if (!_configurations.TryGetValue(melonName, out var configuration))
            {
                var configFilePath = PathUtils.SanitizePath(Path.Combine(GetUserDataPath(), $"{melonName}.cfg"));
                _configurations[melonName] = configuration = File.Exists(configFilePath) ? 
                    Configuration.Load(configFilePath) : new Configuration(configFilePath);
            }

            return configuration;
        }

        private static string _userDataPath;
        /// <summary>
        /// Returns the path to the UserData folder of MelonLoader.
        /// </summary>
        /// <returns></returns>
        public string GetUserDataPath()
        {
            // Path to userdata folder
            return _userDataPath ?? (_userDataPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "UserData")));
        }

        /// <summary>
        /// Gets the melon's name from the specified assembly.
        /// <br>If no <see cref="MelonInfoAttribute"/> exists, the assembly name is returned instead.</br>
        /// </summary>
        /// <param name="asm"></param>
        /// <returns></returns>
        internal string GetMelonNameFromAssembly(Assembly asm)
        {
            // Try to read MelonInfoAttribute
            var attr = (MelonInfoAttribute)asm.GetCustomAttribute(typeof(MelonInfoAttribute));
            if (attr != null)
                return attr.Name;

            throw new Exception($"Invalid non MelonLoader mod assembly retrieved \"{asm.FullName}\", please provide assembly instead.");
        }
    }
}
