using System.Text.Json;

namespace JSynth.Desktop
{
    public class CLISettings
    {
        public static CLISettings Default { get; } = new();
        public bool UseSkia { get; set; }
        public bool UseGPU { get; set; } = true;
        public bool AllowEGLInitialization { get; set; } = true;
        public bool UseWindowsGL { get; set; }
        public bool UseWindowsUIComposition { get; set; } = true;
        public bool UseDirectX11 { get; set; }
        public bool UseManagedSystemDialogs { get; set; }

        /// <summary>
        /// Menus, flyouts and tooltips drawn inside the window rather than as windows of their own: on by
        /// default under WSL, where WSLg can leave closed menus on the screen, overlapping the next ones.
        /// </summary>
        public bool OverlayPopups { get; set; } = Wsl.IsRunningUnder;
        public bool EnableVNCServer { get; set; }

        /// <summary>
        /// The address VNC is served on; null for this computer only.
        /// </summary>
        public string? VNCHost { get; set; } = null;

        /// <summary>
        /// The port VNC is served on; null for any free one.
        /// </summary>
        public int? VNCPort { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? VNCPassword { get; set; }

        /// <summary>
        /// A design file to open, alone, in this window: a tab opened in a new window.
        /// </summary>
        public string? OpenFile { get; set; }

        /// <summary>
        /// A design never saved, kept here, to open alone in this window; named <see cref="DesignName"/>.
        /// </summary>
        public string? UntitledFile { get; set; }

        public string? DesignName { get; set; }

        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true,
        };

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, _serializerOptions);
        }
    }
}
