using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Venomaus.BigAmbitionsMods.Common.Objects;

namespace Venomaus.BigAmbitionsMods.Common.Helpers
{
    /// <summary>
    /// Helper class to parse configuration data.
    /// </summary>
    internal static class ConfigurationParser
    {
        /// <summary>
        /// Parses the specified file into a configuration object.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        internal static Configuration Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception($"File \"{filePath}\" does not exist.");

            if (!Path.GetExtension(filePath).Equals(".cfg", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"File \"{filePath}\" is not a valid configuration file.");

            var configuration = new Configuration(filePath);
            string currentSection = null;
            string pendingDescription = null;

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();

                // Skip completely empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    pendingDescription = null;
                    continue;
                }

                // Comment line (acts as description)
                if (line.StartsWith("#"))
                {
                    // Store comment text without the prefix
                    string commentText = line.TrimStart('#', ' ').Trim();
                    // Append to existing pending description (multi-line comment support)
                    pendingDescription = pendingDescription == null
                        ? commentText
                        : $"{pendingDescription}\n{commentText}";
                    continue;
                }

                // Section header
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    pendingDescription = null; // reset when entering new section
                    continue;
                }

                // Key-value pair
                var parts = line.Split('=');
                if (parts.Length == 2 && currentSection != null)
                {
                    var key = parts[0].Trim();
                    var value = string.Join("=", parts.Skip(1)).Trim();

                    configuration.Set(currentSection, key, value, pendingDescription, true);
                    pendingDescription = null;
                }
            }

            return configuration;
        }

        /// <summary>
        /// Writes the specified configuration object to its file path.
        /// </summary>
        /// <param name="configuration">The configuration object to write.</param>
        internal static void Save(Configuration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (string.IsNullOrWhiteSpace(configuration.FilePath))
                throw new InvalidOperationException("Configuration has no valid file path.");

            var sb = new StringBuilder();

            foreach (var section in configuration.Sections)
            {
                if (section.Value.Count == 0) continue;
                sb.AppendLine($"[{section.Key}]");

                foreach (var entry in section.Value)
                {
                    var key = entry.Key;
                    var value = entry.Value.Value;
                    var description = entry.Value.Description;

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        // Support multi-line descriptions
                        foreach (var line in description.Split('\n'))
                            sb.AppendLine($"# {line.Trim()}");
                    }

                    sb.AppendLine($"{key} = {value}");
                    sb.AppendLine(); // blank line between entries
                }
            }

            // Write all text to file (UTF-8, overwrite existing)
            File.WriteAllText(configuration.FilePath, sb.ToString().TrimEnd() + Environment.NewLine, Encoding.UTF8);
        }

        /// <summary>
        /// Serializes the value into a string format.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static string SerializeValue<T>(T value)
        {
            string serialized;

            if (value == null)
            {
                serialized = string.Empty;
            }
            else if (value is IFormattable formattable)
            {
                // Culture-invariant string for numbers, dates, etc.
                serialized = formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (typeof(T).IsEnum)
            {
                serialized = value.ToString();
            }
            else
            {
                // JSON for complex types
                serialized = JsonConvert.SerializeObject(value);
            }

            return serialized;
        }

        /// <summary>
        /// Deserializes the string value into the specified type of object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strValue"></param>
        /// <returns></returns>
        internal static T DeserializeValue<T>(string strValue)
        {
            var targetType = typeof(T);
            try
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                // Enums
                if (targetType.IsEnum)
                    return (T)Enum.Parse(targetType, strValue, ignoreCase: true);

                // String
                if (targetType == typeof(string))
                    return (T)(object)strValue;

                // Boolean
                if (targetType == typeof(bool))
                    return (T)(object)bool.Parse(strValue);

                // Signed integers
                if (targetType == typeof(byte))
                    return (T)(object)byte.Parse(strValue, culture);
                if (targetType == typeof(short))
                    return (T)(object)short.Parse(strValue, culture);
                if (targetType == typeof(int))
                    return (T)(object)int.Parse(strValue, culture);
                if (targetType == typeof(long))
                    return (T)(object)long.Parse(strValue, culture);

                // Unsigned integers
                if (targetType == typeof(ushort))
                    return (T)(object)ushort.Parse(strValue, culture);
                if (targetType == typeof(uint))
                    return (T)(object)uint.Parse(strValue, culture);
                if (targetType == typeof(ulong))
                    return (T)(object)ulong.Parse(strValue, culture);

                // Floating point
                if (targetType == typeof(float))
                    return (T)(object)float.Parse(strValue, culture);
                if (targetType == typeof(double))
                    return (T)(object)double.Parse(strValue, culture);
                if (targetType == typeof(decimal))
                    return (T)(object)decimal.Parse(strValue, culture);

                // Date/time
                if (targetType == typeof(DateTime))
                    return (T)(object)DateTime.Parse(strValue, culture);

                if (targetType == typeof(TimeSpan))
                    return (T)(object)TimeSpan.Parse(strValue, culture);

                // For any other type, try JSON deserialization
                return JsonConvert.DeserializeObject<T>(strValue);
            }
            catch
            {
                throw new Exception($"Cannot parse \"{strValue}\" to type \"{targetType.Name}\".");
            }
        }
    }
}
