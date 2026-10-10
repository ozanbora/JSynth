using Avalonia;
using Avalonia.Styling;
using JuliaSynth.ViewModels;
using System;
using System.IO;
using System.Windows.Input;
using Xunit;

namespace JuliaSynth.Tests.Interface
{
    /// <summary>
    /// The theme: as the system is unless light or dark is picked, and remembered.
    /// </summary>
    public sealed class ThemeTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public ThemeTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            Ui.Run(() => MainViewModel.ApplyTheme(AppTheme.System));
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void The_theme_follows_the_system_until_one_is_picked_and_is_remembered()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                Assert.Equal(AppTheme.System, viewModel.Theme);
                Assert.True(viewModel.IsSystemTheme);

                viewModel.IsDarkTheme = true;
                Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
                Assert.Equal(AppTheme.Dark, AppSettings.Load().Theme);
                Assert.Contains("\"Dark\"", File.ReadAllText(AppSettings.SettingsPath));
                Assert.Equal(AppTheme.Dark, new MainViewModel().Theme);

                // The bar's button: dark, then as the system is, then light.
                ((ICommand)viewModel.NextTheme).Execute(null);
                Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
                Assert.True(viewModel.IsSystemTheme);
                ((ICommand)viewModel.NextTheme).Execute(null);
                Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
                Assert.Equal(AppTheme.Light, AppSettings.Load().Theme);
            });
        }
    }
}
