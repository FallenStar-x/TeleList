using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeleList.Models;

namespace TeleList.Services
{
    /// <summary>
    /// Parses entity data files exported from the game.
    ///
    /// Expected file format (blocks separated by "----------------"):
    /// <code>
    /// Entity type: SomeEntity_123
    /// Location: -123.45,67.89,-234.56
    /// Distance: 45.67
    /// ----------------
    /// </code>
    /// </summary>
    public static class EntityParser
    {
        // Regex patterns for parsing entity data blocks
        private static readonly Regex TypePattern = new Regex(@"Entity type:\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex LocationPattern = new Regex(@"Location:\s*([-\d.]+),([-\d.]+),([-\d.]+)", RegexOptions.Compiled);
        private static readonly Regex DistancePattern = new Regex(@"Distance:\s*([-\d.]+)", RegexOptions.Compiled);

        /// <summary>
        /// Parses an entity file and returns a list of Entity objects.
        /// Tries multiple encodings to handle various file formats.
        /// </summary>
        /// <param name="filepath">Path to the entity file</param>
        /// <returns>List of parsed entities</returns>
        public static List<Entity> ParseFile(string filepath)
        {
            var entities = new List<Entity>();

            string? content = null;
            Encoding[] encodings = {
                Encoding.UTF8,
                new UTF8Encoding(true), // UTF-8 with BOM
                Encoding.Unicode,       // UTF-16 LE
                Encoding.GetEncoding(1252), // Windows-1252
                Encoding.Latin1,
                Encoding.GetEncoding("GB2312")
            };

            foreach (var encoding in encodings)
            {
                try
                {
                    content = File.ReadAllText(filepath, encoding);
                    break;
                }
                catch (DecoderFallbackException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error reading file: {ex.Message}");
                }
            }

            if (content == null)
            {
                // Last resort: read as bytes and decode with replacement
                try
                {
                    var bytes = File.ReadAllBytes(filepath);
                    content = Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error reading file: {ex.Message}");
                }
            }

            // Split by separator
            var blocks = content.Split(new[] { "----------------" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var trimmedBlock = block.Trim();
                if (string.IsNullOrEmpty(trimmedBlock))
                    continue;

                // Parse entity type
                var typeMatch = TypePattern.Match(trimmedBlock);
                if (!typeMatch.Success)
                    continue;

                var entityType = typeMatch.Groups[1].Value.Trim();

                // Parse location
                var locMatch = LocationPattern.Match(trimmedBlock);
                if (!locMatch.Success)
                    continue;

                if (!double.TryParse(locMatch.Groups[1].Value, out var x) ||
                    !double.TryParse(locMatch.Groups[2].Value, out var y) ||
                    !double.TryParse(locMatch.Groups[3].Value, out var z))
                    continue;

                // Parse distance
                var distMatch = DistancePattern.Match(trimmedBlock);
                var distance = 0.0;
                if (distMatch.Success)
                {
                    double.TryParse(distMatch.Groups[1].Value, out distance);
                }

                entities.Add(new Entity
                {
                    EntityType = entityType,
                    X = x,
                    Y = y,
                    Z = z,
                    Distance = distance
                });
            }

            return entities;
        }
    }
}
