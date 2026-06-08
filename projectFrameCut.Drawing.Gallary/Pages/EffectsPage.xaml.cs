using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Gallary.Demos;
using projectFrameCut.Drawing.Gallary.Services;

namespace projectFrameCut.Drawing.Gallary.Pages;

public partial class EffectsPage : ContentPage
{
    private const int ThumbSize = 120;

    public EffectsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (EffectsContainer.Children.Count > 0) return;

        _ = Task.Run(() => LoadEffectsAsync());
    }

    private async Task LoadEffectsAsync()
    {
        try
        {
            var effects = EffectDiscoveryService.Discover();

            // Generate the base test pattern once
            using var basePattern = DemoHelper.GenerateTestPattern(ThumbSize, ThumbSize);

            foreach (var effect in effects)
            {
                using var result = effect.Apply(basePattern);

                // Create thumbnail: 120x120 for the effect result
                var thumb = DemoHelper.CreateWhiteCanvas(ThumbSize, ThumbSize);
                CopyRegion(result, 0, 0, ThumbSize, ThumbSize, thumb);
                var imageSource = thumb.ToImageSource();
                thumb.Dispose();

                // Build the card on the UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    EffectsContainer.Children.Add(BuildEffectCard(effect.DisplayName, imageSource));
                });

                // Small yield so the UI can update between cards
                await Task.Delay(30);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EffectsPage error: {ex}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Spinner.IsRunning = false;
                Spinner.IsVisible = false;
            });
        }
    }

    private static View BuildEffectCard(string name, ImageSource image)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        return new Border
        {
            WidthRequest = 140,
            HeightRequest = 170,
            Margin = new Thickness(4),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = isDark ? Color.FromArgb("#1C1C1E") : Colors.White,
            Stroke = isDark ? Color.FromArgb("#3C3C3E") : Color.FromArgb("#E0E0E0"),
            StrokeThickness = 1,
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Image
                    {
                        Source = image,
                        WidthRequest = ThumbSize,
                        HeightRequest = ThumbSize,
                        HorizontalOptions = LayoutOptions.Center,
                    },
                    new Label
                    {
                        Text = name,
                        FontSize = 13,
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = isDark ? Colors.White : Colors.Black,
                    },
                },
            },
        };
    }

    private static void CopyRegion(IPicture<byte> src, int dstX, int dstY, int width, int height, Picture8bpp dst)
    {
        for (int y = 0; y < height && y < src.Height; y++)
        {
            for (int x = 0; x < width && x < src.Width; x++)
            {
                int si = y * src.Width + x;
                int di = (dstY + y) * dst.Width + (dstX + x);
                dst.r[di] = src.r[si];
                dst.g[di] = src.g[si];
                dst.b[di] = src.b[si];
            }
        }
    }
}
