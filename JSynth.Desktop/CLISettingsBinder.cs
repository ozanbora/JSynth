using System.CommandLine;
using System.CommandLine.Binding;

namespace JSynth.Desktop
{
    public class CLISettingsBinder : BinderBase<CLISettings>
    {
        public Option<bool> UseSkia { get; set; }

        public Option<bool> UseGPU { get; set; }

        public Option<bool> AllowEGLInitialization { get; set; }

        public Option<bool> UseWindowsGL { get; set; }

        public Option<bool> UseWindowsUIComposition { get; set; }

        public Option<bool> UseDirectX11 { get; set; }

        public Option<bool> UseManagedSystemDialogs { get; set; }

        public Option<bool> OverlayPopups { get; set; }

        public Option<bool> Headless { get; set; }

        public Option<bool> EnableVNCServer { get; set; }

        public Option<string?> VNCHost { get; set; }

        public Option<int> VNCPort { get; set; }

        public Option<string?> OpenFile { get; set; }

        public Option<string?> UntitledFile { get; set; }

        public Option<string?> DesignName { get; set; }

        public CLISettingsBinder()
        {
            UseSkia = new Option<bool>(
                aliases: ["--useSkia"],
                getDefaultValue: () => CLISettings.Default.UseSkia,
                description: "Use Skia renderer"
            );

            UseGPU = new Option<bool>(
                aliases: ["--useGpu"],
                getDefaultValue: () => CLISettings.Default.UseGPU,
                description: "Use GPU"
            );

            AllowEGLInitialization = new Option<bool>(
                aliases: ["--allowEGLInitialization"],
                getDefaultValue: () => CLISettings.Default.AllowEGLInitialization,
                description: "Allow Egl initialization"
            );

            UseWindowsGL = new Option<bool>(
                aliases: ["--useWindowsGL"],
                getDefaultValue: () => CLISettings.Default.UseWindowsGL,
                description: "Use Windows GL"
            );

            UseWindowsUIComposition = new Option<bool>(
                aliases: ["--useWindowsUiComposition"],
                getDefaultValue: () => CLISettings.Default.UseWindowsUIComposition,
                description: "Use Windows UI composition"
            );

            UseDirectX11 = new Option<bool>(
                aliases: ["--useDirectX11"],
                getDefaultValue: () => CLISettings.Default.UseDirectX11,
                description: "Use DirectX11 platform api"
            );

            UseManagedSystemDialogs = new Option<bool>(
                aliases: ["--useManagedSystemDialogs"],
                getDefaultValue: () => CLISettings.Default.UseManagedSystemDialogs,
                description: "Use managed system dialogs"
            );

            OverlayPopups = new Option<bool>(
                aliases: ["--overlayPopups"],
                getDefaultValue: () => CLISettings.Default.OverlayPopups,
                description: "Draw menus and popups inside the window (Linux; on by default under WSL)"
            );

            Headless = new Option<bool>(
                aliases: ["--headless"],
                getDefaultValue: () => CLISettings.Default.Headless,
                description: "Run in headless mode"
            );

            EnableVNCServer = new Option<bool>(
                aliases: ["--enableVncServer"],
                getDefaultValue: () => CLISettings.Default.EnableVNCServer,
                description: "Enable VNC server"
            );

            VNCHost = new Option<string?>(
                aliases: ["--vncHost"],
                getDefaultValue: () => CLISettings.Default.VNCHost,
                description: "VNC host"
            );

            VNCPort = new Option<int>(
                aliases: ["--vncPort"],
                getDefaultValue: () => CLISettings.Default.VNCPort,
                description: "VNC port"
            );

            OpenFile = new Option<string?>(
                aliases: ["--open"],
                description: "Open this design file, alone in the window"
            );

            UntitledFile = new Option<string?>(
                aliases: ["--untitled"],
                description: "Open this design never saved, alone in the window"
            );

            DesignName = new Option<string?>(
                aliases: ["--name"],
                description: "What to call the design given with --untitled"
            );
        }

        private static T? GetOptionValue<T>(BindingContext bindingContext, Option<T> option)
        {
            return bindingContext.ParseResult.GetValueForOption<T>(option);
        }
        protected override CLISettings GetBoundValue(BindingContext bindingContext)
        {
            return new()
            {
                UseSkia = GetOptionValue(bindingContext, UseSkia),
                UseGPU = GetOptionValue(bindingContext, UseGPU),
                AllowEGLInitialization = GetOptionValue(bindingContext, AllowEGLInitialization),
                UseWindowsGL = GetOptionValue(bindingContext, UseWindowsGL),
                UseWindowsUIComposition = GetOptionValue(bindingContext, UseWindowsUIComposition),
                UseDirectX11 = GetOptionValue(bindingContext, UseDirectX11),
                UseManagedSystemDialogs = GetOptionValue(bindingContext, UseManagedSystemDialogs),
                OverlayPopups = GetOptionValue(bindingContext, OverlayPopups),
                Headless = GetOptionValue(bindingContext, Headless),
                EnableVNCServer = GetOptionValue(bindingContext, EnableVNCServer),
                VNCHost = GetOptionValue(bindingContext, VNCHost),
                VNCPort = GetOptionValue(bindingContext, VNCPort),
                OpenFile = GetOptionValue(bindingContext, OpenFile),
                UntitledFile = GetOptionValue(bindingContext, UntitledFile),
                DesignName = GetOptionValue(bindingContext, DesignName),
            };
        }
    }
}
