using MelonLoader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        public Configuration GetOrCreate(Assembly assembly = null)
        {
            var asm = assembly ?? GetCallingModAssembly();
            var melonName = GetMelonNameFromAssembly(asm);

            if (!_configurations.TryGetValue(melonName, out var configuration))
            {
                var configFilePath = Path.Combine(GetUserDataPath(), $"{melonName}.cfg");
                _configurations[melonName] = configuration = File.Exists(configFilePath) ? 
                    Configuration.Load(configFilePath) : new Configuration(configFilePath);
            }

            return configuration;
        }

        /// <summary>
        /// Returns the path to the UserData folder of MelonLoader.
        /// </summary>
        /// <returns></returns>
        public string GetUserDataPath()
        {
            // Path to userdata folder
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "UserData"));
        }

        // Cache per detected mod assembly
        private static readonly ConcurrentDictionary<string, Assembly> _modAssemblyCache = new ConcurrentDictionary<string, Assembly>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal Assembly GetCallingModAssembly()
        {
            // Fast path: calling assembly
            var callingAsm = Assembly.GetCallingAssembly();
            var key = callingAsm.FullName;

            // Already cached? return immediately
            if (_modAssemblyCache.TryGetValue(key, out var cached))
                return cached;

            // Slow path: need to actually resolve via stack trace
            var resolvedAsm = ResolveCaller();

            // Pick the most accurate result
            var finalAsm = resolvedAsm ?? callingAsm;

            // Cache for next time
            _modAssemblyCache[finalAsm.FullName] = finalAsm;

            return finalAsm;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Assembly ResolveCaller()
        {
            var thisAsm = Assembly.GetExecutingAssembly();
            var trace = new StackTrace(false);

            foreach (var frame in trace.GetFrames())
            {
                var method = frame.GetMethod();
                var asm = method?.DeclaringType?.Assembly;
                if (asm == null)
                    continue;

                // Skip this library
                if (asm == thisAsm)
                    continue;

                // Skip system/framework assemblies
                var name = asm.GetName().Name;
                if (name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                    || name == "mscorlib" 
                    || name == "netstandard")
                    continue;

                return asm;
            }

            return null;
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
