using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace TeleList.Services
{
    /// <summary>
    /// Updates teleport coordinates in INI configuration files.
    ///
    /// Looks for lines matching pattern:
    /// <code>keys=store -v TELEPORTX -w "-123.456"</code>
    ///
    /// Supports TELEPORTX, TELEPORTY, and TELEPORTZ variables.
    /// </summary>
    public static class INICoordinateUpdater
    {
        // Regex patterns to match coordinate storage commands in INI files
        // Format: keys=store -v TELEPORTX -w "value" (quotes optional)
        private static readonly Regex TeleportXPattern = new Regex(
            @"^(\s*keys\d*\s*=\s*.*?store\s+-v\s+TELEPORTX\s+-w\s+[""']?)(-?[\d.]+)([""']?.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex TeleportYPattern = new Regex(
            @"^(\s*keys\d*\s*=\s*.*?store\s+-v\s+TELEPORTY\s+-w\s+[""']?)(-?[\d.]+)([""']?.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex TeleportZPattern = new Regex(
            @"^(\s*keys\d*\s*=\s*.*?store\s+-v\s+TELEPORTZ\s+-w\s+[""']?)(-?[\d.]+)([""']?.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Updates TELEPORTX, TELEPORTY, TELEPORTZ values in an INI file.
        /// </summary>
        /// <param name="filepath">Path to the INI file</param>
        /// <param name="x">New X coordinate</param>
        /// <param name="y">New Y coordinate</param>
        /// <param name="z">New Z coordinate</param>
        /// <returns>Tuple of (success, message) indicating result</returns>
        public static (bool success, string message) UpdateCoordinates(string filepath, double x, double y, double z)
        {
            try
            {
                var content = File.ReadAllText(filepath);
                var originalContent = content;
                var updatesMade = new System.Collections.Generic.List<string>();

                // Update X coordinate
                var newX = x.ToString("F6", CultureInfo.InvariantCulture);
                var matchX = TeleportXPattern.Match(content);
                if (matchX.Success)
                {
                    var oldVal = matchX.Groups[2].Value;
                    content = TeleportXPattern.Replace(content, m => $"{m.Groups[1].Value}{newX}{m.Groups[3].Value}", 1);
                    updatesMade.Add($"X: {oldVal} -> {newX}");
                }

                // Update Y coordinate
                var newY = y.ToString("F6", CultureInfo.InvariantCulture);
                var matchY = TeleportYPattern.Match(content);
                if (matchY.Success)
                {
                    var oldVal = matchY.Groups[2].Value;
                    content = TeleportYPattern.Replace(content, m => $"{m.Groups[1].Value}{newY}{m.Groups[3].Value}", 1);
                    updatesMade.Add($"Y: {oldVal} -> {newY}");
                }

                // Update Z coordinate
                var newZ = z.ToString("F6", CultureInfo.InvariantCulture);
                var matchZ = TeleportZPattern.Match(content);
                if (matchZ.Success)
                {
                    var oldVal = matchZ.Groups[2].Value;
                    content = TeleportZPattern.Replace(content, m => $"{m.Groups[1].Value}{newZ}{m.Groups[3].Value}", 1);
                    updatesMade.Add($"Z: {oldVal} -> {newZ}");
                }

                if (updatesMade.Count == 0)
                {
                    return (false, "No coordinate patterns found in the INI file.\n\n" +
                        "Expected format (one per line):\n" +
                        "keys=store -v TELEPORTX -w \"0.0\"\n" +
                        "keys2=store -v TELEPORTY -w \"0.0\"\n" +
                        "keys3=store -v TELEPORTZ -w \"0.0\"");
                }

                if (content == originalContent)
                {
                    return (true, "Coordinates already up to date");
                }

                // Write the updated content
                File.WriteAllText(filepath, content);

                return (true, $"Updated: {string.Join(", ", updatesMade)}");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating file: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads current teleport coordinates from an INI file.
        /// </summary>
        /// <param name="filepath">Path to the INI file</param>
        /// <returns>Tuple of (x, y, z) or null if not found</returns>
        public static (double x, double y, double z)? GetCurrentCoordinates(string filepath)
        {
            try
            {
                var content = File.ReadAllText(filepath);

                var xMatch = TeleportXPattern.Match(content);
                var yMatch = TeleportYPattern.Match(content);
                var zMatch = TeleportZPattern.Match(content);

                if (xMatch.Success && yMatch.Success && zMatch.Success)
                {
                    if (double.TryParse(xMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                        double.TryParse(yMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                        double.TryParse(zMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    {
                        return (x, y, z);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
