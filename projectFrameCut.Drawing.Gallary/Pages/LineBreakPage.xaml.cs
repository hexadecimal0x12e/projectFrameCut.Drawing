using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using projectFrameCut.Drawing.Gallary.Demos;
using projectFrameCut.Drawing.Gallary.Services;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Gallary.Pages;

public partial class LineBreakPage : ContentPage
{
    private List<FontFace> _fonts = [];
    private byte[]? _lastRenderedPng;

    public LineBreakPage()
    {
        InitializeComponent();

        DropGestureRecognizer dropGesture = new();
        dropGesture.Drop += async (s, e) =>
        {
            var paths = await FileDropHelper.GetFilePathsFromDrop(e);
            if (paths.Count > 0)
            {
                string path = paths[0];
                try
                {
                    var fonts = FontFace.AutoLoad(path);
                    foreach (var f in fonts)
                    {
                        _fonts.Add(f);
                        FontPicker.ItemsSource = _fonts.Select(f => $"{f.DisplayName} {f.SubfamilyName} ({f.TargetLanguages.First().DisplayName})").ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load font from dropped file: {ex}");
                }
            }
            FontPicker.SelectedIndex = _fonts.Count - 1;
        };
        MainScroll.GestureRecognizers.Add(dropGesture);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_fonts.Count > 0) return;

        _ = Task.Run(() =>
        {
            _fonts = FontDiscoveryService.DiscoverSystemFonts();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                FontPicker.ItemsSource = _fonts.Select(f => $"{f.DisplayName} {f.SubfamilyName} ({f.TargetLanguages.First().DisplayName})").ToList();
                FontCountLabel.Text = $"共 {_fonts.Count} 个系统字体";

                if (_fonts.Count > 0)
                {
                    FontPicker.SelectedIndex = 0;
                }
                else
                {
                    Spinner.IsRunning = false;
                    Spinner.IsVisible = false;
                }
            });
        });
    }

    private async void OnFontSelected(object? sender, EventArgs e)
    {
        if (FontPicker.SelectedIndex < 0 || FontPicker.SelectedIndex >= _fonts.Count)
            return;
        await RenderCurrentConfig();
    }

    private async void OnBreakClicked(object? sender, EventArgs e)
    {
        if (FontPicker.SelectedIndex < 0 || FontPicker.SelectedIndex >= _fonts.Count)
            return;
        await RenderCurrentConfig();
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider)
        {
            Label? label = slider switch
            {
                _ when slider == SliderTargetWidth => LabelTargetWidth,
                _ when slider == SliderFontSize => LabelFontSize,
                _ => null,
            };
            if (label != null)
                label.Text = $"{slider.Value:F2}";
        }
    }

    private async void OnPreviewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_lastRenderedPng == null || _lastRenderedPng.Length == 0)
            return;

        try
        {
#if WINDOWS
            var tempFile = Path.Combine(FileSystem.CacheDirectory, "FrameCut_clipboard.png");
            await File.WriteAllBytesAsync(tempFile, _lastRenderedPng);

            var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempFile);
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
            };
            dataPackage.SetBitmap(
                Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(storageFile));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
#else
            await Clipboard.Default.SetTextAsync($"Image ({_lastRenderedPng.Length / 1024} KB)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy image to clipboard: {ex}");
        }
    }

    private async Task RenderCurrentConfig()
    {
        if (FontPicker.SelectedIndex < 0 || FontPicker.SelectedIndex >= _fonts.Count)
            return;

        var selected = _fonts[FontPicker.SelectedIndex];

        Spinner.IsRunning = true;
        Spinner.IsVisible = true;
        DemoImage.IsVisible = false;
        BreakButton.IsEnabled = false;

        try
        {
            int imageWidth = 1280;
            int imageHeight = 720;
            float targetWidth = (float)SliderTargetWidth.Value;
            float fontSize = (float)SliderFontSize.Value;

            var entry = new TextEntry
            {
                Text = (InputText.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'),
                FontName = selected.FamilyName,
                FontSize = fontSize,
                X = 0.02f,
                Y = 0.05f,
                FillR = 0,
                FillG = 0,
                FillB = 200,
                FillA = 1f,
            };

            var (broken, imgSource, pngBytes) = await Task.Run(() =>
                LineBreakGenerator.RenderBrokenText(selected, entry, targetWidth,
                    imageWidth, imageHeight, false, useDashWhenWordAcrossLineInLatin.IsToggled, allowPunctuationOverflowMaxWidthInCJK.IsToggled));

            _lastRenderedPng = pngBytes;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                BrokenTextDisplay.Text = string.IsNullOrEmpty(broken) ? "(empty)" : broken;

                if (imgSource != null)
                {
                    DemoImage.Source = imgSource;
                    DemoImage.IsVisible = true;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LineBreak error: {ex}");
            BrokenTextDisplay.Text = $"(error: {ex.Message})";
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            BreakButton.IsEnabled = true;
        }
    }
}
