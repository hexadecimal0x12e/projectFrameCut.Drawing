using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Vector;
using System.Numerics;

namespace projectFrameCut.Drawing.Text;

/// <summary>A COLR glyph represented by ordered CPAL-coloured outline layers.</summary>
public sealed class ColorGlyphCanvasElement : VectorCanvasElement
{
    private readonly FontFace _font;
    private readonly ColorGlyphLayer[] _layers;
    private VectorSegment[]? _cached;

    internal ColorGlyphCanvasElement(FontFace font, ColorGlyphLayer[] layers, float fontSize, float opacity)
    {
        _font = font;
        _layers = layers;
        FontSize = fontSize;
        Opacity = opacity;
        UseUniformScale = true;
    }

    public float FontSize { get; }
    public float Opacity { get; }

    public override VectorSegment[] Draw()
    {
        if (_cached is not null) return _cached;
        var segments = new List<VectorSegment>();
        foreach (var layer in _layers)
        {
            Glyph? glyph;
            try { glyph = _font.GetVariedGlyph(layer.GlyphId); }
            catch { continue; }
            if (glyph is null || glyph.IsEmpty) continue;
            var e = new GlyphCanvasElement(glyph, _font.UnitsPerEm)
            {
                FontSize = FontSize,
                GlyphTransform = layer.GlyphTransform,
                // COLR PaintGlyph supplies a clipped fill, not an outline.
                // Keep the layer stroke-free even if GlyphCanvasElement's
                // defaults are changed by a caller in the future.
                StrokeThickness = 0f,
                StrokeA = 0f,
            };
            if (layer.Brush is SolidColorBrush solid)
            {
                e.WithFill(solid.Color.R, solid.Color.G, solid.Color.B,
                    Math.Clamp(solid.Color.A * Opacity, 0f, 1f));
                segments.AddRange(e.Draw());
            }
            else
            {
                e.WithFill(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, 1f);
                VectorGradientBrush gradient = ToVectorGradient(layer.Brush, layer.BrushTransform);
                foreach (var segment in e.Draw())
                {
                    if (segment is PolygonVectorSegment polygon)
                        segments.Add(new GradientPolygonVectorSegment
                        {
                            Points = polygon.Points,
                            AdditionalContours = polygon.AdditionalContours,
                            Holes = polygon.Holes,
                            Gradient = gradient,
                            Opacity = Opacity, FillA = 1f,
                        });
                }
            }
        }
        return _cached = segments.ToArray();
    }

    private VectorGradientBrush ToVectorGradient(ColorBrush brush, Matrix3x2 transform)
    {
        float scale = FontSize / _font.UnitsPerEm;
        Point P(float x, float y)
        {
            Vector2 v = Vector2.Transform(new Vector2(x, -y), transform);
            return new Point(v.X * scale, v.Y * scale);
        }
        float Radius(float r) => Vector2.TransformNormal(new Vector2(r, 0), transform).Length() * scale;
        float SweepAngle(float angle)
        {
            Vector2 direction = Vector2.TransformNormal(
                new Vector2(MathF.Cos(angle), -MathF.Sin(angle)), transform);
            return MathF.Atan2(direction.Y, direction.X);
        }
        static VectorGradientExtendMode Extend(byte value) => value switch
        { 1 => VectorGradientExtendMode.Repeat, 2 => VectorGradientExtendMode.Reflect, _ => VectorGradientExtendMode.Pad };
        VectorGradientStop[] Stops(ColorStop[] stops) => stops.Select(s =>
            new VectorGradientStop(s.Offset, s.Color.R, s.Color.G, s.Color.B, s.Color.A)).ToArray();

        return brush switch
        {
            LinearColorBrush b => MakeLinear(b),
            RadialColorBrush b => MakeRadial(b),
            SweepColorBrush b => MakeSweep(b),
            _ => throw new InvalidOperationException("Unsupported COLR gradient brush."),
        };

        VectorGradientBrush MakeLinear(LinearColorBrush b)
        {
            Point p0 = P(b.X0, b.Y0), p1 = P(b.X1, b.Y1), p2 = P(b.X2, b.Y2);
            // COLR projects colours parallel to p0-p2. Convert the three-point
            // representation to its equivalent p0-p3 gradient vector.
            float rx = p2.X - p0.X, ry = p2.Y - p0.Y;
            float nx = -ry, ny = rx;
            float nn = nx * nx + ny * ny;
            float projection = nn <= 1e-12f ? 0f
                : ((p1.X - p0.X) * nx + (p1.Y - p0.Y) * ny) / nn;
            Point p3 = new(p0.X + nx * projection, p0.Y + ny * projection);
            return new() { Kind = VectorGradientKind.Linear, ExtendMode = Extend(b.Extend), Stops = Stops(b.Stops),
                X0 = p0.X, Y0 = p0.Y, X1 = p3.X, Y1 = p3.Y, X2 = p2.X, Y2 = p2.Y };
        }
        VectorGradientBrush MakeRadial(RadialColorBrush b)
        {
            Point p0 = P(b.X0, b.Y0), p1 = P(b.X1, b.Y1);
            return new() { Kind = VectorGradientKind.Radial, ExtendMode = Extend(b.Extend), Stops = Stops(b.Stops),
                X0 = p0.X, Y0 = p0.Y, Radius0 = Radius(b.R0), X1 = p1.X, Y1 = p1.Y, Radius1 = Radius(b.R1) };
        }
        VectorGradientBrush MakeSweep(SweepColorBrush b)
        {
            Point p = P(b.CenterX, b.CenterY);
            float start = SweepAngle(b.StartAngle), end = SweepAngle(b.EndAngle);
            float sourceSpan = b.EndAngle - b.StartAngle;
            if (MathF.Abs(sourceSpan) < 1e-6f) sourceSpan = MathF.Tau;
            float orientation = transform.GetDeterminant() < 0f ? 1f : -1f;
            float desiredSpan = sourceSpan * orientation;
            float transformedSpan = end - start;
            if (desiredSpan > 0f)
            {
                while (transformedSpan <= 0f) transformedSpan += MathF.Tau;
            }
            else
            {
                while (transformedSpan >= 0f) transformedSpan -= MathF.Tau;
            }
            return new() { Kind = VectorGradientKind.Sweep, ExtendMode = Extend(b.Extend), Stops = Stops(b.Stops),
                X0 = p.X, Y0 = p.Y, StartAngle = start, EndAngle = start + transformedSpan };
        }
    }
}
