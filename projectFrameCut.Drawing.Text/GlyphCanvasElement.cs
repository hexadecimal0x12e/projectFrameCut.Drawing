using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Text;

/// <summary>
/// A <see cref="VectorCanvasElement"/> that renders a TrueType glyph outline
/// as vector segments (lines and quadratic Béziers). Supports both fill and stroke.
/// </summary>
/// <remarks>
/// Glyph coordinates are scaled from font units to normalized canvas space using
/// <c>FontSize / UnitsPerEm</c>. The element's origin (RelativeX/Y) sits at the
/// font baseline — ascenders extend upward (negative Y) and descenders downward.
/// </remarks>
/// <example>
/// <code>
/// using var font = FontFace.Load("font.ttf");
/// ushort glyphIndex = font.GetGlyphIndex('A');
/// Glyph? glyph = font.GetGlyph(glyphIndex);
///
/// var canvas = new VectorPicture();
/// canvas.Elements.Add(
///     new GlyphCanvasElement(glyph, font.UnitsPerEm)
///     {
///         FontSize = 0.08f,
///         RelativeX = 0.5f,
///         RelativeY = 0.5f,
///     }
///     .WithFill(0, 0, ushort.MaxValue)      // blue fill
///     .WithStroke(0, 0, 0, 0.5f, 2f)       // black outline
/// );
/// </code>
/// </remarks>
public sealed class GlyphCanvasElement : VectorCanvasElement
{
    private readonly Glyph _glyph;
    private readonly ushort _unitsPerEm;
    private VectorSegment[]? _cached;

    // ──────────────────────────────────────────────
    //  Properties
    // ──────────────────────────────────────────────

    /// <summary>Desired glyph height in normalized canvas coordinates (0–1).</summary>
    public float FontSize { get; set; } = 0.1f;

    public ushort StrokeR { get; set; }
    public ushort StrokeG { get; set; }
    public ushort StrokeB { get; set; }
    public float StrokeA { get; set; } = 1f;
    public float StrokeThickness { get; set; } = 1f;

    public ushort FillR { get; set; }
    public ushort FillG { get; set; }
    public ushort FillB { get; set; }
    public float FillA { get; set; } = 1f;

    // ──────────────────────────────────────────────
    //  Constructor
    // ──────────────────────────────────────────────

    /// <param name="glyph">The glyph outline to render (must not be null or empty).</param>
    /// <param name="unitsPerEm">EM square size from the parent <c>FontFace</c>.</param>
    public GlyphCanvasElement(Glyph glyph, ushort unitsPerEm)
    {
        ArgumentNullException.ThrowIfNull(glyph);
        if (unitsPerEm == 0)
            throw new ArgumentException("UnitsPerEm must be greater than zero.", nameof(unitsPerEm));
        _glyph = glyph;
        _unitsPerEm = unitsPerEm;
        // Glyphs are intrinsically isotropic — the X/Y dimensions of a glyph are coupled
        // by the font's metrics. If the renderer scaled X and Y with the canvas width/height
        // independently, non-square selections would horizontally stretch every character.
        UseUniformScale = true;
    }

    // ──────────────────────────────────────────────
    //  Fluent API
    // ──────────────────────────────────────────────

    public GlyphCanvasElement WithStroke(ushort r, ushort g, ushort b, float a = 1f, float thickness = 1f)
    {
        StrokeR = r; StrokeG = g; StrokeB = b; StrokeA = a; StrokeThickness = thickness;
        _cached = null;
        return this;
    }

    public GlyphCanvasElement WithFill(ushort r, ushort g, ushort b, float a = 1f)
    {
        FillR = r; FillG = g; FillB = b; FillA = a;
        _cached = null;
        return this;
    }

    public GlyphCanvasElement WithPosition(float x, float y)
    {
        RelativeX = x; RelativeY = y;
        return this;
    }

    public GlyphCanvasElement WithLayer(int index)
    {
        LayerIndex = index;
        return this;
    }

    public GlyphCanvasElement WithFontSize(float size)
    {
        FontSize = size;
        _cached = null;
        return this;
    }

    // ──────────────────────────────────────────────
    //  Draw
    // ──────────────────────────────────────────────

    public override VectorSegment[] Draw()
    {
        if (_cached is not null)
            return _cached;

        if (_glyph.IsEmpty)
            return _cached = [];

        float scale = FontSize / _unitsPerEm;
        bool hasFill = FillA > 0f;
        bool hasStroke = StrokeThickness > 0f && StrokeA > 0f;

        if (!hasFill && !hasStroke)
            return _cached = [];

        // Flatten ALL contours into polygons using the same subdivision.
        var flattened = new List<Point>[_glyph.Contours.Length];
        for (int i = 0; i < _glyph.Contours.Length; i++)
        {
            var pts = FlattenContour(_glyph.Contours[i], scale);
            flattened[i] = pts.Count >= 3 ? pts : null!;
        }

        // Classify: outer (negative signed area) vs. holes (positive signed area).
        // Use the contour with largest absolute area as the main polygon.
        int mainIndex = -1;
        float bestAbs = -1f;
        for (int i = 0; i < flattened.Length; i++)
        {
            if (flattened[i] is not { Count: >= 3 }) continue;
            float absA = MathF.Abs(SignedArea(flattened[i]));
            if (absA > bestAbs) { bestAbs = absA; mainIndex = i; }
        }

        if (mainIndex < 0)
            return _cached = [];

        var holesList = new List<Point[]>();
        for (int i = 0; i < flattened.Length; i++)
        {
            if (i == mainIndex) continue;
            if (flattened[i] is { Count: >= 3 } h)
                holesList.Add(h.ToArray());
        }

        var segments = new List<VectorSegment>();

        // Fill: single polygon with holes.
        if (hasFill)
        {
            segments.Add(new PolygonVectorSegment
            {
                Points = [.. flattened[mainIndex]],
                Holes = holesList.Count > 0 ? holesList.ToArray() : null,
                FillR = FillR,
                FillG = FillG,
                FillB = FillB,
                FillA = FillA,
                Thickness = 0,
                StrokeA = 0f,
            });
        }

        // Stroke: EVERY contour (outer + holes) as individual polygons,
        // using the exact same flattened vertices as the fill — no gaps, no seams.
        if (hasStroke)
        {
            for (int i = 0; i < flattened.Length; i++)
            {
                if (flattened[i] is not { Count: >= 3 }) continue;
                segments.Add(new PolygonVectorSegment
                {
                    Points = flattened[i].ToArray(),
                    FillA = 0f,
                    Thickness = StrokeThickness,
                    StrokeR = StrokeR,
                    StrokeG = StrokeG,
                    StrokeB = StrokeB,
                    StrokeA = StrokeA,
                });
            }
        }

        return _cached = segments.ToArray();
    }

    // ──────────────────────────────────────────────
    //  Private — flatten curves to polygon
    // ──────────────────────────────────────────────

    /// <summary>
    /// Walk a TrueType contour and produce a closed polygon by flattening
    /// quadratic Bézier curves into line segments.
    /// </summary>
    /// <remarks>
    /// <see cref="GlyphPoint.Y"/> is already Y-down (canvas convention) —
    /// the CFF and glyf parsers both negate Y when reading font units. So
    /// the polygon's Y axis matches the canvas's Y-down coordinate system
    /// directly. Earlier versions of this method re-negated Y, which flipped
    /// glyphs upside down on render.
    /// </remarks>
    private static List<Point> FlattenContour(GlyphPoint[] contour, float scale)
    {
        int n = contour.Length;
        if (n < 2) return [];

        var result = new List<Point>();

        // Locate the first on-curve point.
        int firstOnCurve = -1;
        for (int i = 0; i < n; i++)
            if (contour[i].OnCurve) { firstOnCurve = i; break; }

        float curX, curY;
        int startIdx, idx;

        if (firstOnCurve >= 0)
        {
            curX = contour[firstOnCurve].X * scale;
            curY = contour[firstOnCurve].Y * scale;
            startIdx = firstOnCurve;
            idx = (firstOnCurve + 1) % n;
            result.Add(new Point(curX, curY));
        }
        else
        {
            // All off-curve: synthetic on-curve at midpoint of last and first.
            curX = (contour[n - 1].X + contour[0].X) * 0.5f * scale;
            curY = (contour[n - 1].Y + contour[0].Y) * 0.5f * scale;
            startIdx = -1;
            idx = 0;
            result.Add(new Point(curX, curY));
        }

        int consumed = 0;
        while (consumed < n)
        {
            var p1 = contour[idx];

            if (p1.OnCurve)
            {
                result.Add(new Point(p1.X * scale, p1.Y * scale));
                curX = p1.X * scale;
                curY = p1.Y * scale;
                idx = (idx + 1) % n;
                consumed++;
            }
            else
            {
                var p2 = contour[(idx + 1) % n];
                float p2x = p2.X * scale;
                float p2y = p2.Y * scale;

                if (p2.OnCurve)
                {
                    FlattenQuadraticBezier(curX, curY,
                        p1.X * scale, p1.Y * scale, p2x, p2y, result, 0);
                    curX = p2x;
                    curY = p2y;
                    idx = (idx + 2) % n;
                    consumed += 2;
                }
                else
                {
                    float midX = (p1.X + p2.X) * 0.5f * scale;
                    float midY = (p1.Y + p2.Y) * 0.5f * scale;
                    FlattenQuadraticBezier(curX, curY,
                        p1.X * scale, p1.Y * scale, midX, midY, result, 0);
                    curX = midX;
                    curY = midY;
                    idx = (idx + 1) % n;
                    consumed++;
                }
            }
        }

        return result;
    }

    /// <summary>Compute signed area of a polygon (Y-down). Negative = counter-clockwise (outer), positive = clockwise (hole).</summary>
    private static float SignedArea(List<Point> pts)
    {
        float area = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            int j = (i + 1) % pts.Count;
            area += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
        }
        return area * 0.5f;
    }

    /// <summary>
    /// Recursively subdivide a quadratic Bézier until flat, appending
    /// intermediate points to <paramref name="result"/> (endpoint included).
    /// </summary>
    private static void FlattenQuadraticBezier(
        float x0, float y0, float x1, float y1, float x2, float y2,
        List<Point> result, int depth)
    {
        // 深度上限：超过后强制把当前子曲线的端点加入结果，避免多边形
        // 因漏点而无法闭合。
        if (depth > 16)
        {
            result.Add(new Point(x2, y2));
            return;
        }

        float dx = x2 - x0;
        float dy = y2 - y0;
        float len2 = dx * dx + dy * dy;

        if (len2 <= 0f)
        {
            result.Add(new Point(x2, y2));
            return;
        }

        float t = ((x1 - x0) * dx + (y1 - y0) * dy) / len2;
        float d = MathF.Abs((y1 - y0) - t * dy) + MathF.Abs((x1 - x0) - t * dx);
        // 阈值从 0.001f 收紧到 0.0000001f（控制点到弦的曼哈顿距离² < 弦长² × 0.0000001），
        // 偏差约 0.01% 弦长，对常规渲染尺寸是亚像素级。
        if (d * d < len2 * 0.0000001f)
        {
            result.Add(new Point(x2, y2));
            return;
        }

        // De Casteljau subdivision.
        float mx01 = (x0 + x1) * 0.5f, my01 = (y0 + y1) * 0.5f;
        float mx12 = (x1 + x2) * 0.5f, my12 = (y1 + y2) * 0.5f;
        float mx012 = (mx01 + mx12) * 0.5f, my012 = (my01 + my12) * 0.5f;

        // Note: do NOT add mx012 here — the left recursive call will add it
        // as its endpoint when it terminates, avoiding duplicate vertices.
        FlattenQuadraticBezier(x0, y0, mx01, my01, mx012, my012, result, depth + 1);
        FlattenQuadraticBezier(mx012, my012, mx12, my12, x2, y2, result, depth + 1);
    }
}
