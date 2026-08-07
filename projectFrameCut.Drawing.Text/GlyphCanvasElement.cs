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

        // Flatten all contours into polygons using the same subdivision.
        // Do not infer holes from winding alone: many fonts (especially CFF and
        // some converted outlines) do not preserve the expected outer/hole
        // direction consistently, which turns counters like "o" or "口" solid.
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

        int contourCount = flattenedContours.Count;
        var absoluteAreas = new float[contourCount];
        var nestingDepths = new int[contourCount];
        var holeParentIndices = new int[contourCount];
        var minXs = new float[contourCount];
        var minYs = new float[contourCount];
        var maxXs = new float[contourCount];
        var maxYs = new float[contourCount];
        Array.Fill(holeParentIndices, -1);

        for (int i = 0; i < contourCount; i++)
        {
            absoluteAreas[i] = MathF.Abs(SignedArea(flattenedContours[i]));
            GetBounds(flattenedContours[i], out minXs[i], out minYs[i], out maxXs[i], out maxYs[i]);
        }

        for (int i = 0; i < contourCount; i++)
        {
            int depth = 0;

            for (int j = 0; j < contourCount; j++)
            {
                if (i == j || absoluteAreas[j] <= absoluteAreas[i])
                    continue;

                if (!BoundsContain(minXs[j], minYs[j], maxXs[j], maxYs[j], minXs[i], minYs[i], maxXs[i], maxYs[i]))
                    continue;

                if (IsContourContained(flattenedContours[i], flattenedContours[j]))
                    depth++;
            }

            nestingDepths[i] = depth;
        }

        for (int i = 0; i < contourCount; i++)
        {
            if ((nestingDepths[i] & 1) == 0)
                continue;

            int parentDepth = nestingDepths[i] - 1;
            float bestParentArea = float.MaxValue;

            for (int j = 0; j < contourCount; j++)
            {
                if (i == j || nestingDepths[j] != parentDepth || absoluteAreas[j] <= absoluteAreas[i])
                    continue;

                if (!BoundsContain(minXs[j], minYs[j], maxXs[j], maxYs[j], minXs[i], minYs[i], maxXs[i], maxYs[i]))
                    continue;

                if (IsContourContained(flattenedContours[i], flattenedContours[j]) && absoluteAreas[j] < bestParentArea)
                {
                    bestParentArea = absoluteAreas[j];
                    holeParentIndices[i] = j;
                }
            }
        }

        var holesByOuter = new List<Point[]>[contourCount];
        for (int i = 0; i < contourCount; i++)
            holesByOuter[i] = [];

        for (int i = 0; i < contourCount; i++)
        {
            int parentIndex = holeParentIndices[i];
            if (parentIndex >= 0)
            {
                var hole = flattenedContours[i];
                float holeArea = SignedArea(hole);
                float parentArea = SignedArea(flattenedContours[parentIndex]);

                if (MathF.Sign(holeArea) == MathF.Sign(parentArea))
                {
                    // Same winding as parent: this is an overlapping filled stroke, not a
                    // counter/hole. Fonts like Source Han Serif encode stroke intersections as
                    // separate same-wound contours that overlap; forcing a reversal would punch
                    // them as holes and leave white gaps at stroke intersections.
                    // Promote to outer so the fill loop renders it as an independent filled region.
                    nestingDepths[i] = 0;
                }
                else
                {
                    holesByOuter[parentIndex].Add(hole);
                }
            }
        }

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

        // Fill: every outer contour (and promoted same-winding inner contours) gets
        // its own fill segment. Holes are attached only when the inner contour has
        // opposite winding from its parent (the standard typographic convention for
        // counter shapes). Same-winding inner contours are overlapping filled strokes
        // (common in CFF/CJK fonts) and are rendered as independent fills instead of
        // being punched as holes — which would create white gaps at stroke intersections.
        if (hasFill)
        {
            for (int i = 0; i < contourCount; i++)
            {
                if ((nestingDepths[i] & 1) != 0)
                    continue;

                segments.Add(new PolygonVectorSegment
                {
                    Points = flattenedContours[i],
                    Holes = holesByOuter[i].Count > 0 ? holesByOuter[i].ToArray() : null,
                    FillR = FillR,
                    FillG = FillG,
                    FillB = FillB,
                    FillA = FillA,
                    Thickness = 0,
                    StrokeA = 0f,
                });
            }
        }

        // Stroke: EVERY contour (outer + holes) as individual polygons,
        // using the exact same flattened vertices as the fill — no gaps, no seams.
        if (hasStroke)
        {
            for (int i = 0; i < contourCount; i++)
            {
                segments.Add(new PolygonVectorSegment
                {
                    Points = flattenedContours[i],
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

    /// <summary>Compute signed area of a polygon in Y-down coordinates.</summary>
    private static float SignedArea(ReadOnlySpan<Point> pts)
    {
        float area = 0f;
        for (int i = 0; i < pts.Length; i++)
        {
            int j = (i + 1) % pts.Length;
            area += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
        }
        return area * 0.5f;
    }

    private static bool PointInPolygon(Point point, ReadOnlySpan<Point> polygon)
    {
        bool inside = false;
        float px = point.X;
        float py = point.Y;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            float xi = polygon[i].X;
            float yi = polygon[i].Y;
            float xj = polygon[j].X;
            float yj = polygon[j].Y;

            bool crosses = ((yi > py) != (yj > py))
                && (px < ((xj - xi) * (py - yi) / (yj - yi)) + xi);

            if (crosses)
                inside = !inside;
        }

        return inside;
    }

    private static bool IsContourContained(ReadOnlySpan<Point> candidate, ReadOnlySpan<Point> container)
    {
        bool sawInteriorPoint = false;

        for (int i = 0; i < candidate.Length; i++)
        {
            if (PointOnPolygonBoundary(candidate[i], container))
                continue;

            if (!PointInPolygon(candidate[i], container))
                return false;

            sawInteriorPoint = true;
        }

        return sawInteriorPoint || candidate.Length > 0;
    }

    private static bool PointOnPolygonBoundary(Point point, ReadOnlySpan<Point> polygon)
    {
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (PointOnSegment(point, polygon[j], polygon[i]))
                return true;
        }

        return false;
    }

    private static bool PointOnSegment(Point point, Point a, Point b)
    {
        const float epsilon = 1e-6f;

        float cross = (point.Y - a.Y) * (b.X - a.X) - (point.X - a.X) * (b.Y - a.Y);
        if (MathF.Abs(cross) > epsilon)
            return false;

        float dot = (point.X - a.X) * (b.X - a.X) + (point.Y - a.Y) * (b.Y - a.Y);
        if (dot < -epsilon)
            return false;

        float lengthSquared = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
        if (dot - lengthSquared > epsilon)
            return false;

        return true;
    }

    private static void GetBounds(ReadOnlySpan<Point> polygon, out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = maxX = polygon[0].X;
        minY = maxY = polygon[0].Y;

        for (int i = 1; i < polygon.Length; i++)
        {
            var point = polygon[i];
            if (point.X < minX) minX = point.X;
            if (point.X > maxX) maxX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.Y > maxY) maxY = point.Y;
        }
    }

    private static bool BoundsContain(
        float outerMinX, float outerMinY, float outerMaxX, float outerMaxY,
        float innerMinX, float innerMinY, float innerMaxX, float innerMaxY)
        => innerMinX >= outerMinX
        && innerMaxX <= outerMaxX
        && innerMinY >= outerMinY
        && innerMaxY <= outerMaxY;

    private static Point[] ReverseContour(ReadOnlySpan<Point> contour)
    {
        var reversed = contour.ToArray();
        Array.Reverse(reversed);
        return reversed;
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
