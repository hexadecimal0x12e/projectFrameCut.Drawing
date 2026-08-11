using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using System.Numerics;

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
/// </example>
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

    /// <summary>Optional COLR paint transform expressed in font design units.</summary>
    public Matrix3x2 GlyphTransform { get; set; } = Matrix3x2.Identity;

    // ──────────────────────────────────────────────
    //  Debug
    // ──────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c>, draws a semi-transparent gray square representing the
    /// full EM square (FontSize × FontSize) behind the glyph outline. Useful
    /// for debugging glyph positioning, alignment, and spacing.
    /// </summary>
    public bool ShowEmBox
    {
        get => _showEmBox;
        set { _showEmBox = value; _cached = null; }
    }
    private bool _showEmBox;

    public ushort StrokeR { get; set; }
    public ushort StrokeG { get; set; }
    public ushort StrokeB { get; set; }
    // Glyphs are normally filled outlines. A stroke must be explicitly requested
    // through WithStroke; otherwise small glyphs receive an unintended extra rim.
    public float StrokeA { get; set; }
    public float StrokeThickness { get; set; }

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

    public GlyphCanvasElement WithShowEmBox(bool show = true)
    {
        ShowEmBox = show;
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

        if (!hasFill && !hasStroke && !_showEmBox)
            return _cached = [];

        // Preserve every source contour and its direction. OpenType glyphs use
        // non-zero winding: a glyph may contain both counters and intentional
        // overlapping filled contours, which cannot be recovered reliably from
        // a geometric outer/hole containment hierarchy.
        var flattenedContours = new List<Point[]>();
        foreach (var sourceContour in _glyph.Contours)
        {
            var pts = FlattenContour(sourceContour, scale);
            if (!GlyphTransform.IsIdentity)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 v = Vector2.Transform(new Vector2(pts[i].X / scale, pts[i].Y / scale), GlyphTransform);
                    pts[i] = new Point(v.X * scale, v.Y * scale);
                }
            }
            if (pts.Count >= 3)
                flattenedContours.Add(pts.ToArray());
        }

        if (flattenedContours.Count == 0)
            return _cached = [];

        var segments = new List<VectorSegment>();

        // Debug EM square — a FontSize × FontSize gray box from (0, -FontSize)
        // to (FontSize, 0) representing the full em square in TrueType design space
        // (Y-up originals were negated to canvas Y-down convention).
        if (_showEmBox)
        {
            float em = FontSize;
            segments.Add(new PolygonVectorSegment
            {
                Points =
                [
                    new Point(0, -em),
                    new Point(em, -em),
                    new Point(em, 0),
                    new Point(0, 0),
                ],
                FillR = 32768, FillG = 32768, FillB = 32768, FillA = 0.08f,
                Thickness = 1f,
                StrokeR = 32768, StrokeG = 32768, StrokeB = 32768, StrokeA = 0.35f,
            });
        }

        if (hasFill)
        {
            segments.Add(new PolygonVectorSegment
            {
                Points = flattenedContours[0],
                AdditionalContours = flattenedContours.Count > 1
                    ? flattenedContours.Skip(1).ToArray()
                    : null,
                FillR = FillR,
                FillG = FillG,
                FillB = FillB,
                FillA = FillA,
                Thickness = 0,
                StrokeA = 0f,
            });
        }

        // Stroke every source contour individually, using the same flattened
        // vertices as the fill path.
        if (hasStroke)
        {
            foreach (Point[] contour in flattenedContours)
            {
                segments.Add(new PolygonVectorSegment
                {
                    Points = contour,
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
        // 控制点到弦的曼哈顿距离² < 弦长² × 0.0001，对 ≤4096px 画布
        // 偏差在亚像素范围内（≤0.4px），同时避免产生过多顶点。
        if (d * d < len2 * 0.0001f)
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
