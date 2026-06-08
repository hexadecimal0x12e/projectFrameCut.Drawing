using projectFrameCut.Drawing.Gallary.Pages;

namespace projectFrameCut.Drawing.Gallary;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(EffectsPage), typeof(EffectsPage));
        Routing.RegisterRoute(nameof(BlendPage), typeof(BlendPage));
        Routing.RegisterRoute(nameof(VectorPage), typeof(VectorPage));
        Routing.RegisterRoute(nameof(TextPage), typeof(TextPage));
    }
}
