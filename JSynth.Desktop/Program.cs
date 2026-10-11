using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using ReactiveUI.Avalonia;
using Avalonia.Win32;
using Serilog;
using Splat;
using Splat.Serilog;
using System;
using System.Net;
using System.Net.Sockets;

namespace JSynth.Desktop;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // A Windows app has no console of its own, so no window opens beside it; started from a terminal,
        // it writes there (help, errors in the arguments).
        if (OperatingSystem.IsWindows())
        {
            _ = AttachConsole(AttachParentProcess);
        }

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("Logs/JSynth-.log", rollingInterval: RollingInterval.Day, shared: true)
            .CreateLogger();
        Locator.CurrentMutable.UseSerilogFullLogger();

        if (CommandLineInterface.GetCLISettings(args, out int exitCode) is not CLISettings settings)
        {
            return exitCode;
        }

        if (settings.EnableVNCServer && CheckVnc(settings) is int vncError)
        {
            return vncError;
        }

        Console.WriteLine("Running JSynth..");
        StartApp(settings, args);
        return 0;
    }

    /// <summary>
    /// Says what's wrong before serving VNC, rather than leaving viewers waiting; null when nothing is.
    /// </summary>
    private static int? CheckVnc(CLISettings settings)
    {
        IPAddress? address = null;
        if (!string.IsNullOrEmpty(settings.VNCHost) && !IPAddress.TryParse(settings.VNCHost, out address))
        {
            Console.Error.WriteLine($"'{settings.VNCHost}' isn't an IP address: give --vncHost as one, such as 127.0.0.1 or 0.0.0.0.");
            return 1;
        }

        TurboJpeg.FindSystemLibrary();
        if (!TurboJpeg.IsAvailable())
        {
            Console.Error.WriteLine($"VNC needs the turbojpeg library, which wasn't found. {TurboJpeg.InstallHint}");
            return 1;
        }

        // No port: any free one, found by binding port 0 for a moment.
        address ??= IPAddress.Loopback;
        try
        {
            TcpListener listener = new(address, settings.VNCPort ?? 0);
            listener.Start();
            settings.VNCPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }
        catch (Exception error) when (error is SocketException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"Can't serve VNC at {address}:{settings.VNCPort}: {error.Message}");
            return 1;
        }

        string where = address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            ? $"port {settings.VNCPort} on every address of this computer"
            : $"{address}:{settings.VNCPort}";
        Console.WriteLine($"Serving JSynth over VNC at {where}: connect a VNC viewer there.");
        Log.Information("Serving VNC at {Address}:{Port}", address, settings.VNCPort);
        if (!IPAddress.IsLoopback(address) && !IPAddress.IsLoopback(address) && string.IsNullOrEmpty(settings.VNCPassword))
        {
            Console.Error.WriteLine("Warning: any computer that can reach this address controls JSynth and the Julia it runs, with no password. "
                + "Serve on 127.0.0.1 and connect through an SSH tunnel, or at least give --vncPassword.");
        }

        return null;
    }

    private static void StartApp(CLISettings settings, string[] args)
    {
        App.StartDesign = (settings.OpenFile, settings.UntitledFile, settings.DesignName);
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

        // The managed dialogs are only for desktop systems; the analyzer needs the check spelled out.
        if (settings.UseManagedSystemDialogs && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            _ = builder.UseManagedSystemDialogs();
        }

        // No host: this computer only. CheckVnc has picked the port.
        if (settings.EnableVNCServer)
        {
            _ = builder.StartWithHeadlessVncPlatform(settings.VNCHost, settings.VNCPort!.Value, settings.VNCPassword, args, ShutdownMode.OnMainWindowClose);
            return;
        }

        _ = builder.StartWithClassicDesktopLifetime(args);
    }

    private const int AttachParentProcess = -1;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

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
