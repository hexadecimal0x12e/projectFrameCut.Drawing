using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using projectFrameCut.Drawing.Gallary.Services;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;
using DrawTextAlignment = projectFrameCut.Drawing.Text.Entry.TextAlignment;
using DrawTextDecoration = projectFrameCut.Drawing.Text.Entry.TextDecoration;

namespace projectFrameCut.Drawing.Gallary.Pages;

public partial class TextPage : ContentPage
{
    private List<FontFace> _fonts = [];
    private bool _initialized;
    private byte[]? _lastRenderedPng;

    public TextPage()
    {
        InitializeComponent();
        DropGestureRecognizer dropGesture = new DropGestureRecognizer();
        dropGesture.Drop += async (s, e) =>
        {
            var paths = await FileDropHelper.GetFilePathsFromDrop(e);
            if (paths.Count > 0)
            {
                string path = paths[0];
                try
                {
                    var fonts = FontFace.AutoLoad(path);
                    foreach(var f in fonts)
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

        if (!_initialized)
        {
            _initialized = true;
            PickerAlignment.SelectedIndex = 0;
        }

        await RenderCurrentConfig();
    }

    private async void OnRenderClicked(object? sender, EventArgs e)
    {
        if (FontPicker.SelectedIndex < 0 || FontPicker.SelectedIndex >= _fonts.Count)
            return;

        await RenderCurrentConfig();
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider)
        {
            UpdateSliderLabel(slider);
        }
    }

    private void OnStrokeToggled(object? sender, ToggledEventArgs e)
    {
        StrokeControls.IsEnabled = e.Value;
    }

    private void OnEngineSelected(object? sender, EventArgs e)
    {
        // no immediate action needed
    }

    private void OnAlignmentChanged(object? sender, EventArgs e)
    {
        // no immediate action needed
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

    private void UpdateSliderLabel(Slider slider)
    {
        Label? label = slider switch
        {
            _ when slider == SliderFontSize => LabelFontSize,
            _ when slider == SliderX => LabelX,
            _ when slider == SliderY => LabelY,
            _ when slider == SliderRotation => LabelRotation,
            _ when slider == SliderFillR => LabelFillR,
            _ when slider == SliderFillG => LabelFillG,
            _ when slider == SliderFillB => LabelFillB,
            _ when slider == SliderFillA => LabelFillA,
            _ when slider == SliderStrokeR => LabelStrokeR,
            _ when slider == SliderStrokeG => LabelStrokeG,
            _ when slider == SliderStrokeB => LabelStrokeB,
            _ when slider == SliderStrokeA => LabelStrokeA,
            _ when slider == SliderStrokeThickness => LabelStrokeThickness,
            _ when slider == SliderCharSpacing => LabelCharSpacing,
            _ when slider == SliderWordSpacing => LabelWordSpacing,
            _ when slider == SliderLineSpacing => LabelLineSpacing,
            _ => null,
        };

        if (label != null)
        {
            if (slider == SliderRotation)
                label.Text = $"{slider.Value * 180 / Math.PI:F0}°";
            else if (slider == SliderFillA || slider == SliderStrokeA)
                label.Text = $"{slider.Value:F2}";
            else if (slider == SliderStrokeThickness)
                label.Text = $"{slider.Value:F3}";
            else if (slider == SliderFontSize || slider == SliderLineSpacing)
                label.Text = $"{slider.Value:F2}";
            else if (slider == SliderCharSpacing || slider == SliderWordSpacing)
                label.Text = $"{slider.Value:F3}";
            else
                label.Text = $"{slider.Value:F0}";
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
        RenderButton.IsEnabled = false;

        try
        {
            var entry = CollectTextEntry();
            int width = int.TryParse(InputWidth.Text, out var w) ? Math.Clamp(w, 1, 4096) : 1280;
            int height = int.TryParse(InputHeight.Text, out var h) ? Math.Clamp(h, 1, 4096) : 720;
            bool transparent = SwitchTransparent.IsToggled;
            var aaMode = PickerAntiAlias.SelectedIndex switch
            {
                1 => AntiAliasMode.SSAA2x,
                2 => AntiAliasMode.SSAA4x,
                3 => AntiAliasMode.SSAA8x,
                _ => AntiAliasMode.None,
            };

            var pngBytes = await Task.Run(() =>
            {
                VectorPicture vectorCanvas;
                if (PickerEngine.SelectedIndex == 1)
                {
                    var verticalEngine = new VerticalTypesettingEngine();
                    bool strokeEnabled = SwitchStroke.IsToggled;
                    vectorCanvas = verticalEngine.Layout(
                        entry.Text,
                        selected,
                        entry.FontSize,
                        entry.X,
                        entry.Y,
                        entry.LineSpacing,
                        keepNonCjkHorizontal: false,
                        fillR: entry.FillR, fillG: entry.FillG, fillB: entry.FillB, fillA: entry.FillA,
                        strokeR: strokeEnabled ? entry.StrokeR : (ushort)0,
                        strokeG: strokeEnabled ? entry.StrokeG : (ushort)0,
                        strokeB: strokeEnabled ? entry.StrokeB : (ushort)0,
                        strokeThickness: strokeEnabled ? entry.StrokeThickness : 0f,
                        flowDirection: entry.FlowDirection
                    );
                }
                else
                {
                    var renderEntry = entry with { FontName = selected.FamilyName };
                    var engine = new NormalTypesettingEngine { DebugMode = SwitchDebug.IsToggled };
                    vectorCanvas = engine.Layout(renderEntry, selected);
                }

                var picture = VectorToIPicture.Convert(vectorCanvas, width, height, transparent, aaMode);

                using var ms = new MemoryStream();
                var encoder = new PngPictureEncoder();
                picture.Save(ms, encoder);
                return ms.ToArray();
            });

            _lastRenderedPng = pngBytes;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DemoImage.Source = ImageSource.FromStream(() => new MemoryStream(pngBytes));
                DemoImage.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Render error: {ex}");
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            RenderButton.IsEnabled = true;
        }
    }

    private TextEntry CollectTextEntry()
    {
        var alignment = PickerAlignment.SelectedIndex switch
        {
            1 => DrawTextAlignment.Center,
            2 => DrawTextAlignment.Right,
            _ => DrawTextAlignment.Left,
        };

        var decoration = PickerDecoration.SelectedIndex switch
        {
            1 => DrawTextDecoration.Underline,
            2 => DrawTextDecoration.Strikethrough,
            _ => DrawTextDecoration.None,
        };

        var flowDirection = PickerFlowDirection.SelectedIndex switch
        {
            1 => TextFlowDirection.RightToLeft,
            _ => TextFlowDirection.LeftToRight,
        };

        bool strokeEnabled = SwitchStroke.IsToggled;

        return new TextEntry
        {
            Text = (InputText.Text ?? "Hello MAUI!").Replace("\r\n","\n").Replace('\r','\n'),
            FontName = _fonts[FontPicker.SelectedIndex].DisplayName,
            FontSize = (float)SliderFontSize.Value,
            X = (float)SliderX.Value,
            Y = (float)SliderY.Value,
            Rotation = (float)SliderRotation.Value,
            FillR = (ushort)(SliderFillR.Value * 257.0),
            FillG = (ushort)(SliderFillG.Value * 257.0),
            FillB = (ushort)(SliderFillB.Value * 257.0),
            FillA = (float)SliderFillA.Value,
            StrokeR = strokeEnabled ? (ushort)(SliderStrokeR.Value * 257.0) : (ushort)0,
            StrokeG = strokeEnabled ? (ushort)(SliderStrokeG.Value * 257.0) : (ushort)0,
            StrokeB = strokeEnabled ? (ushort)(SliderStrokeB.Value * 257.0) : (ushort)0,
            StrokeA = strokeEnabled ? (float)SliderStrokeA.Value : 0f,
            StrokeThickness = strokeEnabled ? (float)SliderStrokeThickness.Value : 0f,
            CharacterSpacing = (float)SliderCharSpacing.Value,
            WordSpacing = (float)SliderWordSpacing.Value,
            LineSpacing = (float)SliderLineSpacing.Value,
            Alignment = alignment,
            Decoration = decoration,
            FlowDirection = flowDirection,
        };
    }

}
