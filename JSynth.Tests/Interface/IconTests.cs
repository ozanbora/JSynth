using JSynth.Model;
using JSynth.ViewModels;
using JSynth.Views;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// Every window carries JSynth's icon: on Linux (and WSL) nothing else gives it one.
    /// </summary>
    public class IconTests
    {
        [Fact]
        public void Every_window_has_the_icon()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Assert.NotNull(AppIcon.Window);
                Assert.NotNull(new MainWindow { DataContext = new MainViewModel() }.Icon);
                Assert.Same(AppIcon.Window, new Views.Outputs.PlotWindow(new PlotNode()).Icon);
                Assert.Same(AppIcon.Window, new MessageDialog("Title", "Message", ["OK"]).Icon);
            });
        }
    }
}
