using Avalonia.Controls;
using Avalonia.Platform;
using System;

namespace JSynth.Views;

/// <summary>
/// JSynth's icon, for every window: the taskbar and window switchers show it, on Linux (and WSL) too,
/// where nothing else gives a window its icon. On Windows the program file has it as well.
/// </summary>
public static class AppIcon
{
    private static WindowIcon? _icon;

    public static WindowIcon Window => _icon ??= new WindowIcon(AssetLoader.Open(new Uri("avares://JSynth/Assets/Icon/jsynth.ico")));
}
