# JuliaSynth

A multi-platform visual editor for Julia: wire nodes together on a canvas, then run the design as a Julia script.

## Requirements

- [.NET SDK 9.0.300](https://dotnet.microsoft.com/download) or newer, plus the .NET 8 runtime (the projects target `net8.0`). Older SDKs can't run Avalonia 12's source generators; `global.json` enforces this.
- [Julia](https://julialang.org/downloads/), preferably through juliaup. JuliaSynth looks for it on the `PATH` and in the usual install folders, or you can point it at `julia` yourself.

## Build and run

```sh
dotnet run --project JuliaSynth.Desktop
dotnet test JuliaSynth.Tests
```

Run `dotnet run --project JuliaSynth.Desktop -- --help` for rendering and headless options.

## Linux and WSL

JuliaSynth runs on Linux, including Ubuntu under WSL 2 with WSLg (Windows 11 shows the window on the Windows desktop).

1. Install the native library Skia needs:

   ```sh
   sudo apt install libfontconfig1
   ```

2. Install .NET. Ubuntu 22.04's apt only offers SDK 8, which is too old, so use Microsoft's install script:

   ```sh
   curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
   bash dotnet-install.sh --channel 9.0 --install-dir ~/.dotnet
   bash dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir ~/.dotnet
   export PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet
   ```

3. Install Julia with `curl -fsSL https://install.julialang.org | sh`.

Under WSL, clone the repository into the Linux file system (for example `~/JuliaSynth`) rather than building from `/mnt/c`, so Linux and Windows builds don't share `bin` and `obj` folders.

Alternatively, publish a self-contained build from Windows and run it in WSL with only `libfontconfig1` installed:

```sh
dotnet publish JuliaSynth.Desktop -c Release -r linux-x64 --self-contained -o publish/linux-x64
```
