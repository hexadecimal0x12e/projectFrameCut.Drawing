using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;
using VecPoint = projectFrameCut.Drawing.Vector.Point;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal enum ShapeType
{
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Line,
    Polygon,
    CubicBezier,
    Arc,
}

internal enum PolygonPreset
{
    Triangle,
    Square,
    Pentagon,
    Hexagon,
    Star,
    Diamond,
}

internal static class VectorGenerator
{
    public static ImageSource RenderShape(
        ShapeType shapeType,
        // Geometry
        float shapeWidth, float shapeHeight, float cornerRadius,
        float lineX1, float lineY1, float lineX2, float lineY2,
        float bezierX1, float bezierY1, float bezierX2, float bezierY2,
        float bezierX3, float bezierY3, float bezierX4, float bezierY4,
        float arcX, float arcY, float arcRadiusX, float arcRadiusY,
        float arcStartAngle, float arcSweepAngle,
        PolygonPreset polygonPreset,
        // Appearance
        ushort fillR, ushort fillG, ushort fillB, float fillA,
        ushort strokeR, ushort strokeG, ushort strokeB, float strokeA, float strokeThickness,
        // Transform
        float posX, float posY, float rotation,
        // Output
        int outputWidth, int outputHeight, bool transparentBackground,
        AntiAliasMode aaMode = AntiAliasMode.None)
    {
        var element = CreateShape(
            shapeType, shapeWidth, shapeHeight, cornerRadius,
            lineX1, lineY1, lineX2, lineY2,
            bezierX1, bezierY1, bezierX2, bezierY2,
            bezierX3, bezierY3, bezierX4, bezierY4,
            arcX, arcY, arcRadiusX, arcRadiusY,
            arcStartAngle, arcSweepAngle,
            polygonPreset,
            fillR, fillG, fillB, fillA,
            strokeR, strokeG, strokeB, strokeA, strokeThickness,
            posX, posY, rotation);

        var canvas = new VectorPicture();
        canvas.Elements.Add(element);
        var picture = VectorToIPicture.Convert(canvas, outputWidth, outputHeight, transparentBackground, aaMode);
        return picture.ToImageSource();
    }

    private static VectorCanvasElement CreateShape(
        ShapeType type,
        float shapeWidth, float shapeHeight, float cornerRadius,
        float lineX1, float lineY1, float lineX2, float lineY2,
        float bezierX1, float bezierY1, float bezierX2, float bezierY2,
        float bezierX3, float bezierY3, float bezierX4, float bezierY4,
        float arcX, float arcY, float arcRadiusX, float arcRadiusY,
        float arcStartAngle, float arcSweepAngle,
        PolygonPreset polygonPreset,
        ushort fillR, ushort fillG, ushort fillB, float fillA,
        ushort strokeR, ushort strokeG, ushort strokeB, float strokeA, float strokeThickness,
        float posX, float posY, float rotation)
    {
        ShapeCanvasElement shape = type switch
        {
            ShapeType.Rectangle => ShapeCanvasElement.DrawRectangle(shapeWidth, shapeHeight),
            ShapeType.RoundedRectangle => ShapeCanvasElement.DrawRoundedRectangle(shapeWidth, shapeHeight, cornerRadius),
            ShapeType.Ellipse => ShapeCanvasElement.DrawEllipse(shapeWidth, shapeHeight),
            ShapeType.Line => ShapeCanvasElement.DrawLine(lineX1, lineY1, lineX2, lineY2),
            ShapeType.Polygon => MakePolygon(polygonPreset),
            ShapeType.CubicBezier => ShapeCanvasElement.DrawCubicBezier(
                bezierX1, bezierY1, bezierX2, bezierY2, bezierX3, bezierY3, bezierX4, bezierY4),
            ShapeType.Arc => ShapeCanvasElement.DrawArc(
                arcX, arcY, arcRadiusX, arcRadiusY, arcStartAngle, arcSweepAngle),
            _ => ShapeCanvasElement.DrawRectangle(shapeWidth, shapeHeight),
        };

        shape = shape
            .WithFill(fillR, fillG, fillB, fillA)
            .WithStroke(strokeR, strokeG, strokeB, strokeA, strokeThickness)
            .WithPosition(posX, posY);

        shape.Rotation = rotation;
        return shape;
    }

    private static ShapeCanvasElement MakePolygon(PolygonPreset preset)
    {
        VecPoint[] points = preset switch
        {
            PolygonPreset.Triangle => CreatePolygonPoints(3, 0.5f),
            PolygonPreset.Square => CreatePolygonPoints(4, 0.5f),
            PolygonPreset.Pentagon => CreatePolygonPoints(5, 0.5f),
            PolygonPreset.Hexagon => CreatePolygonPoints(6, 0.5f),
            PolygonPreset.Star => CreateStarPoints(5, 0.5f, 0.2f),
            PolygonPreset.Diamond => [
                new VecPoint(0.5f, 0),
                new VecPoint(1, 0.5f),
                new VecPoint(0.5f, 1),
                new VecPoint(0, 0.5f),
            ],
            _ => CreatePolygonPoints(3, 0.5f),
        };

        return ShapeCanvasElement.DrawPolygon(points);
    }

    private static VecPoint[] CreatePolygonPoints(int sides, float radius)
    {
        var points = new VecPoint[sides];
        for (int i = 0; i < sides; i++)
        {
            double angle = 2 * Math.PI * i / sides - Math.PI / 2;
            points[i] = new VecPoint(
                (float)(0.5 + radius * Math.Cos(angle)),
                (float)(0.5 + radius * Math.Sin(angle)));
        }
        return points;
    }

    private static VecPoint[] CreateStarPoints(int points, float outerRadius, float innerRadius)
    {
        var result = new VecPoint[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI * i / points - Math.PI / 2;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            result[i] = new VecPoint(
                (float)(0.5 + radius * Math.Cos(angle)),
                (float)(0.5 + radius * Math.Sin(angle)));
        }
        return result;
    }
}
