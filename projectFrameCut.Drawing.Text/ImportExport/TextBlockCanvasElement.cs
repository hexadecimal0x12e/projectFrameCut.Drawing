using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.ImportExport;

/// <summary>
/// A <see cref="VectorCanvasElement"/> that composites multiple child
/// <see cref="VectorCanvasElement"/>s (typically per-character
/// <see cref="GlyphCanvasElement"/> instances) into a single draw call.
/// </summary>
/// <remarks>
/// <para>
/// Each child's <see cref="VectorCanvasElement.Draw"/> output is offset by
/// the child's <see cref="VectorCanvasElement.RelativeX"/> /
/// <see cref="VectorCanvasElement.RelativeY"/> cursor position so that all
/// glyphs sit in the same local coordinate space relative to this element's
/// origin.
/// </para>
/// <para>
/// This element uses <see cref="VectorCanvasElement.UseUniformScale"/> =
/// <c>true</c> so that text glyphs are never distorted on non-square canvases.
/// The SVG import pipeline detects this flag and adjusts
/// <see cref="VectorCanvasElement.RelativeX"/> /
/// <see cref="VectorCanvasElement.RelativeY"/> accordingly.
/// </para>
/// </remarks>
public sealed class TextBlockCanvasElement : VectorCanvasElement
{
    private readonly VectorCanvasElement[] _children;

    /// <summary>
    /// Initialises a new composite text element from the given glyph elements.
    /// </summary>
    /// <param name="children">Per-character glyph elements with their
    /// <see cref="VectorCanvasElement.RelativeX"/> /
    /// <see cref="VectorCanvasElement.RelativeY"/> set to cursor positions
    /// in uniform-normalised space.</param>
    public TextBlockCanvasElement(VectorCanvasElement[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        UseUniformScale = true;
    }

    /// <inheritdoc />
    public override VectorSegment[] Draw()
    {
        if (_children.Length == 0)
            return [];

        var result = new List<VectorSegment>(_children.Length * 16);

        foreach (var child in _children)
        {
            float dx = child.RelativeX;
            float dy = child.RelativeY;

            foreach (var seg in child.Draw())
                result.Add(OffsetSegment(seg, dx, dy));
        }

        return result.ToArray();
    }

    // ---------------------------------------------------------------
    //  Private helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Offset every coordinate in a <see cref="VectorSegment"/> by (dx, dy).
    /// </summary>
    private static VectorSegment OffsetSegment(VectorSegment seg, float dx, float dy)
        => seg switch
        {
            StraightLineVectorSegment s => s with
            {
                X1 = s.X1 + dx, Y1 = s.Y1 + dy,
                X2 = s.X2 + dx, Y2 = s.Y2 + dy,
            },
            // RoundedRectangle must come before Rectangle because it inherits from it.
            RoundedRectangleVectorSegment s => s with { X = s.X + dx, Y = s.Y + dy },
            RectangleVectorSegment s => s with { X = s.X + dx, Y = s.Y + dy },
            EllipseVectorSegment s => s with { X = s.X + dx, Y = s.Y + dy },
            CubicBezierVectorSegment s => s with
            {
                X1 = s.X1 + dx, Y1 = s.Y1 + dy,
                X2 = s.X2 + dx, Y2 = s.Y2 + dy,
                X3 = s.X3 + dx, Y3 = s.Y3 + dy,
                X4 = s.X4 + dx, Y4 = s.Y4 + dy,
            },
            QuadraticBezierVectorSegment s => s with
            {
                X1 = s.X1 + dx, Y1 = s.Y1 + dy,
                X2 = s.X2 + dx, Y2 = s.Y2 + dy,
                X3 = s.X3 + dx, Y3 = s.Y3 + dy,
            },
            ArcVectorSegment s => s with { X = s.X + dx, Y = s.Y + dy },
            PolygonVectorSegment s => s with
            {
                Points = OffsetPoints(s.Points, dx, dy),
                Holes = s.Holes?.Select(h => OffsetPoints(h, dx, dy)).ToArray(),
            },
            PolylineVectorSegment s => s with
            {
                Points = OffsetPoints(s.Points, dx, dy),
            },
            _ => seg,
        };

    private static Point[] OffsetPoints(ReadOnlySpan<Point> pts, float dx, float dy)
    {
        var result = new Point[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            result[i] = new Point(pts[i].X + dx, pts[i].Y + dy);
        return result;
    }
}
