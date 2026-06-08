using projectFrameCut.Drawing.Gallary.Demos;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Gallary.Pages;

public partial class VectorPage : ContentPage
{
    private bool _initialized;

    public VectorPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        PickerShapeType.SelectedIndex = 0;
        PickerPolygon.SelectedIndex = 0;
    }

    private void OnShapeTypeChanged(object? sender, EventArgs e)
    {
        int index = PickerShapeType.SelectedIndex;
        GeoRect.IsVisible = index is 0 or 1;
        GeoCorner.IsVisible = index == 1;
        GeoEllipse.IsVisible = index == 2;
        GeoLine.IsVisible = index == 3;
        GeoPolygon.IsVisible = index == 4;
        GeoBezier.IsVisible = index == 5;
        GeoArc.IsVisible = index == 6;
    }

    private async void OnRenderClicked(object? sender, EventArgs e)
    {
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;
        DemoImage.IsVisible = false;
        RenderButton.IsEnabled = false;

        try
        {
            int outputW = int.TryParse(InputWidth.Text, out var w) ? Math.Clamp(w, 1, 4096) : 500;
            int outputH = int.TryParse(InputHeight.Text, out var h) ? Math.Clamp(h, 1, 4096) : 400;
            bool transparent = SwitchTransparent.IsToggled;
            var aaMode = PickerAntiAlias.SelectedIndex switch
            {
                1 => AntiAliasMode.SSAA2x,
                2 => AntiAliasMode.SSAA4x,
                _ => AntiAliasMode.None,
            };

            var src = await Task.Run(() => VectorGenerator.RenderShape(
                GetShapeType(),
                // Geometry
                (float)SliderRectW.Value,
                (float)SliderRectH.Value,
                (float)SliderCorner.Value,
                (float)SliderLineX1.Value,
                (float)SliderLineY1.Value,
                (float)SliderLineX2.Value,
                (float)SliderLineY2.Value,
                (float)SliderBezX1.Value,
                (float)SliderBezY1.Value,
                (float)SliderBezX2.Value,
                (float)SliderBezY2.Value,
                (float)SliderBezX3.Value,
                (float)SliderBezY3.Value,
                (float)SliderBezX4.Value,
                (float)SliderBezY4.Value,
                (float)SliderArcX.Value,
                (float)SliderArcY.Value,
                (float)SliderArcRX.Value,
                (float)SliderArcRY.Value,
                (float)SliderArcStart.Value,
                (float)SliderArcSweep.Value,
                GetPolygonPreset(),
                // Fill
                (ushort)(SliderFillR.Value * 257.0),
                (ushort)(SliderFillG.Value * 257.0),
                (ushort)(SliderFillB.Value * 257.0),
                (float)SliderFillA.Value,
                // Stroke
                (ushort)(SliderStrokeR.Value * 257.0),
                (ushort)(SliderStrokeG.Value * 257.0),
                (ushort)(SliderStrokeB.Value * 257.0),
                (float)SliderStrokeA.Value,
                (float)SliderStrokeThickness.Value,
                // Transform
                (float)SliderPosX.Value,
                (float)SliderPosY.Value,
                (float)SliderRotation.Value,
                // Output
                outputW, outputH, transparent, aaMode));

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DemoImage.Source = src;
                DemoImage.IsVisible = src != null;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VectorPage render error: {ex}");
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            RenderButton.IsEnabled = true;
        }
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider)
            UpdateSliderLabel(slider);
    }

    private void OnStrokeToggled(object? sender, ToggledEventArgs e)
    {
        StrokeControls.IsEnabled = e.Value;
    }

    private void UpdateSliderLabel(Slider slider)
    {
        Label? label = slider switch
        {
            _ when slider == SliderRectW => LabelRectW,
            _ when slider == SliderRectH => LabelRectH,
            _ when slider == SliderCorner => LabelCorner,
            _ when slider == SliderEllipseRX => LabelEllipseRX,
            _ when slider == SliderEllipseRY => LabelEllipseRY,
            _ when slider == SliderLineX1 => LabelLineX1,
            _ when slider == SliderLineY1 => LabelLineY1,
            _ when slider == SliderLineX2 => LabelLineX2,
            _ when slider == SliderLineY2 => LabelLineY2,
            _ when slider == SliderBezX1 => LabelBezX1,
            _ when slider == SliderBezY1 => LabelBezY1,
            _ when slider == SliderBezX2 => LabelBezX2,
            _ when slider == SliderBezY2 => LabelBezY2,
            _ when slider == SliderBezX3 => LabelBezX3,
            _ when slider == SliderBezY3 => LabelBezY3,
            _ when slider == SliderBezX4 => LabelBezX4,
            _ when slider == SliderBezY4 => LabelBezY4,
            _ when slider == SliderArcX => LabelArcX,
            _ when slider == SliderArcY => LabelArcY,
            _ when slider == SliderArcRX => LabelArcRX,
            _ when slider == SliderArcRY => LabelArcRY,
            _ when slider == SliderArcStart => LabelArcStart,
            _ when slider == SliderArcSweep => LabelArcSweep,
            _ when slider == SliderFillR => LabelFillR,
            _ when slider == SliderFillG => LabelFillG,
            _ when slider == SliderFillB => LabelFillB,
            _ when slider == SliderFillA => LabelFillA,
            _ when slider == SliderStrokeR => LabelStrokeR,
            _ when slider == SliderStrokeG => LabelStrokeG,
            _ when slider == SliderStrokeB => LabelStrokeB,
            _ when slider == SliderStrokeA => LabelStrokeA,
            _ when slider == SliderStrokeThickness => LabelStrokeThickness,
            _ when slider == SliderPosX => LabelPosX,
            _ when slider == SliderPosY => LabelPosY,
            _ when slider == SliderRotation => LabelRotation,
            _ => null,
        };

        if (label != null)
        {
            if (slider == SliderRotation)
                label.Text = $"{slider.Value * 180 / Math.PI:F0}°";
            else if (slider == SliderArcStart || slider == SliderArcSweep)
                label.Text = $"{slider.Value:F0}°";
            else if (slider == SliderFillA || slider == SliderStrokeA)
                label.Text = $"{slider.Value:F2}";
            else if (slider == SliderStrokeThickness)
                label.Text = $"{slider.Value:F3}";
            else
                label.Text = $"{slider.Value:F2}";
        }
    }

    private ShapeType GetShapeType() => PickerShapeType.SelectedIndex switch
    {
        1 => ShapeType.RoundedRectangle,
        2 => ShapeType.Ellipse,
        3 => ShapeType.Line,
        4 => ShapeType.Polygon,
        5 => ShapeType.CubicBezier,
        6 => ShapeType.Arc,
        _ => ShapeType.Rectangle,
    };

    private PolygonPreset GetPolygonPreset() => PickerPolygon.SelectedIndex switch
    {
        1 => PolygonPreset.Square,
        2 => PolygonPreset.Pentagon,
        3 => PolygonPreset.Hexagon,
        4 => PolygonPreset.Star,
        5 => PolygonPreset.Diamond,
        _ => PolygonPreset.Triangle,
    };
}
