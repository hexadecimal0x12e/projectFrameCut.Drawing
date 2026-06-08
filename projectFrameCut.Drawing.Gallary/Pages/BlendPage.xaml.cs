using projectFrameCut.Drawing.Gallary.Demos;

namespace projectFrameCut.Drawing.Gallary.Pages;

public partial class BlendPage : ContentPage
{
    public BlendPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (DemoImage.IsVisible) return;

        _ = Task.Run(() =>
        {
            try
            {
                var src = BlendGenerator.GenerateBlendDemo(120);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DemoImage.Source = src;
                    DemoImage.IsVisible = true;
                    Spinner.IsRunning = false;
                    Spinner.IsVisible = false;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Spinner.IsRunning = false;
                    Spinner.IsVisible = false;
                });
                System.Diagnostics.Debug.WriteLine($"BlendPage error: {ex}");
            }
        });
    }
}
