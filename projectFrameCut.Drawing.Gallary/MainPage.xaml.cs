using projectFrameCut.Drawing.Gallary.Demos;
using projectFrameCut.Drawing.Gallary.Pages;

namespace projectFrameCut.Drawing.Gallary;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        LoadSourcePattern();
    }

    private void LoadSourcePattern()
    {
        using var pattern = DemoHelper.GenerateTestPattern(120, 120);
        SourceImage.Source = pattern.ToImageSource();
    }

    private async void OnEffectsTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(EffectsPage));

    private async void OnBlendTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(BlendPage));

    private async void OnVectorTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(VectorPage));

    private async void OnTextTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(TextPage));
}
