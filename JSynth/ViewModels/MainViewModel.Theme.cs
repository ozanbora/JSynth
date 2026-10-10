using Avalonia;
using Avalonia.Styling;
using ReactiveUI;
using System.Windows.Input;

namespace JSynth.ViewModels;

/// <summary>
/// Light, dark, or as the system is.
/// </summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The theme: as the system is unless one is picked (View → Theme, or the bar's button), and kept in the settings.
/// </summary>
public partial class MainViewModel
{
    private AppTheme? _theme;

    public AppTheme Theme
    {
        get => _theme ??= AppSettings.Load().Theme;
        set
        {
            if (value == Theme)
            {
                return;
            }

            _theme = value;
            ApplyTheme(value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSystemTheme));
            this.RaisePropertyChanged(nameof(IsLightTheme));
            this.RaisePropertyChanged(nameof(IsDarkTheme));
            this.RaisePropertyChanged(nameof(ThemeGlyph));
            this.RaisePropertyChanged(nameof(ThemeTip));
            AppSettings settings = AppSettings.Load();
            settings.Theme = value;
            settings.Save();
        }
    }

    public bool IsSystemTheme
    {
        get => Theme == AppTheme.System;
        set { if (value) { Theme = AppTheme.System; } }
    }

    public bool IsLightTheme
    {
        get => Theme == AppTheme.Light;
        set { if (value) { Theme = AppTheme.Light; } }
    }

    public bool IsDarkTheme
    {
        get => Theme == AppTheme.Dark;
        set { if (value) { Theme = AppTheme.Dark; } }
    }

    /// <summary>
    /// The bar's theme button: the theme now.
    /// </summary>
    public string ThemeGlyph => Theme switch
    {
        AppTheme.Light => "☀",
        AppTheme.Dark => "☾",
        _ => "◐",
    };

    public string ThemeTip => Theme switch
    {
        AppTheme.Light => "Theme: light. Click for dark",
        AppTheme.Dark => "Theme: dark. Click to follow the system",
        _ => "Theme: as the system is. Click for light",
    };

    /// <summary>
    /// System, then light, then dark, then system again.
    /// </summary>
    public ICommand NextTheme => _nextTheme ??= ReactiveCommand.Create(() => { Theme = (AppTheme)(((int)Theme + 1) % 3); });

    private ICommand? _nextTheme;

    /// <summary>
    /// Shows the app in <paramref name="theme"/>.
    /// </summary>
    public static void ApplyTheme(AppTheme theme)
    {
        if (Application.Current is Application app)
        {
            app.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
