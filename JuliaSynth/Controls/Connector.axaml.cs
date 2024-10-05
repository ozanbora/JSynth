using Avalonia.Controls;
using Avalonia.Media;

namespace JuliaSynth.Controls;

public partial class Connector : UserControl
{
    public Connector()
    {
        InitializeComponent();
        RenderOptions.SetRequiresFullOpacityHandling(this, true);
    }

}