using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        /// Whether <see cref="LastFile"/> holds a design never saved, kept to reopen as such.
        /// </summary>
        public bool LastUntitled { get; set; }

        /// <summary>
        /// Whether designs save themselves a moment after each change.
        /// </summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// The Julia program picked by the user; null to look for one.
        /// </summary>
        public string? JuliaPath { get; set; }

        /// <summary>
        /// Where the data of examples was downloaded to, by the data's name (see <see cref="ExampleData"/>).
        /// </summary>
        public Dictionary<string, string> DataFolders
        {
            get => _dataFolders;
            set => _dataFolders = value ?? [];
        }

        private Dictionary<string, string> _dataFolders = [];

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
