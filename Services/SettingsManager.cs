using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeleList.Services
{
    /// <summary>
    /// Application settings that are persisted to JSON.
    /// Settings file is stored next to the executable.
    /// </summary>
    public class AppSettings
    {
        [JsonPropertyName("ini_file_path")]
        public string IniFilePath { get; set; } = string.Empty;

        [JsonPropertyName("entities_file_path")]
        public string EntitiesFilePath { get; set; } = string.Empty;

        [JsonPropertyName("auto_refresh")]
        public bool AutoRefresh { get; set; } = true;

        [JsonPropertyName("auto_update_ini")]
        public bool AutoUpdateIni { get; set; } = false;

        [JsonPropertyName("window_geometry")]
        public string WindowGeometry { get; set; } = "1200x750";

        [JsonPropertyName("window_left")]
        public double WindowLeft { get; set; } = 100;

        [JsonPropertyName("window_top")]
        public double WindowTop { get; set; } = 100;

        [JsonPropertyName("window_width")]
        public double WindowWidth { get; set; } = 1200;

        [JsonPropertyName("window_height")]
        public double WindowHeight { get; set; } = 750;

        [JsonPropertyName("marked_entities")]
        public List<string> MarkedEntities { get; set; } = new List<string>();

        [JsonPropertyName("global_hotkeys_enabled")]
        public bool GlobalHotkeysEnabled { get; set; } = true;

        // Hotkey settings
        [JsonPropertyName("hotkey_next")]
        public string HotkeyNext { get; set; } = "right";

        [JsonPropertyName("hotkey_prev")]
        public string HotkeyPrev { get; set; } = "left";

        [JsonPropertyName("hotkey_mark")]
        public string HotkeyMark { get; set; } = "oem5";

        [JsonPropertyName("hotkey_reload")]
        public string HotkeyReload { get; set; } = "ctrl+r";

        [JsonPropertyName("hotkey_update_ini")]
        public string HotkeyUpdateIni { get; set; } = "ctrl+u";

        [JsonPropertyName("hotkey_clear")]
        public string HotkeyClear { get; set; } = "ctrl+delete";

        // Warning suppression settings
        [JsonPropertyName("suppress_clear_entities_warning")]
        public bool SuppressClearEntitiesWarning { get; set; } = false;

        [JsonPropertyName("suppress_clear_skipped_warning")]
        public bool SuppressClearSkippedWarning { get; set; } = false;

        // Last used entity tracking
        [JsonPropertyName("last_used_entity_key")]
        public string LastUsedEntityKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles loading and saving application settings to JSON file.
    /// Settings are stored in the same directory as the executable.
    /// </summary>
    public static class SettingsManager
    {
        // Settings file is stored next to the executable for portability
        private static readonly string SettingsFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "telelist_settings.json");

        public static AppSettings LoadSettings()
        {
            var settings = new AppSettings();

            try
            {
                if (File.Exists(SettingsFile))
                {
                    var content = File.ReadAllText(SettingsFile);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var loaded = JsonSerializer.Deserialize<AppSettings>(content);
                        if (loaded != null)
                        {
                            settings = loaded;

                            // Ensure lists are never null
                            settings.MarkedEntities ??= new List<string>();

                            // Ensure strings are never null (use empty string as default for paths)
                            settings.IniFilePath ??= string.Empty;
                            settings.EntitiesFilePath ??= string.Empty;
                            settings.WindowGeometry ??= "1200x750";
                            settings.HotkeyNext ??= "right";
                            settings.HotkeyPrev ??= "left";
                            settings.HotkeyMark ??= "oem5";
                            settings.HotkeyReload ??= "ctrl+r";
                            settings.HotkeyUpdateIni ??= "ctrl+u";
                            settings.HotkeyClear ??= "ctrl+delete";
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Settings file corrupted, using defaults. Error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Warning: Cannot read settings file (permission denied): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error loading settings: {ex.Message}");
            }

            return settings;
        }

        public static bool SaveSettings(AppSettings settings)
        {
            try
            {
                var settingsDir = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                // Write to temp file first, then rename for atomic write
                var tempFile = SettingsFile + ".tmp";
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(tempFile, json);

                // Atomic rename
                if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                }
                File.Move(tempFile, SettingsFile);

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: Cannot save settings (permission denied): {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error: Cannot save settings (IO error): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to save settings: {ex.Message}");
                return false;
            }
        }
    }
}
