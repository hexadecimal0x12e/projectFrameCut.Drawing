namespace projectFrameCut.Drawing.Vector
{
    /// <summary>A 2D point in normalized canvas space (0..1).</summary>
    /// <param name="X">X coordinate (0..1).</param>
    /// <param name="Y">Y coordinate (0..1).</param>
    public readonly record struct Point(float X, float Y);

    /// <summary>Base record for all vector segments with stroke and fill properties.</summary>
    public record VectorSegment
    {
        /// <summary>Stroke thickness in normalized units.</summary>
        public float Thickness { get; init; }
        /// <summary>Stroke red channel (0..65535).</summary>
        public ushort StrokeR { get; init; }
        /// <summary>Stroke green channel (0..65535).</summary>
        public ushort StrokeG { get; init; }
        /// <summary>Stroke blue channel (0..65535).</summary>
        public ushort StrokeB { get; init; }
        /// <summary>Stroke alpha (0.0 = transparent, 1.0 = opaque).</summary>
        public float StrokeA { get; init; } = 1.0f;

        /// <summary>Fill red channel (0..65535).</summary>
        public ushort FillR { get; init; }
        /// <summary>Fill green channel (0..65535).</summary>
        public ushort FillG { get; init; }
        /// <summary>Fill blue channel (0..65535).</summary>
        public ushort FillB { get; init; }
        /// <summary>Fill alpha (0.0 = transparent / no fill, 1.0 = opaque).</summary>
        public float FillA { get; init; }
    }

    /// <summary>A straight line segment between two points.</summary>
    public record StraightLineVectorSegment : VectorSegment
    {
        /// <summary>Start point X coordinate (0..1).</summary>
        public float X1 { get; init; }
        /// <summary>Start point Y coordinate (0..1).</summary>
        public float Y1 { get; init; }
        /// <summary>End point X coordinate (0..1).</summary>
        public float X2 { get; init; }
        /// <summary>End point Y coordinate (0..1).</summary>
        public float Y2 { get; init; }
    }

    /// <summary>An axis-aligned rectangle segment.</summary>
    public record RectangleVectorSegment : VectorSegment
    {
        /// <summary>Top-left X coordinate (0..1).</summary>
        public float X { get; init; }
        /// <summary>Top-left Y coordinate (0..1).</summary>
        public float Y { get; init; }
        /// <summary>Rectangle width in normalized units.</summary>
        public float Width { get; init; }
        /// <summary>Rectangle height in normalized units.</summary>
        public float Height { get; init; }
    }

    /// <summary>A rectangle segment with rounded corners.</summary>
    public record RoundedRectangleVectorSegment : RectangleVectorSegment
    {
        /// <summary>Corner radius in normalized units.</summary>
        public float CornerRadius { get; init; }
    }

    /// <summary>An ellipse segment defined by center and radii.</summary>
    public record EllipseVectorSegment : VectorSegment
    {
        /// <summary>Center X coordinate (0..1).</summary>
        public float X { get; init; }
        /// <summary>Center Y coordinate (0..1).</summary>
        public float Y { get; init; }
        /// <summary>Horizontal radius in normalized units.</summary>
        public float RadiusX { get; init; }
        /// <summary>Vertical radius in normalized units.</summary>
        public float RadiusY { get; init; }
    }

    /// <summary>A cubic B�zier curve segment with four control points.</summary>
    public record CubicBezierVectorSegment : VectorSegment
    {
        /// <summary>First control point X.</summary>
        public float X1 { get; init; }
        /// <summary>First control point Y.</summary>
        public float Y1 { get; init; }
        /// <summary>Second control point X.</summary>
        public float X2 { get; init; }
        /// <summary>Second control point Y.</summary>
        public float Y2 { get; init; }
        /// <summary>Third control point X.</summary>
        public float X3 { get; init; }
        /// <summary>Third control point Y.</summary>
        public float Y3 { get; init; }
        /// <summary>Fourth control point X.</summary>
        public float X4 { get; init; }
        /// <summary>Fourth control point Y.</summary>
        public float Y4 { get; init; }
    }

    /// <summary>A quadratic B�zier curve segment with three control points.</summary>
    public record QuadraticBezierVectorSegment : VectorSegment
    {
        /// <summary>Start point X coordinate.</summary>
        public float X1 { get; init; }
        /// <summary>Start point Y coordinate.</summary>
        public float Y1 { get; init; }
        /// <summary>Control point X coordinate.</summary>
        public float X2 { get; init; }
        /// <summary>Control point Y coordinate.</summary>
        public float Y2 { get; init; }
        /// <summary>End point X coordinate.</summary>
        public float X3 { get; init; }
        /// <summary>End point Y coordinate.</summary>
        public float Y3 { get; init; }
    }

    /// <summary>An elliptical arc segment.</summary>
    public record ArcVectorSegment : VectorSegment
    {
        /// <summary>Center X coordinate (0..1).</summary>
        public float X { get; init; }
        /// <summary>Center Y coordinate (0..1).</summary>
        public float Y { get; init; }
        /// <summary>Horizontal radius in normalized units.</summary>
        public float RadiusX { get; init; }
        /// <summary>Vertical radius in normalized units.</summary>
        public float RadiusY { get; init; }
        /// <summary>Start angle in radians.</summary>
        public float StartAngle { get; init; }
        /// <summary>Sweep angle in radians (positive = clockwise).</summary>
        public float SweepAngle { get; init; }
    }

    /// <summary>A closed polygon segment defined by a list of vertices.</summary>
    public record PolygonVectorSegment : VectorSegment
    {
        /// <summary>The polygon vertices in order.</summary>
        public required Point[] Points { get; init; }

        /// <summary>
        /// Optional hole polygons. When set, all edges (outer + holes) are
        /// rendered together using the even-odd fill rule so that holes
        /// punch through the main polygon instead of being filled over.
        /// </summary>
        public Point[][]? Holes { get; init; }
    }

    /// <summary>An open polyline segment defined by a list of vertices.</summary>
    public record PolylineVectorSegment : VectorSegment
    {
        /// <summary>The polyline vertices in order.</summary>
        public required Point[] Points { get; init; }
    }
}
