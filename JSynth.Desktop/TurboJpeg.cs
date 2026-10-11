using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace JSynth.Desktop
{
    /// <summary>
    /// The JPEG library VNC sessions need. Its package brings it for Windows and Intel Macs only; elsewhere it is the
    /// system's, under names the package doesn't try: Ubuntu's libturbojpeg has only libturbojpeg.so.0, and Homebrew's
    /// jpeg-turbo is outside the usual search path on Apple Silicon.
    /// </summary>
    public static class TurboJpeg
    {
        private const string Name = "turbojpeg";

        private static readonly string[] SystemNames =
        [
            "libturbojpeg.so.0",
            "/opt/homebrew/opt/jpeg-turbo/lib/libturbojpeg.dylib",
            "/usr/local/opt/jpeg-turbo/lib/libturbojpeg.dylib",
        ];

        private static Assembly Wrapper => typeof(TurboJpegWrapper.TJCompressor).Assembly;

        /// <summary>
        /// Lets the wrapper fall back on the system's library. Not a DllImport resolver: the wrapper sets its own, and
        /// an assembly can have only one; this runs only after it and the usual search find nothing.
        /// </summary>
        public static void FindSystemLibrary()
        {
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, name) =>
                assembly == Wrapper && name == Name ? LoadSystemLibrary() : IntPtr.Zero;
        }

        /// <summary>
        /// Whether the library can be loaded; without it every VNC connection is left unanswered.
        /// </summary>
        public static bool IsAvailable()
        {
            return NativeLibrary.TryLoad(Name, Wrapper, null, out _) || LoadSystemLibrary() != IntPtr.Zero;
        }

        /// <summary>
        /// How to install the library on this system.
        /// </summary>
        public static string InstallHint => OperatingSystem.IsMacOS()
            ? "Install it with Homebrew: brew install jpeg-turbo"
            : "Install it with your package manager, for example: sudo apt install libturbojpeg";

        private static IntPtr LoadSystemLibrary()
        {
            foreach (string name in SystemNames)
            {
                if ((!Path.IsPathRooted(name) || File.Exists(name)) && NativeLibrary.TryLoad(name, out IntPtr handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }
    }
}
