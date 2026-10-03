using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using Avalonia.Win32;
using Serilog;
using Splat;
using Splat.Serilog;
using System;

namespace JuliaSynth.Desktop;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("Logs/JuliaSynth-.log", rollingInterval: RollingInterval.Day, shared: true)
            .CreateLogger();
        Locator.CurrentMutable.UseSerilogFullLogger();

        if (CommandLineInterface.GetCLISettings(args, out int exitCode) is not CLISettings settings)
        {
            return exitCode;
        }

        Console.WriteLine("Running Julia Synth..");
        StartApp(settings, args);
        return 0;
    }

    private static void StartApp(CLISettings settings, string[] args)
    {
        AppBuilder builder = BuildAvaloniaApp();

        if (settings.UseSkia)
        {
            _ = builder.UseSkia();
        }

        _ = builder.With(new X11PlatformOptions
        {
            RenderingMode = settings.UseGPU
            ? [X11RenderingMode.Glx, X11RenderingMode.Software]
            : [X11RenderingMode.Software],
            OverlayPopups = settings.OverlayPopups,
        });

        _ = builder.With(new AvaloniaNativePlatformOptions
        {
            RenderingMode = settings.UseGPU
            ? [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software]
            : [AvaloniaNativeRenderingMode.Software]
        });

        Win32RenderingMode[] renderingModeWin32 = settings.AllowEGLInitialization
                ? settings.UseWindowsGL
                    ? [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software]
                    : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
                : [Win32RenderingMode.Software];

        Win32CompositionMode[] compositionModeWin32 = settings.UseWindowsUIComposition
            ? [Win32CompositionMode.WinUIComposition, Win32CompositionMode.RedirectionSurface]
            : [Win32CompositionMode.RedirectionSurface];

        _ = builder.With(new Win32PlatformOptions
        {
            RenderingMode = renderingModeWin32,
            CompositionMode = compositionModeWin32
        });

        if (settings.UseDirectX11)
        {
            _ = builder.With(new AngleOptions
            {
                AllowedPlatformApis =
                    [
                        AngleOptions.PlatformApi.DirectX11
                    ]
            });
        }

        if (settings.UseManagedSystemDialogs)
        {
            _ = builder.UseManagedSystemDialogs();
        }

        if (settings.Headless)
        {
            _ = builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }

        if (settings.EnableVNCServer && !string.IsNullOrEmpty(settings.VNCHost))
        {
            _ = builder.StartWithHeadlessVncPlatform(settings.VNCHost, settings.VNCPort, args, ShutdownMode.OnMainWindowClose);
            return;
        }

        _ = builder.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI(_ => { });
    }
}
