using Newtonsoft.Json;
using System;
using System.IO;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// Per-user settings, kept in %AppData%/JuliaSynth/settings.json (~/.config/JuliaSynth on Linux).
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>
        /// Where settings are kept; tests point this at a temporary folder.
        /// </summary>
        internal static string SettingsPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JuliaSynth", "settings.json");

        /// <summary>
        /// The design reopened at start-up.
        /// </summary>
        public string? LastFile { get; set; }

        /// <summary>
        /// Settings can't be read (missing, unreadable, corrupt) → defaults; they are a convenience only.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                return File.Exists(SettingsPath)
                    ? JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SettingsPath)) ?? new()
                    : new();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return new();
            }
        }

        public void Save()
        {
            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Not being able to remember the last file isn't worth interrupting anyone for.
            }
        }
    }
}
