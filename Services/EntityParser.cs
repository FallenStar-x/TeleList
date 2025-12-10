using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// Tries multiple encodings to handle various file formats including CJK languages.
        /// </summary>
        /// <param name="filepath">Path to the entity file</param>
        /// <returns>List of parsed entities</returns>
        public static List<Entity> ParseFile(string filepath)
        {
            var entities = new List<Entity>();
            string content;

            try
            {
                content = ReadFileWithEncodingDetection(filepath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading file: {ex.Message}");
            }

            // Split by separator
            var blocks = content.Split(new[] { "----------------" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                try
                {
                    var trimmedBlock = block.Trim();
                    if (string.IsNullOrEmpty(trimmedBlock))
                        continue;

                    // Parse entity type
                    var typeMatch = TypePattern.Match(trimmedBlock);
                    if (!typeMatch.Success)
                        continue;

                    var entityType = typeMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(entityType))
                        continue;

                    // Parse location
                    var locMatch = LocationPattern.Match(trimmedBlock);
                    if (!locMatch.Success)
                        continue;

                    if (!double.TryParse(locMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                        !double.TryParse(locMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                        !double.TryParse(locMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                        continue;

                    // Parse distance
                    var distMatch = DistancePattern.Match(trimmedBlock);
                    var distance = 0.0;
                    if (distMatch.Success)
                    {
                        double.TryParse(distMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out distance);
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
                catch
                {
                    // Skip malformed blocks, continue parsing others
                    continue;
                }
            }

            return entities;
        }

        /// <summary>
        /// Reads a file with automatic encoding detection.
        /// Tries BOM detection first, then validates content with strict decoders.
        /// Supports UTF-8, UTF-16, and various CJK encodings.
        /// </summary>
        private static string ReadFileWithEncodingDetection(string filepath)
        {
            var bytes = File.ReadAllBytes(filepath);

            if (bytes.Length == 0)
                return string.Empty;

            // Check for BOM (Byte Order Mark) first
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2); // UTF-16 LE
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2); // UTF-16 BE
            }

            // No BOM - try encodings with strict validation
            // Order matters: try most common/reliable first
            var encodingsToTry = new (string name, Encoding? encoding)[]
            {
                ("UTF-8", new UTF8Encoding(false, true)), // UTF-8 strict (throws on invalid)
                ("GB18030", GetEncodingSafe("GB18030")),  // Simplified Chinese (superset of GBK/GB2312)
                ("Big5", GetEncodingSafe("Big5")),        // Traditional Chinese
                ("Shift-JIS", GetEncodingSafe("shift_jis")), // Japanese
                ("EUC-KR", GetEncodingSafe("euc-kr")),    // Korean
                ("UTF-16 LE", Encoding.Unicode),          // UTF-16 without BOM
                ("Windows-1252", Encoding.GetEncoding(1252)), // Western European
            };

            foreach (var (name, encoding) in encodingsToTry)
            {
                if (encoding == null)
                    continue;

                try
                {
                    // Create a strict decoder that throws on invalid sequences
                    var strictEncoding = Encoding.GetEncoding(
                        encoding.CodePage,
                        EncoderFallback.ExceptionFallback,
                        DecoderFallback.ExceptionFallback);

                    var result = strictEncoding.GetString(bytes);

                    // Additional validation: check if result contains expected patterns
                    if (result.Contains("Entity type:") || result.Contains("Location:"))
                    {
                        return result;
                    }
                }
                catch (DecoderFallbackException)
                {
                    // This encoding doesn't work, try next
                    continue;
                }
                catch (ArgumentException)
                {
                    // Encoding not supported on this system, try next
                    continue;
                }
            }

            // Fallback: use UTF-8 with replacement characters (lossy but won't crash)
            var fallbackEncoding = new UTF8Encoding(false, false);
            return fallbackEncoding.GetString(bytes);
        }

        /// <summary>
        /// Safely gets an encoding by name, returning null if not available.
        /// </summary>
        private static Encoding? GetEncodingSafe(string name)
        {
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch
            {
                return null;
            }
        }
    }
}
