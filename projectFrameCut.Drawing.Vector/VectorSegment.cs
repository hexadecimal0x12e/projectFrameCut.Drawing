namespace projectFrameCut.Drawing.Vector
{
    public readonly record struct Point(float X, float Y);

    public record VectorSegment
    {
        // Stroke (outline) properties
        public float Thickness { get; init; }
        public ushort StrokeR { get; init; }
        public ushort StrokeG { get; init; }
        public ushort StrokeB { get; init; }
        public float StrokeA { get; init; } = 1.0f;

        // Fill properties (default alpha 0 = transparent / no fill)
        public ushort FillR { get; init; }
        public ushort FillG { get; init; }
        public ushort FillB { get; init; }
        public float FillA { get; init; }
    }

    public record StraightLineVectorSegment : VectorSegment
    {
        public float X1 { get; init; }
        public float Y1 { get; init; }
        public float X2 { get; init; }
        public float Y2 { get; init; }
    }

    public record RectangleVectorSegment : VectorSegment
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Width { get; init; }
        public float Height { get; init; }
    }

    public record RoundedRectangleVectorSegment : RectangleVectorSegment
    {
        public float CornerRadius { get; init; }
    }

    public record EllipseVectorSegment : VectorSegment
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float RadiusX { get; init; }
        public float RadiusY { get; init; }
    }

    public record CubicBezierVectorSegment : VectorSegment
    {
        public float X1 { get; init; }
        public float Y1 { get; init; }
        public float X2 { get; init; }
        public float Y2 { get; init; }
        public float X3 { get; init; }
        public float Y3 { get; init; }
        public float X4 { get; init; }
        public float Y4 { get; init; }
    }

    public record QuadraticBezierVectorSegment : VectorSegment
    {
        public float X1 { get; init; }
        public float Y1 { get; init; }
        public float X2 { get; init; }
        public float Y2 { get; init; }
        public float X3 { get; init; }
        public float Y3 { get; init; }
    }

    public record ArcVectorSegment : VectorSegment
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float RadiusX { get; init; }
        public float RadiusY { get; init; }
        public float StartAngle { get; init; }
        public float SweepAngle { get; init; }
    }

    public record PolygonVectorSegment : VectorSegment
    {
        public required Point[] Points { get; init; }
    }

    public record PolylineVectorSegment : VectorSegment
    {
        public required Point[] Points { get; init; }
    }
}
