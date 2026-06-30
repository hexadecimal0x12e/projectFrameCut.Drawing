namespace projectFrameCut.Drawing.Vector;

/// <summary>
/// A concrete <see cref="VectorCanvasElement"/> that wraps one or more <see cref="VectorSegment"/> records
/// with convenient static factory methods. Use the <c>Draw*</c> methods to create instances, then set
/// <see cref="VectorCanvasElement.RelativeX"/> and <see cref="VectorCanvasElement.RelativeY"/> to position
/// them on the canvas.
/// </summary>
/// <example>
/// <code>
/// var canvas = new VectorPicture();
/// canvas.Elements.Add(
///     ShapeCanvasElement.DrawRectangle(0.5f, 0.3f)
///         .WithStroke(ushort.MaxValue, 0, 0, 1f, 2f)
///         .WithFill(0, ushort.MaxValue, 0, 0.3f)
///     {
///         RelativeX = 0.25f,
///         RelativeY = 0.35f,
///     });
/// </code>
/// </example>
public sealed class ShapeCanvasElement : VectorCanvasElement
{
    private VectorSegment[] _segments;

    internal ShapeCanvasElement(VectorSegment[] segments)
    {
        _segments = segments;
    }

    public override VectorSegment[] Draw() => _segments;

    // ---------------------------------------------------------------
    // Animation support
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a deep clone of this element with independently-owned segment data.
    /// Each segment record is copied via a <c>with</c> expression so the clone
    /// shares no references with the original.
    /// </summary>
    public ShapeCanvasElement Clone()
    {
        var newSegments = new VectorSegment[_segments.Length];
        for (int i = 0; i < _segments.Length; i++)
            newSegments[i] = _segments[i] with { };

        var clone = new ShapeCanvasElement(newSegments)
        {
            RelativeX = RelativeX,
            RelativeY = RelativeY,
            Rotation = Rotation,
            LayerIndex = LayerIndex,
            BaseX = BaseX,
            BaseY = BaseY,
            UseUniformScale = UseUniformScale,
        };
        return clone;
    }

    /// <summary>
    /// Applies a transformation function to every segment, replacing the
    /// internal segment array with the results. This is the entry point
    /// for the animation system to modify segment-level appearance (e.g.
    /// fill opacity, stroke colour) per frame.
    /// </summary>
    /// <param name="transform">A function that produces a new segment from an existing one.</param>
    public void TransformSegments(Func<VectorSegment, VectorSegment> transform)
    {
        if (_segments == null || _segments.Length == 0)
            return;

        var newSegments = new VectorSegment[_segments.Length];
        for (int i = 0; i < _segments.Length; i++)
            newSegments[i] = transform(_segments[i]);

        _segments = newSegments;
    }

    // ---------------------------------------------------------------
    // Fluent styling
    // ---------------------------------------------------------------

    /// <summary>Set stroke (outline) properties on all segments.</summary>
    public ShapeCanvasElement WithStroke(ushort r, ushort g, ushort b, float a = 1f, float thickness = 1f)
    {
        for (var i = 0; i < _segments.Length; i++)
            _segments[i] = _segments[i] with { StrokeR = r, StrokeG = g, StrokeB = b, StrokeA = a, Thickness = thickness };
        return this;
    }

    /// <summary>Set fill properties on all segments.</summary>
    public ShapeCanvasElement WithFill(ushort r, ushort g, ushort b, float a = 1f)
    {
        for (var i = 0; i < _segments.Length; i++)
            _segments[i] = _segments[i] with { FillR = r, FillG = g, FillB = b, FillA = a };
        return this;
    }

    /// <summary>Set the element position on the canvas (relative 0–1).</summary>
    public ShapeCanvasElement WithPosition(float x, float y)
    {
        RelativeX = x;
        RelativeY = y;
        return this;
    }

    /// <summary>Set the layer index for z-order.</summary>
    public ShapeCanvasElement WithLayer(int index)
    {
        LayerIndex = index;
        return this;
    }

    // ---------------------------------------------------------------
    // Factory methods
    // ---------------------------------------------------------------

    /// <summary>Create a straight line from (x1,y1) to (x2,y2).</summary>
    public static ShapeCanvasElement DrawLine(float x1, float y1, float x2, float y2) =>
        new([new StraightLineVectorSegment
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create a rectangle with the given <paramref name="width"/> and <paramref name="height"/>.</summary>
    public static ShapeCanvasElement DrawRectangle(float width, float height) =>
        new([new RectangleVectorSegment
        {
            Width = width, Height = height,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create a rounded rectangle.</summary>
    public static ShapeCanvasElement DrawRoundedRectangle(float width, float height, float cornerRadius) =>
        new([new RoundedRectangleVectorSegment
        {
            Width = width, Height = height, CornerRadius = cornerRadius,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create an ellipse centered at the element's origin.</summary>
    public static ShapeCanvasElement DrawEllipse(float radiusX, float radiusY) =>
        new([new EllipseVectorSegment
        {
            RadiusX = radiusX, RadiusY = radiusY,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create a cubic Bézier curve.</summary>
    public static ShapeCanvasElement DrawCubicBezier(
        float x1, float y1, float x2, float y2,
        float x3, float y3, float x4, float y4) =>
        new([new CubicBezierVectorSegment
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            X3 = x3, Y3 = y3, X4 = x4, Y4 = y4,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create a quadratic Bézier curve.</summary>
    public static ShapeCanvasElement DrawQuadraticBezier(
        float x1, float y1, float x2, float y2, float x3, float y3) =>
        new([new QuadraticBezierVectorSegment
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, X3 = x3, Y3 = y3,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create an arc centered at (x, y).</summary>
    public static ShapeCanvasElement DrawArc(
        float x, float y, float radiusX, float radiusY,
        float startAngle, float sweepAngle) =>
        new([new ArcVectorSegment
        {
            X = x, Y = y,
            RadiusX = radiusX, RadiusY = radiusY,
            StartAngle = startAngle, SweepAngle = sweepAngle,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create a closed polygon from the given points.</summary>
    public static ShapeCanvasElement DrawPolygon(params Point[] points) =>
        new([new PolygonVectorSegment
        {
            Points = points,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);

    /// <summary>Create an open polyline from the given points.</summary>
    public static ShapeCanvasElement DrawPolyline(params Point[] points) =>
        new([new PolylineVectorSegment
        {
            Points = points,
            Thickness = 1f,
            StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
        }]);
}
