using Splat;
using System.CommandLine;

namespace JSynth.Desktop
{
    public class CommandLineInterface
    {
        /// <summary>
        /// The settings <paramref name="args"/> ask for, or null when the app shouldn't start: after
        /// printing help or the version, or an error about the arguments. <paramref name="exitCode"/>
        /// is what the process should exit with then.
        /// </summary>
        public static CLISettings? GetCLISettings(string[] args, out int exitCode)
        {
            CLISettingsBinder binder = new();
            CLISettings? parsedSettings = null;

            // Named for the program people run, whatever its file is called.
            RootCommand rootCommand = new()
            {
                Name = "JSynth",
                Description = "A multi-platform Julia Visual Editor."
            };

            rootCommand.AddOption(binder.UseSkia);
            rootCommand.AddOption(binder.UseGPU);
            rootCommand.AddOption(binder.AllowEGLInitialization);
            rootCommand.AddOption(binder.UseWindowsGL);
            rootCommand.AddOption(binder.UseWindowsUIComposition);
            rootCommand.AddOption(binder.UseDirectX11);
            rootCommand.AddOption(binder.UseManagedSystemDialogs);
            rootCommand.AddOption(binder.OverlayPopups);
            rootCommand.AddOption(binder.EnableVNCServer);
            rootCommand.AddOption(binder.VNCHost);
            rootCommand.AddOption(binder.VNCPort);
            rootCommand.AddOption(binder.VNCPassword);
            rootCommand.AddOption(binder.OpenFile);
            rootCommand.AddOption(binder.UntitledFile);
            rootCommand.AddOption(binder.DesignName);

            rootCommand.SetHandler((settings) => { parsedSettings = settings; }, binder);

            LogHost.Default.Info("Parsing arguments.");

            // The handler only runs when the arguments are for starting the app.
            exitCode = rootCommand.Invoke(args);

            LogHost.Default.Info("Command line interface settings parsed.");

            return parsedSettings;
        }
    }
}