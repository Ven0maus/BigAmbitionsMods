using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Venomaus.BigAmbitionsMods.Common.Helpers;

namespace Venomaus.BigAmbitionsMods.Common.Objects
{
    /// <summary>
    /// Container for configuration data from the MelonLoader mod.
    /// </summary>
    public sealed class Configuration
    {
        internal readonly string FilePath;
        internal readonly Dictionary<string, Dictionary<string, ConfigEntry>> Sections = new Dictionary<string, Dictionary<string, ConfigEntry>>();

        internal Configuration(string filePath)
        { 
            FilePath = filePath;
        }

        /// <summary>
        /// Sets a configuration entry. Category can be defined by specifying the key such as "CategoryName.EntryName".
        /// <br>By default the category will be "General" if only an EntryName is specified.</br>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="description">Optional</param>
        /// <param name="overwrite">If the value should be overwritten if it already exists.</param>
        public void SetEntry<T>(string key, T value, string description = null, bool overwrite = false)
        {
            string categoryName = "General";
            string entryName = key;

            var keyParts = key.Split('.');
            if (keyParts.Length > 1)
            {
                categoryName = keyParts[0];
                entryName = string.Join(".", keyParts.Skip(1));
            }

            var valueSerialized = ConfigurationParser.SerializeValue(value);
            Set(categoryName, entryName, valueSerialized, description, overwrite);
        }

        /// <summary>
        /// Removes the specified configuration entry, removes also the category if no more entries are present within it.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if entry or category deleted, False if entry or category does not exist.</returns>
        public bool RemoveEntry(string key)
        {
            string categoryName = "General";
            string entryName = key;

            var keyParts = key.Split('.');
            if (keyParts.Length > 1)
            {
                categoryName = keyParts[0];
                entryName = string.Join(".", keyParts.Skip(1));
            }

            bool succes = false;
            if (Sections.TryGetValue(categoryName, out var category))
            {
                succes = category.Remove(entryName);
                if (category.Values.Count == 0)
                    succes = Sections.Remove(categoryName);
            }
            return succes;
        }

        /// <summary>
        /// Collects the specified value based on the key, category must be specified in the key such as "CategoryName.EntryName" or default category will be "General".
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetEntry<T>(string key)
        {
            var categoryName = "General";
            string entryName = key;

            var keyParts = key.Split('.');
            if (keyParts.Length > 1)
            {
                categoryName = keyParts[0];
                entryName = string.Join(".", keyParts.Skip(1));
            }

            var strValue = Get(categoryName, entryName);
            if (string.IsNullOrWhiteSpace(strValue))
                return default;

            return ConfigurationParser.DeserializeValue<T>(strValue);
        }

        /// <summary>
        /// Reloads all the configuration values.
        /// </summary>
        public void Reload()
        {
            if (!File.Exists(FilePath)) return;
            var configuration = Load(FilePath);
            Clear();
            foreach (var section in configuration.Sections)
                Sections[section.Key] = section.Value;
        }

        /// <summary>
        /// Save the configuration to file.
        /// </summary>
        public void Save() => ConfigurationParser.Save(this);

        /// <summary>
        /// Removes all sections and entries from the configuration.
        /// </summary>
        public void Clear() => Sections.Clear();

        /// <summary>
        /// Laods a configuration file from the disk, useful to read other mod's configuration files.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static Configuration Load(string filePath) => ConfigurationParser.Load(filePath);

        internal void Set(string section, string key, string value, string description, bool overwrite)
        {
            if (!Sections.TryGetValue(section, out var entries))
                Sections[section] = entries = new Dictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase);

            entries.TryGetValue(key, out var existingEntry);

            if (overwrite)
            {
                // Only overwrite the value, not the description.
                entries[key] = new ConfigEntry(value, existingEntry?.Description ?? description);
            }
            else
            {
                if (existingEntry == null)
                    entries[key] = new ConfigEntry(value, description);
            }
        }

        internal string Get(string section, string key)
        {
            if (Sections.TryGetValue(section, out var entries) && entries.TryGetValue(key, out var value))
                return value.Value;
            return null;
        }

        internal sealed class ConfigEntry
        {
            public string Value { get; set; }
            public string Description { get; set; }

            public ConfigEntry(string value, string description = null)
            {
                Value = value;
                Description = description;
            }
        }
    }
}
