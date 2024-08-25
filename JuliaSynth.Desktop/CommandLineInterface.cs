using Splat;
using System.CommandLine;

namespace JuliaSynth.Desktop
{
    public class CommandLineInterface
    {
        public static CLISettings GetCLISettings(string[] args)
        {
            CLISettingsBinder binder = new();
            CLISettings parsedSettings = new();

            RootCommand rootCommand = new()
            {
                Description = "A multi-platform Julia Visual Editor."
            };

            rootCommand.AddOption(binder.UseSkia);
            rootCommand.AddOption(binder.UseGPU);
            rootCommand.AddOption(binder.AllowEGLInitialization);
            rootCommand.AddOption(binder.UseWindowsGL);
            rootCommand.AddOption(binder.UseWindowsUIComposition);
            rootCommand.AddOption(binder.UseDirectX11);
            rootCommand.AddOption(binder.UseManagedSystemDialogs);
            rootCommand.AddOption(binder.Headless);
            rootCommand.AddOption(binder.EnableVNCServer);
            rootCommand.AddOption(binder.VNCHost);
            rootCommand.AddOption(binder.VNCPort);

            rootCommand.SetHandler((settings) => { parsedSettings = settings; }, binder);

            LogHost.Default.Info("Parsing arguments.");

            _ = rootCommand.Invoke(args);

            LogHost.Default.Info("Command line interface settings parsed.");

            return parsedSettings;
        }
    }
}