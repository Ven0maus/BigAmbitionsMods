using System;
using System.IO;
using System.Linq;

namespace Venomaus.BigAmbitionsMods.Common.Helpers
{
    public static class PathUtils
    {
        private static char[] _invalidFileNameChars;

        /// <summary>
        /// Sanitizes an entire file or folder path by cleaning each segment individually.
        /// </summary>
        /// <param name="path">The path to sanitize (absolute or relative).</param>
        /// <param name="replacement">Replacement character for invalid characters (default '_').</param>
        /// <returns>A sanitized, safe file path.</returns>
        public static string SanitizePath(string path, char replacement = '_')
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            var invalidChars = _invalidFileNameChars ?? (_invalidFileNameChars = Path.GetInvalidFileNameChars());

            // Handle UNC paths (e.g., \\server\share)
            bool isUnc = path.StartsWith(@"\\");
            string root = Path.GetPathRoot(path);

            // Split into segments, skipping root if present
            var segments = path
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment =>
                {
                    // Sanitize each directory or file name component
                    var sanitized = new string(segment
                        .Select(ch => invalidChars.Contains(ch) ? replacement : ch)
                        .ToArray());

                    sanitized = sanitized.Trim().TrimEnd('.', ' ');
                    return string.IsNullOrEmpty(sanitized) ? "untitled" : sanitized;
                });

            // Recombine sanitized segments
            string sanitizedPath = string.Join(Path.DirectorySeparatorChar.ToString(), segments);

            if (!string.IsNullOrEmpty(root))
            {
                sanitizedPath = Path.Combine(root, sanitizedPath);
            }
            else if (isUnc)
            {
                sanitizedPath = @"\\" + sanitizedPath;
            }

            return sanitizedPath;
        }
    }
}
