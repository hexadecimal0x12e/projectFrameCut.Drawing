using System.Globalization;
using System.Text;
using System.Xml.Linq;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Vector.ImportExport
{
    /// <summary>
    /// Concrete <see cref="VectorCanvasElement"/> that holds pre-computed segments.
    /// Used primarily for SVG import and programmatic canvas construction.
    /// </summary>
    internal class SegmentCollectionElement : VectorCanvasElement
    {
        private readonly VectorSegment[] _segments;

        public SegmentCollectionElement(params VectorSegment[] segments)
        {
            _segments = segments ?? [];
        }

        public override VectorSegment[] Draw() => _segments;
    }

    /// <summary>
    /// Contains all the information needed to render an SVG <c>&lt;text&gt;</c> element
    /// into a <see cref="VectorCanvasElement"/>.
    /// </summary>
    /// <remarks>
    /// Instances of this record are created by <see cref="SVGToVectorElement"/> during
    /// SVG import and passed to <see cref="SvgTextImportHandler"/> if one is registered.
    /// </remarks>
    public sealed record SvgTextImportContext
    {
        /// <summary>The text content to render.</summary>
        public required string Text { get; init; }

        /// <summary>The X coordinate in SVG user units (before canvas normalisation).</summary>
        public float X { get; init; }

        /// <summary>The Y coordinate in SVG user units (before canvas normalisation).</summary>
        public float Y { get; init; }

        /// <summary>Canvas width in SVG user units.</summary>
        public float CanvasWidth { get; init; }

        /// <summary>Canvas height in SVG user units.</summary>
        public float CanvasHeight { get; init; }

        /// <summary>CSS font-family value, or <c>null</c> if unspecified.</summary>
        public string? FontFamily { get; init; }

        /// <summary>Font size in SVG user units. Defaults to 16.</summary>
        public float FontSize { get; init; } = 16f;

        /// <summary>CSS font-weight value (e.g. <c>"bold"</c>, <c>"normal"</c>), or <c>null</c>.</summary>
        public string? FontWeight { get; init; }

        /// <summary>CSS font-style value (e.g. <c>"italic"</c>, <c>"normal"</c>), or <c>null</c>.</summary>
        public string? FontStyle { get; init; }

        /// <summary>CSS text-anchor value (<c>"start"</c>, <c>"middle"</c>, <c>"end"</c>), or <c>null</c>.</summary>
        public string? TextAnchor { get; init; }

        /// <summary>Fill color red component (16-bit). Zero when no fill.</summary>
        public ushort FillR { get; init; }

        /// <summary>Fill color green component (16-bit).</summary>
        public ushort FillG { get; init; }

        /// <summary>Fill color blue component (16-bit).</summary>
        public ushort FillB { get; init; }

        /// <summary>Fill alpha (opacity). Zero when no fill.</summary>
        public float FillA { get; init; }

        /// <summary>Stroke color red component (16-bit).</summary>
        public ushort StrokeR { get; init; }

        /// <summary>Stroke color green component (16-bit).</summary>
        public ushort StrokeG { get; init; }

        /// <summary>Stroke color blue component (16-bit).</summary>
        public ushort StrokeB { get; init; }

        /// <summary>Stroke alpha (opacity). Zero when no stroke.</summary>
        public float StrokeA { get; init; }

        /// <summary>Stroke width in SVG user units.</summary>
        public float StrokeWidth { get; init; }
    }

    /// <summary>
    /// Handles rendering of an SVG <c>&lt;text&gt;</c> element during import.
    /// </summary>
    /// <param name="context">The text rendering context.</param>
    /// <returns>A <see cref="VectorCanvasElement"/> representing the rendered text,
    /// or <c>null</c> if the handler cannot or chooses not to render this element.</returns>
    public delegate VectorCanvasElement? SvgTextImportHandler(SvgTextImportContext context);

    /// <summary>
    /// Provides bidirectional conversion between <see cref="VectorPicture"/> and SVG markup.
    /// </summary>
    public static class SVGToVectorElement
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>
        /// When <see langword="true"/>, export writes 16-bit color + float alpha as
        /// custom XML attributes (<c>fill_projectFrameCut.Drawing.Color</c> /
        /// <c>stroke_projectFrameCut.Drawing.Color</c>) alongside standard 8-bit
        /// attributes. Import reads these attributes and uses them with full
        /// priority, bypassing the lossy 8-bit colour parsing and opacity
        /// multiplication.
        /// </summary>
        public static bool UsePrivateColorSavingMode { get; set; }

        /// <summary>
        /// Gets or sets an optional handler for importing <c>&lt;text&gt;</c> elements
        /// from SVG markup.
        /// <para>
        /// When set, <see cref="ImportFromSvg(string)"/> calls this handler for every
        /// <c>&lt;text&gt;</c> element it encounters. The handler receives the text
        /// content along with font, position, and colour attributes, and should return
        /// a <see cref="VectorCanvasElement"/> whose <see cref="VectorCanvasElement.Draw"/>
        /// produces the rendered glyphs, or <c>null</c> to silently skip the element.
        /// </para>
        /// <para>
        /// The returned element's <see cref="VectorCanvasElement.RelativeX"/> and
        /// <see cref="VectorCanvasElement.RelativeY"/> are automatically set to the
        /// correct canvas-normalized position by the importer. The handler should
        /// produce segment coordinates relative to the element origin (i.e. in local
        /// 0..1 space).
        /// </para>
        /// </summary>
        /// <example>
        /// <code>
        /// SVGToVectorElement.TextImportHandler = ctx =>
        /// {
        ///     // Render text using an external text layout engine
        ///     var segs = MyTextRenderer.Render(ctx.Text, ctx.FontFamily, ctx.FontSize, ...);
        ///     return new SegmentCollectionElement(segs);
        /// };
        /// </code>
        /// </example>
        public static SvgTextImportHandler? TextImportHandler { get; set; }

        // =====================================================================
        // Export: VectorPicture → SVG
        // =====================================================================

        /// <summary>
        /// Export the canvas to an SVG string.
        /// </summary>
        /// <param name="canvas">Vector canvas to export.</param>
        /// <param name="width">Output SVG viewport width in pixels.</param>
        /// <param name="height">Output SVG viewport height in pixels.</param>
        public static string ExportToSvg(VectorPicture canvas, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(null,
                    $"Canvas size must be positive. Got {width}x{height}.");

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">");

            foreach (var element in canvas.Elements.OrderBy(e => e.LayerIndex))
            {
                float ox, oy, scaleX, scaleY;
                if (element.UseUniformScale)
                {
                    float us = Math.Min(width, height);
                    ox = element.BaseX * width + element.RelativeX * us;
                    oy = element.BaseY * height + element.RelativeY * us;
                    scaleX = us;
                    scaleY = us;
                }
                else
                {
                    ox = element.RelativeX * width;
                    oy = element.RelativeY * height;
                    scaleX = width;
                    scaleY = height;
                }

                var segments = element.Draw();
                var fillPolys = new List<PolygonVectorSegment>();
                var strokePolys = new List<PolygonVectorSegment>();
                var otherSegs = new List<VectorSegment>();

                foreach (var seg in segments)
                {
                    if (seg is GradientPolygonVectorSegment)
                        otherSegs.Add(seg);
                    else if (seg is PolygonVectorSegment poly)
                    {
                        bool hasFill = poly.FillA > 0f;
                        bool hasStroke = poly.StrokeA > 0f && poly.Thickness > 0f;
                        if (hasFill && !hasStroke)
                            fillPolys.Add(poly);
                        else if (!hasFill && hasStroke)
                            strokePolys.Add(poly);
                        else
                            otherSegs.Add(seg);
                    }
                    else
                    {
                        otherSegs.Add(seg);
                    }
                }

                // Keep fill polygons separate
                foreach (var poly in fillPolys)                                                           
                    sb.Append(PolygonToSvg(poly, ox, oy, scaleX, scaleY));    
                if (strokePolys.Count > 0)
                    sb.Append(MergedPolygonPath(strokePolys, ox, oy, scaleX, scaleY));
                foreach (var seg in otherSegs)
                {
                    var tag = SegmentToSvgTag(seg, ox, oy, scaleX, scaleY);
                    if (tag != null)
                        sb.Append(tag);
                }
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Segment → SVG tag helpers
        // ---------------------------------------------------------------

        private static string? SegmentToSvgTag(VectorSegment seg, float ox, float oy, float scaleX, float scaleY)
        {
            return seg switch
            {
                StraightLineVectorSegment s => LineToSvg(s, ox, oy, scaleX, scaleY),
                RoundedRectangleVectorSegment s => RoundedRectToSvg(s, ox, oy, scaleX, scaleY),
                RectangleVectorSegment s => RectToSvg(s, ox, oy, scaleX, scaleY),
                EllipseVectorSegment s => EllipseToSvg(s, ox, oy, scaleX, scaleY),
                CubicBezierVectorSegment s => CubicBezierToSvg(s, ox, oy, scaleX, scaleY),
                QuadraticBezierVectorSegment s => QuadraticBezierToSvg(s, ox, oy, scaleX, scaleY),
                ArcVectorSegment s => ArcToSvg(s, ox, oy, scaleX, scaleY),
                GradientPolygonVectorSegment s => GradientPolygonToSvg(s, ox, oy, scaleX, scaleY),
                PolygonVectorSegment s => PolygonToSvg(s, ox, oy, scaleX, scaleY),
                PolylineVectorSegment s => PolylineToSvg(s, ox, oy, scaleX, scaleY),
                _ => null,
            };
        }

        private static string GradientPolygonToSvg(GradientPolygonVectorSegment s,
            float ox, float oy, float scaleX, float scaleY)
        {
            string id = "g" + Guid.NewGuid().ToString("N");
            var d = new StringBuilder(); AppendContourPath(d, s.Points, ox, oy, scaleX, scaleY);
            if (s.AdditionalContours is not null)
                foreach (var contour in s.AdditionalContours) AppendContourPath(d, contour, ox, oy, scaleX, scaleY);
            if (s.Holes is not null) foreach (var h in s.Holes) AppendContourPath(d, h, ox, oy, scaleX, scaleY);
            var stroke = new StringBuilder();
            if (s.Thickness > 0f && s.StrokeA > 0f)
            {
                stroke.Append($" stroke=\"{ColorToHex(s.StrokeR, s.StrokeG, s.StrokeB)}\"");
                stroke.Append($" stroke-width=\"{Fmt(s.Thickness)}\"");
                if (s.StrokeA < 1f) stroke.Append($" stroke-opacity=\"{Fmt(s.StrokeA)}\"");
            }
            if (s.Gradient.Stops.Length == 0 || s.Opacity <= 0f)
                return $"<path fill-rule=\"nonzero\" d=\"{d}\" fill=\"none\"{stroke}/>";

            string spread = s.Gradient.ExtendMode switch
            { VectorGradientExtendMode.Repeat => "repeat", VectorGradientExtendMode.Reflect => "reflect", _ => "pad" };
            string colorInterpolation = s.Gradient.ColorSpace == VectorGradientColorSpace.LinearRgb
                ? "linearRGB"
                : "sRGB";
            var stops = new StringBuilder();
            foreach (var stop in s.Gradient.Stops)
                stops.Append($"<stop offset=\"{Fmt(stop.Offset * 100)}%\" stop-color=\"{ColorToHex(stop.R, stop.G, stop.B)}\" stop-opacity=\"{Fmt(stop.A * s.Opacity)}\"/>");
            string def;
            if (s.Gradient.Kind == VectorGradientKind.Linear)
                def = $"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" spreadMethod=\"{spread}\" color-interpolation=\"{colorInterpolation}\" x1=\"{Fmt(CX(s.Gradient.X0, ox, scaleX))}\" y1=\"{Fmt(CY(s.Gradient.Y0, oy, scaleY))}\" x2=\"{Fmt(CX(s.Gradient.X1, ox, scaleX))}\" y2=\"{Fmt(CY(s.Gradient.Y1, oy, scaleY))}\">{stops}</linearGradient>";
            else if (s.Gradient.Kind == VectorGradientKind.Radial)
                def = $"<radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" spreadMethod=\"{spread}\" color-interpolation=\"{colorInterpolation}\" fx=\"{Fmt(CX(s.Gradient.X0, ox, scaleX))}\" fy=\"{Fmt(CY(s.Gradient.Y0, oy, scaleY))}\" fr=\"{Fmt(s.Gradient.Radius0 * scaleX)}\" cx=\"{Fmt(CX(s.Gradient.X1, ox, scaleX))}\" cy=\"{Fmt(CY(s.Gradient.Y1, oy, scaleY))}\" r=\"{Fmt(s.Gradient.Radius1 * scaleX)}\">{stops}</radialGradient>";
            else
            {
                var first = s.Gradient.Stops[0];
                return $"<path fill-rule=\"nonzero\" d=\"{d}\" fill=\"{ColorToHex(first.R, first.G, first.B)}\" fill-opacity=\"{Fmt(first.A * s.Opacity)}\"{stroke}/>";
            }
            return $"<defs>{def}</defs><path fill-rule=\"nonzero\" d=\"{d}\" fill=\"url(#{id})\"{stroke}/>";
        }

        /// <summary>
        /// Merge a list of same-style <see cref="PolygonVectorSegment"/>s into a single
        /// <c>&lt;path&gt;</c> element with multiple sub-paths, avoiding per-segment tag
        /// and attribute overhead.
        /// </summary>
        private static string MergedPolygonPath(List<PolygonVectorSegment> polys,
            float ox, float oy, float scaleX, float scaleY)
        {
            var d = new StringBuilder();
            foreach (var poly in polys)
            {
                AppendContourPath(d, poly.Points, ox, oy, scaleX, scaleY);
                if (poly.Holes is { Length: > 0 })
                {
                    foreach (var hole in poly.Holes)
                        AppendContourPath(d, hole, ox, oy, scaleX, scaleY);
                }
            }

            var first = polys[0];
            bool isFill = first.FillA > 0f;

            var attrs = new StringBuilder();
            if (isFill)
            {
                attrs.Append($" fill=\"{ColorToHex(first.FillR, first.FillG, first.FillB)}\"");
                if (first.FillA < 1f)
                    attrs.Append($" fill-opacity=\"{Fmt(first.FillA)}\"");
                if (UsePrivateColorSavingMode)
                    attrs.Append($" fill_projectFrameCut.Drawing.Color=\"{PrivateColorValue(first.FillR, first.FillG, first.FillB, first.FillA)}\"");
            }
            else
            {
                attrs.Append(" fill=\"none\"");
            }

            if (first.Thickness > 0f && first.StrokeA > 0f)
            {
                attrs.Append($" stroke=\"{ColorToHex(first.StrokeR, first.StrokeG, first.StrokeB)}\"");
                attrs.Append($" stroke-width=\"{Fmt(first.Thickness)}\"");
                if (first.StrokeA < 1f)
                    attrs.Append($" stroke-opacity=\"{Fmt(first.StrokeA)}\"");
                if (UsePrivateColorSavingMode)
                    attrs.Append($" stroke_projectFrameCut.Drawing.Color=\"{PrivateColorValue(first.StrokeR, first.StrokeG, first.StrokeB, first.StrokeA)}\"");
            }

            return $"<path fill-rule=\"evenodd\" d=\"{d}\"{attrs}/>";
        }

        private static string CommonAttributes(VectorSegment s)
        {
            var sb = new StringBuilder();

            if (s.FillA > 0f)
            {
                sb.Append($" fill=\"{ColorToHex(s.FillR, s.FillG, s.FillB)}\"");
                if (s.FillA < 1f)
                    sb.Append($" fill-opacity=\"{Fmt(s.FillA)}\"");
                if (UsePrivateColorSavingMode)
                    sb.Append($" fill_projectFrameCut.Drawing.Color=\"{PrivateColorValue(s.FillR, s.FillG, s.FillB, s.FillA)}\"");
            }
            else
            {
                sb.Append(" fill=\"none\"");
            }

            if (s.Thickness > 0f && s.StrokeA > 0f)
            {
                sb.Append($" stroke=\"{ColorToHex(s.StrokeR, s.StrokeG, s.StrokeB)}\"");
                sb.Append($" stroke-width=\"{Fmt(s.Thickness)}\"");
                if (s.StrokeA < 1f)
                    sb.Append($" stroke-opacity=\"{Fmt(s.StrokeA)}\"");
                if (UsePrivateColorSavingMode)
                    sb.Append($" stroke_projectFrameCut.Drawing.Color=\"{PrivateColorValue(s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA)}\"");
            }

            return sb.ToString();
        }

        private static float CX(float segX, float ox, float scaleX) => ox + segX * scaleX;
        private static float CY(float segY, float oy, float scaleY) => oy + segY * scaleY;

        private static string LineToSvg(StraightLineVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f)
                return null!;

            var sb = new StringBuilder();
            sb.Append(CI, $"<line x1=\"{Fmt(CX(s.X1, ox, scaleX))}\" y1=\"{Fmt(CY(s.Y1, oy, scaleY))}\"");
            sb.Append(CI, $" x2=\"{Fmt(CX(s.X2, ox, scaleX))}\" y2=\"{Fmt(CY(s.Y2, oy, scaleY))}\"");
            sb.Append(CI, $" stroke=\"{ColorToHex(s.StrokeR, s.StrokeG, s.StrokeB)}\"");
            sb.Append(CI, $" stroke-width=\"{Fmt(s.Thickness)}\"");
            if (s.StrokeA < 1f)
                sb.Append(CI, $" stroke-opacity=\"{Fmt(s.StrokeA)}\"");
            if (UsePrivateColorSavingMode)
                sb.Append(CI, $" stroke_projectFrameCut.Drawing.Color=\"{PrivateColorValue(s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA)}\"");
            sb.Append("/>");
            return sb.ToString();
        }

        private static string RectToSvg(RectangleVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            var x = CX(s.X, ox, scaleX);
            var y = CY(s.Y, oy, scaleY);
            var rw = s.Width * scaleX;
            var rh = s.Height * scaleY;

            return
                $"<rect x=\"{Fmt(x)}\" y=\"{Fmt(y)}\" width=\"{Fmt(rw)}\" height=\"{Fmt(rh)}\"" +
                CommonAttributes(s) +
                "/>";
        }

        private static string RoundedRectToSvg(RoundedRectangleVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            var x = CX(s.X, ox, scaleX);
            var y = CY(s.Y, oy, scaleY);
            var rw = s.Width * scaleX;
            var rh = s.Height * scaleY;
            var radius = s.CornerRadius * Math.Min(scaleX, scaleY);

            return
                $"<rect x=\"{Fmt(x)}\" y=\"{Fmt(y)}\" width=\"{Fmt(rw)}\" height=\"{Fmt(rh)}\"" +
                $" rx=\"{Fmt(radius)}\" ry=\"{Fmt(radius)}\"" +
                CommonAttributes(s) +
                "/>";
        }

        private static string EllipseToSvg(EllipseVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            var cx = CX(s.X, ox, scaleX);
            var cy = CY(s.Y, oy, scaleY);
            var rx = s.RadiusX * scaleX;
            var ry = s.RadiusY * scaleY;

            return
                $"<ellipse cx=\"{Fmt(cx)}\" cy=\"{Fmt(cy)}\" rx=\"{Fmt(rx)}\" ry=\"{Fmt(ry)}\"" +
                CommonAttributes(s) +
                "/>";
        }

        private static string CubicBezierToSvg(CubicBezierVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f)
                return null!;

            var x1 = CX(s.X1, ox, scaleX); var y1 = CY(s.Y1, oy, scaleY);
            var x2 = CX(s.X2, ox, scaleX); var y2 = CY(s.Y2, oy, scaleY);
            var x3 = CX(s.X3, ox, scaleX); var y3 = CY(s.Y3, oy, scaleY);
            var x4 = CX(s.X4, ox, scaleX); var y4 = CY(s.Y4, oy, scaleY);

            return
                $"<path d=\"M {Fmt(x1)},{Fmt(y1)} C {Fmt(x2)},{Fmt(y2)} {Fmt(x3)},{Fmt(y3)} {Fmt(x4)},{Fmt(y4)}\"" +
                StrokeAttributes(s) +
                "/>";
        }

        private static string QuadraticBezierToSvg(QuadraticBezierVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f)
                return null!;

            var x1 = CX(s.X1, ox, scaleX); var y1 = CY(s.Y1, oy, scaleY);
            var x2 = CX(s.X2, ox, scaleX); var y2 = CY(s.Y2, oy, scaleY);
            var x3 = CX(s.X3, ox, scaleX); var y3 = CY(s.Y3, oy, scaleY);

            return
                $"<path d=\"M {Fmt(x1)},{Fmt(y1)} Q {Fmt(x2)},{Fmt(y2)} {Fmt(x3)},{Fmt(y3)}\"" +
                StrokeAttributes(s) +
                "/>";
        }

        private static string ArcToSvg(ArcVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f)
                return null!;

            var cx = CX(s.X, ox, scaleX);
            var cy = CY(s.Y, oy, scaleY);
            var rx = s.RadiusX * scaleX;
            var ry = s.RadiusY * scaleY;

            if (rx <= 0f || ry <= 0f)
                return null!;

            // Convert center-based arc to SVG endpoint arc
            var startAngle = s.StartAngle;
            var endAngle = startAngle + s.SweepAngle;

            var startX = cx + rx * MathF.Cos(startAngle);
            var startY = cy + ry * MathF.Sin(startAngle);
            var endX = cx + rx * MathF.Cos(endAngle);
            var endY = cy + ry * MathF.Sin(endAngle);

            var largeArcFlag = MathF.Abs(s.SweepAngle) > MathF.PI ? 1 : 0;
            var sweepFlag = s.SweepAngle >= 0f ? 1 : 0;

            return
                $"<path d=\"M {Fmt(startX)},{Fmt(startY)} A {Fmt(rx)},{Fmt(ry)} 0 {largeArcFlag},{sweepFlag} {Fmt(endX)},{Fmt(endY)}\"" +
                StrokeAttributes(s) +
                "/>";
        }

        private static string PolygonToSvg(PolygonVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Points.Length < 3)
                return null!;

            bool hasHoles = s.Holes is { Length: > 0 };

            if (hasHoles)
            {
                // Use <path> with evenodd fill-rule so holes punch through.
                var d = new StringBuilder();
                AppendContourPath(d, s.Points, ox, oy, scaleX, scaleY);
                foreach (var hole in s.Holes!)
                    AppendContourPath(d, hole, ox, oy, scaleX, scaleY);

                return
                    $"<path fill-rule=\"evenodd\" d=\"{d}\"" +
                    CommonAttributes(s) +
                    "/>";
            }

            var pts = new StringBuilder();
            foreach (var p in s.Points)
                pts.Append(CI, $"{Fmt(CX(p.X, ox, scaleX))},{Fmt(CY(p.Y, oy, scaleY))} ");

            return
                $"<polygon points=\"{pts.ToString().TrimEnd()}\"" +
                CommonAttributes(s) +
                "/>";
        }

        private static void AppendContourPath(StringBuilder d, Point[] contour, float ox, float oy, float scaleX, float scaleY)
        {
            d.Append('M');
            d.Append(CI, $"{Fmt(CX(contour[0].X, ox, scaleX))},{Fmt(CY(contour[0].Y, oy, scaleY))}");
            for (int i = 1; i < contour.Length; i++)
            {
                d.Append(" L");
                d.Append(CI, $"{Fmt(CX(contour[i].X, ox, scaleX))},{Fmt(CY(contour[i].Y, oy, scaleY))}");
            }
            d.Append(" Z ");
        }

        private static string PolylineToSvg(PolylineVectorSegment s, float ox, float oy, float scaleX, float scaleY)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f || s.Points.Length < 2)
                return null!;

            var pts = new StringBuilder();
            foreach (var p in s.Points)
            {
                pts.Append(CI, $"{Fmt(CX(p.X, ox, scaleX))},{Fmt(CY(p.Y, oy, scaleY))} ");
            }

            return
                $"<polyline points=\"{pts.ToString().TrimEnd()}\"" +
                StrokeAttributes(s) +
                "/>";
        }

        private static string StrokeAttributes(VectorSegment s)
        {
            var sb = new StringBuilder();
            sb.Append(CI, $" fill=\"none\" stroke=\"{ColorToHex(s.StrokeR, s.StrokeG, s.StrokeB)}\"");
            sb.Append(CI, $" stroke-width=\"{Fmt(s.Thickness)}\"");
            if (s.StrokeA < 1f)
                sb.Append(CI, $" stroke-opacity=\"{Fmt(s.StrokeA)}\"");
            if (UsePrivateColorSavingMode)
                sb.Append(CI, $" stroke_projectFrameCut.Drawing.Color=\"{PrivateColorValue(s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA)}\"");
            sb.Append("/>");
            return sb.ToString();
        }

        public static VectorPicture ImportFromFile(string filename) => ImportFromSvg(File.ReadAllText(filename));

        /// <summary>
        /// Parse an SVG string into a <see cref="VectorCanvas"/>.
        /// </summary>
        public static VectorPicture ImportFromSvg(string svgContent)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(svgContent);

            var doc = XDocument.Parse(svgContent);
            var svg = doc.Root;
            if (svg == null || svg.Name.LocalName != "svg")
                throw new InvalidDataException("Root element is not <svg>.");

            // Determine canvas dimensions from viewBox or width/height
            TryGetDimension(svg, "width", out var svgW, 100);
            TryGetDimension(svg, "height", out var svgH, 100);

            var vb = svg.Attribute("viewBox")?.Value;
            float canvasW, canvasH;
            if (vb != null)
            {
                var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    float.TryParse(parts[2], NumberStyles.Float, CI, out canvasW) &&
                    float.TryParse(parts[3], NumberStyles.Float, CI, out canvasH) &&
                    canvasW > 0 && canvasH > 0)
                {
                    // Use viewBox dimensions
                }
                else
                {
                    canvasW = svgW; canvasH = svgH;
                }
            }
            else
            {
                canvasW = svgW; canvasH = svgH;
            }

            var result = new VectorPicture();
            var ctx = new SvgContext();

            ParseContainer(svg, result, ctx, canvasW, canvasH);

            return result;
        }

        /// <summary>
        /// Tracks inherited SVG presentation attributes during import.
        /// </summary>
        private sealed class SvgContext
        {
            public string? Fill { get; set; }
            public string? Stroke { get; set; }
            public float StrokeWidth { get; set; } = 1f;
            public float Opacity { get; set; } = 1f;
            public float FillOpacity { get; set; } = 1f;
            public float StrokeOpacity { get; set; } = 1f;
            public float TranslateX { get; set; }
            public float TranslateY { get; set; }
            public string? PrivateFillColor { get; set; }
            public string? PrivateStrokeColor { get; set; }
            public string? FontFamily { get; set; }
            public float FontSize { get; set; } = 16f;
            public string? FontWeight { get; set; }
            public string? FontStyle { get; set; }
            public string? TextAnchor { get; set; }

            public SvgContext Clone()
            {
                var c = new SvgContext
                {
                    Fill = Fill,
                    Stroke = Stroke,
                    StrokeWidth = StrokeWidth,
                    Opacity = Opacity,
                    FillOpacity = FillOpacity,
                    StrokeOpacity = StrokeOpacity,
                    TranslateX = TranslateX,
                    TranslateY = TranslateY,
                    PrivateFillColor = PrivateFillColor,
                    PrivateStrokeColor = PrivateStrokeColor,
                    FontFamily = FontFamily,
                    FontSize = FontSize,
                    FontWeight = FontWeight,
                    FontStyle = FontStyle,
                    TextAnchor = TextAnchor,
                };
                return c;
            }
        }

        private static void ParseContainer(XElement parent, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            foreach (var el in parent.Elements())
            {
                var local = el.Name.LocalName;
                var childCtx = ctx.Clone();

                // Inherit / override presentation attributes
                ApplyPresentationAttributes(el, childCtx);

                // Handle translate on groups
                ApplyTransform(el, childCtx);

                switch (local)
                {
                    case "g":
                        ParseContainer(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "rect":
                        ParseRect(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "circle":
                        ParseCircle(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "ellipse":
                        ParseEllipse(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "line":
                        ParseLine(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "polyline":
                        ParsePolyline(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "polygon":
                        ParsePolygon(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "text":
                        ParseText(el, result, childCtx, canvasW, canvasH);
                        break;
                    case "path":
                        ParsePath(el, result, childCtx, canvasW, canvasH);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------
        // Presentation attributes
        // ---------------------------------------------------------------

        private static void ApplyPresentationAttributes(XElement el, SvgContext ctx)
        {
            // Style attribute (takes precedence over direct attributes)
            var style = el.Attribute("style")?.Value;
            if (style != null)
            {
                foreach (var part in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = part.IndexOf(':');
                    if (eq < 0) continue;
                    var name = part[..eq].Trim().ToLowerInvariant();
                    var value = part[(eq + 1)..].Trim();
                    ApplyStyle(name, value, ctx);
                }
            }

            // Direct attributes (lower priority than style, which is already applied)
            ApplyAttribute(el, "fill", v => ctx.Fill = v);
            ApplyAttribute(el, "stroke", v => ctx.Stroke = v);
            ApplyAttribute(el, "stroke-width", v => { if (TryParseFloat(v, out var sw)) ctx.StrokeWidth = sw; });
            ApplyAttribute(el, "opacity", v => { if (TryParseFloat(v, out var o)) ctx.Opacity = o; });
            ApplyAttribute(el, "fill-opacity", v => { if (TryParseFloat(v, out var fo)) ctx.FillOpacity = fo; });
            ApplyAttribute(el, "stroke-opacity", v => { if (TryParseFloat(v, out var so)) ctx.StrokeOpacity = so; });

            // Font / text attributes
            ApplyAttribute(el, "font-family", v => ctx.FontFamily = v);
            ApplyAttribute(el, "font-size", v => { if (TryParseFontSize(v, out var fs)) ctx.FontSize = fs; });
            ApplyAttribute(el, "font-weight", v => ctx.FontWeight = v);
            ApplyAttribute(el, "font-style", v => ctx.FontStyle = v);
            ApplyAttribute(el, "text-anchor", v => ctx.TextAnchor = v);

            // Private color attributes (take complete priority if present)
            ApplyAttribute(el, "fill_projectFrameCut.Drawing.Color", v => ctx.PrivateFillColor = v);
            ApplyAttribute(el, "stroke_projectFrameCut.Drawing.Color", v => ctx.PrivateStrokeColor = v);
        }

        private static void ApplyStyle(string name, string value, SvgContext ctx)
        {
            switch (name)
            {
                case "fill": ctx.Fill = value; break;
                case "stroke": ctx.Stroke = value; break;
                case "stroke-width": if (TryParseFloat(value, out var sw)) ctx.StrokeWidth = sw; break;
                case "opacity": if (TryParseFloat(value, out var o)) ctx.Opacity = o; break;
                case "fill-opacity": if (TryParseFloat(value, out var fo)) ctx.FillOpacity = fo; break;
                case "stroke-opacity": if (TryParseFloat(value, out var so)) ctx.StrokeOpacity = so; break;
                case "font-family": ctx.FontFamily = value; break;
                case "font-size": if (TryParseFontSize(value, out var fs)) ctx.FontSize = fs; break;
                case "font-weight": ctx.FontWeight = value; break;
                case "font-style": ctx.FontStyle = value; break;
                case "text-anchor": ctx.TextAnchor = value; break;
            }
        }

        private static void ApplyAttribute(XElement el, string name, Action<string> setter)
        {
            var v = el.Attribute(name)?.Value;
            if (v != null) setter(v);
        }

        private static void ApplyTransform(XElement el, SvgContext ctx)
        {
            var tf = el.Attribute("transform")?.Value;
            if (tf == null) return;

            // Handle translate(tx, ty) and translate(tx)
            if (tf.StartsWith("translate(", StringComparison.OrdinalIgnoreCase))
            {
                var inner = tf["translate(".Length..^1];
                var parts = inner.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    TryParseFloat(parts[0], out var tx);
                    ctx.TranslateX += tx;
                    if (parts.Length >= 2 && TryParseFloat(parts[1], out var ty))
                        ctx.TranslateY += ty;
                }
            }
        }

        // ---------------------------------------------------------------
        // SVG element parsers
        // ---------------------------------------------------------------

        private static void ParseRect(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var x = GetDim(el, "x", canvasW);
            var y = GetDim(el, "y", canvasH);
            var w = GetDim(el, "width", canvasW);
            var h = GetDim(el, "height", canvasH);

            var rx = GetDim(el, "rx", Math.Min(canvasW, canvasH));
            var ry = GetDim(el, "ry", Math.Min(canvasW, canvasH));

            // SVG: if only one of rx/ry is specified, the other defaults to the same value
            if (rx > 0f && ry <= 0f) ry = rx;
            if (ry > 0f && rx <= 0f) rx = ry;

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            var seg = rx > 0f || ry > 0f
                ? new RoundedRectangleVectorSegment
                {
                    X = (x + ctx.TranslateX) / canvasW,
                    Y = (y + ctx.TranslateY) / canvasH,
                    Width = w / canvasW,
                    Height = h / canvasH,
                    CornerRadius = (rx > 0f ? rx : ry) / Math.Min(canvasW, canvasH),
                    FillR = fillR,
                    FillG = fillG,
                    FillB = fillB,
                    FillA = fillA,
                    StrokeR = strokeR,
                    StrokeG = strokeG,
                    StrokeB = strokeB,
                    StrokeA = strokeA,
                    Thickness = thickness,
                }
                : new RectangleVectorSegment
                {
                    X = (x + ctx.TranslateX) / canvasW,
                    Y = (y + ctx.TranslateY) / canvasH,
                    Width = w / canvasW,
                    Height = h / canvasH,
                    FillR = fillR,
                    FillG = fillG,
                    FillB = fillB,
                    FillA = fillA,
                    StrokeR = strokeR,
                    StrokeG = strokeG,
                    StrokeB = strokeB,
                    StrokeA = strokeA,
                    Thickness = thickness,
                };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParseCircle(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var cx = GetDim(el, "cx", canvasW);
            var cy = GetDim(el, "cy", canvasH);
            var r = GetDim(el, "r", Math.Min(canvasW, canvasH));

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            var seg = new EllipseVectorSegment
            {
                X = (cx + ctx.TranslateX) / canvasW,
                Y = (cy + ctx.TranslateY) / canvasH,
                RadiusX = r / canvasW,
                RadiusY = r / canvasH,
                FillR = fillR,
                FillG = fillG,
                FillB = fillB,
                FillA = fillA,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                Thickness = thickness,
            };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParseEllipse(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var cx = GetDim(el, "cx", canvasW);
            var cy = GetDim(el, "cy", canvasH);
            var rx = GetDim(el, "rx", canvasW);
            var ry = GetDim(el, "ry", canvasH);

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            var seg = new EllipseVectorSegment
            {
                X = (cx + ctx.TranslateX) / canvasW,
                Y = (cy + ctx.TranslateY) / canvasH,
                RadiusX = rx / canvasW,
                RadiusY = ry / canvasH,
                FillR = fillR,
                FillG = fillG,
                FillB = fillB,
                FillA = fillA,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                Thickness = thickness,
            };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParseLine(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var x1 = GetDim(el, "x1", canvasW);
            var y1 = GetDim(el, "y1", canvasH);
            var x2 = GetDim(el, "x2", canvasW);
            var y2 = GetDim(el, "y2", canvasH);

            NormalizeStrokeOnly(ctx, out var strokeR, out var strokeG, out var strokeB,
                                out var strokeA, out var thickness);

            if (thickness <= 0f || strokeA <= 0f) return;

            var seg = new StraightLineVectorSegment
            {
                X1 = (x1 + ctx.TranslateX) / canvasW,
                Y1 = (y1 + ctx.TranslateY) / canvasH,
                X2 = (x2 + ctx.TranslateX) / canvasW,
                Y2 = (y2 + ctx.TranslateY) / canvasH,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                Thickness = thickness,
            };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParsePolyline(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var pts = ParsePoints(el.Attribute("points")?.Value);
            if (pts.Count < 2) return;

            NormalizeStrokeOnly(ctx, out var strokeR, out var strokeG, out var strokeB,
                                out var strokeA, out var thickness);

            if (thickness <= 0f || strokeA <= 0f) return;

            var ptArray = pts.Select(p => new Point(
                (p.x + ctx.TranslateX) / canvasW,
                (p.y + ctx.TranslateY) / canvasH
            )).ToArray();

            var seg = new PolylineVectorSegment
            {
                Points = ptArray,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                Thickness = thickness,
            };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParsePolygon(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var pts = ParsePoints(el.Attribute("points")?.Value);
            if (pts.Count < 3) return;

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            var ptArray = pts.Select(p => new Point(
                (p.x + ctx.TranslateX) / canvasW,
                (p.y + ctx.TranslateY) / canvasH
            )).ToArray();

            var seg = new PolygonVectorSegment
            {
                Points = ptArray,
                FillR = fillR,
                FillG = fillG,
                FillB = fillB,
                FillA = fillA,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                Thickness = thickness,
            };

            result.Elements.Add(new SegmentCollectionElement(seg));
        }

        private static void ParseText(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            if (TextImportHandler == null) return;

            var x = GetDim(el, "x", canvasW);
            var y = GetDim(el, "y", canvasH);

            var text = el.Value?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            var context = new SvgTextImportContext
            {
                Text = text,
                X = x + ctx.TranslateX,
                Y = y + ctx.TranslateY,
                CanvasWidth = canvasW,
                CanvasHeight = canvasH,
                FontFamily = ctx.FontFamily,
                FontSize = ctx.FontSize,
                FontWeight = ctx.FontWeight,
                FontStyle = ctx.FontStyle,
                TextAnchor = ctx.TextAnchor,
                FillR = fillR,
                FillG = fillG,
                FillB = fillB,
                FillA = fillA,
                StrokeR = strokeR,
                StrokeG = strokeG,
                StrokeB = strokeB,
                StrokeA = strokeA,
                StrokeWidth = thickness,
            };

            var element = TextImportHandler(context);
            if (element != null)
            {
                if (element.UseUniformScale)
                {
                    // For uniform-scale elements, position is:
                    //   ox = BaseX * w + RelativeX * min(w,h)
                    // Store the SVG position in BaseX/Y (canvas-space) and
                    // keep the handler-set RelativeX/Y (cursor advances) intact.
                    element.BaseX = (x + ctx.TranslateX) / canvasW;
                    element.BaseY = (y + ctx.TranslateY) / canvasH;
                }
                else
                {
                    element.RelativeX = (x + ctx.TranslateX) / canvasW;
                    element.RelativeY = (y + ctx.TranslateY) / canvasH;
                }
                result.Elements.Add(element);
            }
        }

        /// <summary>
        /// Parse an SVG font-size value into a float in user units.
        /// Handles bare numbers, px, and pt suffixes. Returns <c>false</c> for
        /// unrecognized values (caller should fall back to the inherited size).
        /// </summary>
        private static bool TryParseFontSize(string raw, out float size)
        {
            size = 16f;
            if (raw == null) return false;
            var v = raw.Trim();
            if (v.Length == 0) return false;

            if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();
            else if (v.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();

            return TryParseFloat(v, out size);
        }

        // ---------------------------------------------------------------
        // Path parser
        // ---------------------------------------------------------------

        private static void ParsePath(XElement el, VectorPicture result, SvgContext ctx,
            float canvasW, float canvasH)
        {
            var d = el.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(d)) return;

            NormalizeFillStroke(ctx, out var fillR, out var fillG, out var fillB, out var fillA,
                               out var strokeR, out var strokeG, out var strokeB, out var strokeA,
                               out var thickness);

            float curX = 0, curY = 0;
            float startX = 0, startY = 0;
            bool hasMove = false;

            var tokens = TokenizePath(d);
            var idx = 0;

            while (idx < tokens.Length)
            {
                var cmd = tokens[idx++][0];
                var relative = char.IsLower(cmd);
                var abs = char.ToUpperInvariant(cmd);

                switch (abs)
                {
                    case 'M': // move to
                        {
                            var (x, y) = ReadCoordPair(tokens, ref idx);
                            if (relative) { x += curX; y += curY; }
                            hasMove = true;
                            startX = curX = x; startY = curY = y;

                            // Implicit line-to if more coordinates follow
                            while (idx < tokens.Length && IsNumber(tokens[idx]))
                            {
                                var (lx, ly) = ReadCoordPair(tokens, ref idx);
                                if (relative) { lx += curX; ly += curY; }
                                var seg = new StraightLineVectorSegment
                                {
                                    X1 = (curX + ctx.TranslateX) / canvasW,
                                    Y1 = (curY + ctx.TranslateY) / canvasH,
                                    X2 = (lx + ctx.TranslateX) / canvasW,
                                    Y2 = (ly + ctx.TranslateY) / canvasH,
                                    StrokeR = strokeR,
                                    StrokeG = strokeG,
                                    StrokeB = strokeB,
                                    StrokeA = strokeA,
                                    Thickness = thickness,
                                };
                                result.Elements.Add(new SegmentCollectionElement(seg));
                                curX = lx; curY = ly;
                            }
                            break;
                        }

                    case 'L': // line to
                        {
                            while (idx < tokens.Length && IsNumber(tokens[idx]))
                            {
                                var (x, y) = ReadCoordPair(tokens, ref idx);
                                if (relative) { x += curX; y += curY; }
                                var seg = new StraightLineVectorSegment
                                {
                                    X1 = (curX + ctx.TranslateX) / canvasW,
                                    Y1 = (curY + ctx.TranslateY) / canvasH,
                                    X2 = (x + ctx.TranslateX) / canvasW,
                                    Y2 = (y + ctx.TranslateY) / canvasH,
                                    StrokeR = strokeR,
                                    StrokeG = strokeG,
                                    StrokeB = strokeB,
                                    StrokeA = strokeA,
                                    Thickness = thickness,
                                };
                                result.Elements.Add(new SegmentCollectionElement(seg));
                                curX = x; curY = y;
                            }
                            break;
                        }

                    case 'C': // cubic bezier
                        {
                            while (idx < tokens.Length && IsNumber(tokens[idx]))
                            {
                                var (x1, y1) = ReadCoordPair(tokens, ref idx);
                                var (x2, y2) = ReadCoordPair(tokens, ref idx);
                                var (x, y) = ReadCoordPair(tokens, ref idx);
                                if (relative) { x1 += curX; y1 += curY; x2 += curX; y2 += curY; x += curX; y += curY; }
                                var seg = new CubicBezierVectorSegment
                                {
                                    X1 = (curX + ctx.TranslateX) / canvasW,
                                    Y1 = (curY + ctx.TranslateY) / canvasH,
                                    X2 = (x1 + ctx.TranslateX) / canvasW,
                                    Y2 = (y1 + ctx.TranslateY) / canvasH,
                                    X3 = (x2 + ctx.TranslateX) / canvasW,
                                    Y3 = (y2 + ctx.TranslateY) / canvasH,
                                    X4 = (x + ctx.TranslateX) / canvasW,
                                    Y4 = (y + ctx.TranslateY) / canvasH,
                                    StrokeR = strokeR,
                                    StrokeG = strokeG,
                                    StrokeB = strokeB,
                                    StrokeA = strokeA,
                                    Thickness = thickness,
                                };
                                result.Elements.Add(new SegmentCollectionElement(seg));
                                curX = x; curY = y;
                            }
                            break;
                        }

                    case 'Q': // quadratic bezier
                        {
                            while (idx < tokens.Length && IsNumber(tokens[idx]))
                            {
                                var (x1, y1) = ReadCoordPair(tokens, ref idx);
                                var (x, y) = ReadCoordPair(tokens, ref idx);
                                if (relative) { x1 += curX; y1 += curY; x += curX; y += curY; }
                                var seg = new QuadraticBezierVectorSegment
                                {
                                    X1 = (curX + ctx.TranslateX) / canvasW,
                                    Y1 = (curY + ctx.TranslateY) / canvasH,
                                    X2 = (x1 + ctx.TranslateX) / canvasW,
                                    Y2 = (y1 + ctx.TranslateY) / canvasH,
                                    X3 = (x + ctx.TranslateX) / canvasW,
                                    Y3 = (y + ctx.TranslateY) / canvasH,
                                    StrokeR = strokeR,
                                    StrokeG = strokeG,
                                    StrokeB = strokeB,
                                    StrokeA = strokeA,
                                    Thickness = thickness,
                                };
                                result.Elements.Add(new SegmentCollectionElement(seg));
                                curX = x; curY = y;
                            }
                            break;
                        }

                    case 'A': // arc
                        {
                            while (idx < tokens.Length && IsNumber(tokens[idx]))
                            {
                                var rx = ReadFloat(tokens, ref idx);
                                var ry = ReadFloat(tokens, ref idx);
                                var rot = ReadFloat(tokens, ref idx);
                                var largeArc = ReadFloat(tokens, ref idx);
                                var sweep = ReadFloat(tokens, ref idx);
                                var (x, y) = ReadCoordPair(tokens, ref idx);
                                if (relative) { x += curX; y += curY; }

                                // Convert SVG endpoint arc to center-based ArcVectorSegment
                                if (ArcEndpointToCenter(curX, curY, x, y, rx, ry, rot, largeArc != 0, sweep != 0,
                                                        out var cx, out var cy, out var startAngle, out var sweepAngle))
                                {
                                    var seg = new ArcVectorSegment
                                    {
                                        X = (cx + ctx.TranslateX) / canvasW,
                                        Y = (cy + ctx.TranslateY) / canvasH,
                                        RadiusX = rx / canvasW,
                                        RadiusY = ry / canvasH,
                                        StartAngle = startAngle,
                                        SweepAngle = sweepAngle,
                                        StrokeR = strokeR,
                                        StrokeG = strokeG,
                                        StrokeB = strokeB,
                                        StrokeA = strokeA,
                                        Thickness = thickness,
                                    };
                                    result.Elements.Add(new SegmentCollectionElement(seg));
                                }

                                curX = x; curY = y;
                            }
                            break;
                        }

                    case 'Z': // close path
                        {
                            if (hasMove && (curX != startX || curY != startY))
                            {
                                var seg = new StraightLineVectorSegment
                                {
                                    X1 = (curX + ctx.TranslateX) / canvasW,
                                    Y1 = (curY + ctx.TranslateY) / canvasH,
                                    X2 = (startX + ctx.TranslateX) / canvasW,
                                    Y2 = (startY + ctx.TranslateY) / canvasH,
                                    StrokeR = strokeR,
                                    StrokeG = strokeG,
                                    StrokeB = strokeB,
                                    StrokeA = strokeA,
                                    Thickness = thickness,
                                };
                                result.Elements.Add(new SegmentCollectionElement(seg));
                            }
                            curX = startX; curY = startY;
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Convert SVG endpoint-based arc parameters to center-based representation.
        /// Based on the SVG specification: https://www.w3.org/TR/SVG/implnote.html#ArcImplementationNotes
        /// </summary>
        private static bool ArcEndpointToCenter(
            float x1, float y1, float x2, float y2,
            float rx, float ry, float phi,
            bool largeArc, bool sweep,
            out float cx, out float cy,
            out float startAngle, out float sweepAngle)
        {
            cx = cy = startAngle = sweepAngle = 0;

            // Step 1: treat zero-radius as straight line
            if (rx < 0.0001f || ry < 0.0001f)
                return false;

            // Ensure radii are non-negative
            rx = MathF.Abs(rx);
            ry = MathF.Abs(ry);

            // Step 2: compute (x1', y1') from eq. 5.1
            var cosPhi = MathF.Cos(phi);
            var sinPhi = MathF.Sin(phi);
            var dx = (x1 - x2) * 0.5f;
            var dy = (y1 - y2) * 0.5f;
            var x1p = cosPhi * dx + sinPhi * dy;
            var y1p = -sinPhi * dx + cosPhi * dy;

            // Step 3: ensure radii are large enough (eq. 6.6)
            var lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
            if (lambda > 1f)
            {
                var sqrtLambda = MathF.Sqrt(lambda);
                rx *= sqrtLambda;
                ry *= sqrtLambda;
            }

            // Step 4: compute center (cx', cy') in the transformed system (eq. 5.2)
            var rx2 = rx * rx;
            var ry2 = ry * ry;
            var x1p2 = x1p * x1p;
            var y1p2 = y1p * y1p;

            var radicand = (rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2) / (rx2 * y1p2 + ry2 * x1p2);
            if (radicand < 0f) radicand = 0f; // handle numerical error
            var coeff = MathF.Sqrt(radicand) * (largeArc == sweep ? -1f : 1f);

            var cxp = coeff * (rx * y1p) / ry;
            var cyp = coeff * (-ry * x1p) / rx;

            // Step 5: rotate back to original space (eq. 5.3)
            cx = cosPhi * cxp - sinPhi * cyp + (x1 + x2) * 0.5f;
            cy = sinPhi * cxp + cosPhi * cyp + (y1 + y2) * 0.5f;

            // Step 6: compute start angle and sweep angle (eq. 5.4, 5.5, 5.6)
            var ux = (x1p - cxp) / rx;
            var uy = (y1p - cyp) / ry;
            var vx = (-x1p - cxp) / rx;
            var vy = (-y1p - cyp) / ry;

            startAngle = MathF.Atan2(uy, ux);

            var dot = ux * vx + uy * vy;
            var cross = ux * vy - uy * vx;
            var angleDelta = MathF.Atan2(cross, dot);

            // Adjust sweep
            if (sweep && angleDelta < 0f)
                angleDelta += 2f * MathF.PI;
            else if (!sweep && angleDelta > 0f)
                angleDelta -= 2f * MathF.PI;

            sweepAngle = angleDelta;
            return true;
        }

        // ---------------------------------------------------------------
        // SVG path tokenizer
        // ---------------------------------------------------------------

        private static string[] TokenizePath(string d)
        {
            var list = new List<string>();
            var i = 0;
            while (i < d.Length)
            {
                if (char.IsWhiteSpace(d[i]) || d[i] == ',')
                {
                    i++;
                    continue;
                }

                if (char.IsLetter(d[i]))
                {
                    list.Add(d[i].ToString());
                    i++;
                    continue;
                }

                // Number
                var start = i;
                if (d[i] == '-' || d[i] == '+')
                    i++;
                while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.'))
                    i++;
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
                {
                    i++;
                    if (i < d.Length && (d[i] == '-' || d[i] == '+'))
                        i++;
                    while (i < d.Length && char.IsDigit(d[i]))
                        i++;
                }
                list.Add(d[start..i]);
            }

            return [.. list];
        }

        private static bool IsNumber(string token) =>
            token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '-' || token[0] == '+' || token[0] == '.');

        private static float ReadFloat(string[] tokens, ref int idx)
        {
            if (idx < tokens.Length && IsNumber(tokens[idx]))
            {
                var val = float.Parse(tokens[idx], NumberStyles.Float, CI);
                idx++;
                return val;
            }
            return 0f;
        }

        private static (float x, float y) ReadCoordPair(string[] tokens, ref int idx)
        {
            var x = ReadFloat(tokens, ref idx);
            var y = ReadFloat(tokens, ref idx);
            return (x, y);
        }

        // ---------------------------------------------------------------
        // SVG points attribute parser
        // ---------------------------------------------------------------

        private static List<(float x, float y)> ParsePoints(string? points)
        {
            var result = new List<(float, float)>();
            if (string.IsNullOrWhiteSpace(points)) return result;

            var tokens = points.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < tokens.Length; i += 2)
            {
                if (float.TryParse(tokens[i], NumberStyles.Float, CI, out var x) &&
                    float.TryParse(tokens[i + 1], NumberStyles.Float, CI, out var y))
                {
                    result.Add((x, y));
                }
            }

            return result;
        }

        // ---------------------------------------------------------------
        // Color / attribute parsing
        // ---------------------------------------------------------------

        private static void NormalizeFillStroke(SvgContext ctx,
            out ushort fillR, out ushort fillG, out ushort fillB, out float fillA,
            out ushort strokeR, out ushort strokeG, out ushort strokeB, out float strokeA,
            out float thickness)
        {
            // Private fill color takes priority
            if (ctx.PrivateFillColor != null)
            {
                var parsed = ParsePrivateColorValue(ctx.PrivateFillColor);
                if (parsed.HasValue)
                {
                    fillR = parsed.Value.r; fillG = parsed.Value.g; fillB = parsed.Value.b;
                    fillA = parsed.Value.a;
                    NormalizeStrokeOnly(ctx, out strokeR, out strokeG, out strokeB, out strokeA, out thickness);
                    return;
                }
            }

            // Standard fill fallback
            var fillColor = ParseColor(ctx.Fill, false);
            if (fillColor.HasValue)
            {
                fillR = fillColor.Value.r; fillG = fillColor.Value.g; fillB = fillColor.Value.b;
                fillA = ctx.Opacity * ctx.FillOpacity;
            }
            else
            {
                fillR = fillG = fillB = 0; fillA = 0f;
            }

            NormalizeStrokeOnly(ctx, out strokeR, out strokeG, out strokeB, out strokeA, out thickness);
        }

        private static void NormalizeStrokeOnly(SvgContext ctx,
            out ushort strokeR, out ushort strokeG, out ushort strokeB, out float strokeA,
            out float thickness)
        {
            if (ctx.PrivateStrokeColor != null)
            {
                var parsed = ParsePrivateColorValue(ctx.PrivateStrokeColor);
                if (parsed.HasValue)
                {
                    strokeR = parsed.Value.r; strokeG = parsed.Value.g; strokeB = parsed.Value.b;
                    strokeA = parsed.Value.a;
                    thickness = ctx.StrokeWidth;
                    return;
                }
            }

            var strokeColor = ParseColor(ctx.Stroke, true);
            if (strokeColor.HasValue && ctx.StrokeWidth > 0f)
            {
                strokeR = strokeColor.Value.r; strokeG = strokeColor.Value.g; strokeB = strokeColor.Value.b;
                strokeA = ctx.Opacity * ctx.StrokeOpacity;
                thickness = ctx.StrokeWidth;
            }
            else
            {
                strokeR = strokeG = strokeB = 0; strokeA = 0f; thickness = 0f;
            }
        }

        private static (ushort r, ushort g, ushort b)? ParseColor(string? color, bool isStroke)
        {
            if (string.IsNullOrWhiteSpace(color) || color == "none")
                return null;

            color = color.Trim();

            // #RRGGBB
            if (color[0] == '#' && color.Length == 7)
            {
                return (
                    (ushort)(Convert.ToByte(color[1..3], 16) * 257),
                    (ushort)(Convert.ToByte(color[3..5], 16) * 257),
                    (ushort)(Convert.ToByte(color[5..7], 16) * 257)
                );
            }

            // #RGB
            if (color[0] == '#' && color.Length == 4)
            {
                var r = Convert.ToByte(color[1..2], 16);
                var g = Convert.ToByte(color[2..3], 16);
                var b = Convert.ToByte(color[3..4], 16);
                return (
                    (ushort)((r * 16 + r) * 257 / 15),
                    (ushort)((g * 16 + g) * 257 / 15),
                    (ushort)((b * 16 + b) * 257 / 15)
                );
            }

            // Named colors — basic set
            return NamedColor(color) ?? null;
        }

        private static (ushort r, ushort g, ushort b)? NamedColor(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "black" => (0, 0, 0),
                "white" => (ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
                "red" => (ushort.MaxValue, 0, 0),
                "green" => (0, ushort.MaxValue / 2, 0),
                "blue" => (0, 0, ushort.MaxValue),
                "yellow" => (ushort.MaxValue, ushort.MaxValue, 0),
                "cyan" or "aqua" => (0, ushort.MaxValue, ushort.MaxValue),
                "magenta" or "fuchsia" => (ushort.MaxValue, 0, ushort.MaxValue),
                "silver" => (ushort.MaxValue * 3 / 4, ushort.MaxValue * 3 / 4, ushort.MaxValue * 3 / 4),
                "gray" or "grey" => (ushort.MaxValue / 2, ushort.MaxValue / 2, ushort.MaxValue / 2),
                "maroon" => (ushort.MaxValue / 2, 0, 0),
                "olive" => (ushort.MaxValue / 2, ushort.MaxValue / 2, 0),
                "purple" => (ushort.MaxValue / 2, 0, ushort.MaxValue / 2),
                "teal" => (0, ushort.MaxValue / 2, ushort.MaxValue / 2),
                "navy" => (0, 0, ushort.MaxValue / 2),
                "orange" => (ushort.MaxValue, ushort.MaxValue * 2 / 3, 0),
                "transparent" => (0, 0, 0),
                _ => null,
            };
        }

        /// <summary>
        /// Try to read an SVG dimension attribute. Supports px, pt, etc. — strips units, falls back to default.
        /// </summary>
        private static float GetDim(XElement el, string attr, float canvasDim)
        {
            var v = el.Attribute(attr)?.Value;
            if (v == null) return 0f;

            // Strip common SVG units
            if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();
            else if (v.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();
            else if (v.EndsWith("%", StringComparison.OrdinalIgnoreCase))
            {
                v = v[..^1].Trim();
                if (float.TryParse(v, NumberStyles.Float, CI, out var pct))
                    return pct * 0.01f * canvasDim;
                return 0f;
            }

            return float.TryParse(v, NumberStyles.Float, CI, out var val) ? val : 0f;
        }

        private static bool TryGetDimension(XElement svg, string attr, out float value, float fallback)
        {
            value = fallback;
            var v = svg.Attribute(attr)?.Value;
            if (v == null) return false;

            if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();
            else if (v.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
                v = v[..^2].Trim();

            return float.TryParse(v, NumberStyles.Float, CI, out value);
        }

        private static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CI, out value);
        }

        // =====================================================================
        // Formatting helpers
        // =====================================================================

        private static string Fmt(float v) => v.ToString("0.###", CI);

        private static string ColorToHex(ushort r, ushort g, ushort b)
        {
            return $"#{r >> 8:X2}{g >> 8:X2}{b >> 8:X2}";
        }

        // =====================================================================
        // Private color format helpers (UsePrivateColorSavingMode)
        // Format: "#RRRRGGGGBBBB<Base64Alpha>" where RRRR/GGGG/BBBB are 4-hex-digit
        // ushort values and Base64Alpha is the 4 bytes of the float alpha encoded
        // as Base64.
        // =====================================================================

        private static string PrivateColorValue(ushort r, ushort g, ushort b, float a)
        {
            return $"#{r:X4}{g:X4}{b:X4}{Convert.ToBase64String(BitConverter.GetBytes(a))}";
        }

        private static (ushort r, ushort g, ushort b, float a)? ParsePrivateColorValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '#' || value.Length < 13)
                return null;

            if (!ushort.TryParse(value.AsSpan(1, 4), NumberStyles.HexNumber, CI, out var r) ||
                !ushort.TryParse(value.AsSpan(5, 4), NumberStyles.HexNumber, CI, out var g) ||
                !ushort.TryParse(value.AsSpan(9, 4), NumberStyles.HexNumber, CI, out var b))
                return null;

            try
            {
                var alphaBytes = Convert.FromBase64String(value[13..]);
                if (alphaBytes.Length != 4)
                    return null;
                return (r, g, b, BitConverter.ToSingle(alphaBytes, 0));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
