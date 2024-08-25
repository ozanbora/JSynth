using System.Text.Json;

namespace JuliaSynth.Desktop
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
        public bool Headless { get; set; }
        public bool EnableVNCServer { get; set; }
        public string? VNCHost { get; set; } = null;
        public int VNCPort { get; set; } = 5901;

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
