using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Vector.ImportExport
{
    public class VectorToIPicture
    {
        private readonly ushort[] _r;
        private readonly ushort[] _g;
        private readonly ushort[] _b;
        private readonly float[] _a;
        private readonly int _width;
        private readonly int _height;
        private readonly int _pixels;

        private VectorToIPicture(int width, int height)
        {
            _width = width;
            _height = height;
            _pixels = width * height;
            _r = new ushort[_pixels];
            _g = new ushort[_pixels];
            _b = new ushort[_pixels];
            _a = new float[_pixels];
            Array.Fill(_a, 1f);
        }

        public static IPicture Convert(VectorPicture canvas, int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException($"Canvas size must be positive. Got {width}x{height}.");

            var converter = new VectorToIPicture(width, height);

            // White background
            Array.Fill(converter._r, ushort.MaxValue);
            Array.Fill(converter._g, ushort.MaxValue);
            Array.Fill(converter._b, ushort.MaxValue);

            // Sort elements by layer index (lowest first, drawn first = bottom)
            foreach (var element in canvas.Elements.OrderBy(e => e.LayerIndex))
            {
                var ox = element.RelativeX * width;
                var oy = element.RelativeY * height;
                var segments = element.Draw();

                foreach (var segment in segments)
                {
                    converter.RenderSegment(segment, ox, oy);
                }
            }

            var needsAlpha = false;
            for (int i = 0; i < converter._pixels; i++)
            {
                if (converter._a[i] < 1f)
                { needsAlpha = true; break; }
            }

            return new Picture16bpp(width, height)
            {
                r = converter._r,
                g = converter._g,
                b = converter._b,
                a = needsAlpha ? converter._a : null,
                HasAlphaChannel = needsAlpha,
            };
        }

        // ---------------------------------------------------------------
        // Render dispatch
        // ---------------------------------------------------------------

        private void RenderSegment(VectorSegment seg, float ox, float oy)
        {
            switch (seg)
            {
                case StraightLineVectorSegment s:
                    RenderLine(s, ox, oy);
                    break;
                case RoundedRectangleVectorSegment s:
                    RenderRoundedRect(s, ox, oy);
                    break;
                case RectangleVectorSegment s:
                    RenderRect(s, ox, oy);
                    break;
                case EllipseVectorSegment s:
                    RenderEllipse(s, ox, oy);
                    break;
                case CubicBezierVectorSegment s:
                    RenderCubicBezier(s, ox, oy);
                    break;
                case QuadraticBezierVectorSegment s:
                    RenderQuadraticBezier(s, ox, oy);
                    break;
                case ArcVectorSegment s:
                    RenderArc(s, ox, oy);
                    break;
                case PolygonVectorSegment s:
                    RenderPolygon(s, ox, oy);
                    break;
                case PolylineVectorSegment s:
                    RenderPolyline(s, ox, oy);
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private int ToPixel(float fraction) => (int)(fraction + 0.5f);
        private float ToCanvas(int pixel) => pixel / (float)_width;

        private float CX(float segX, float ox) => ox + segX * _width;
        private float CY(float segY, float oy) => oy + segY * _height;

        private void BlendPixel(int x, int y, ushort r, ushort g, ushort b, float alpha)
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height || alpha <= 0f)
                return;

            var idx = y * _width + x;

            if (alpha >= 1f)
            {
                _r[idx] = r;
                _g[idx] = g;
                _b[idx] = b;
                _a[idx] = 1f;
            }
            else
            {
                var a0 = _a[idx];
                var aOut = a0 + alpha * (1f - a0);
                if (aOut <= 0f) return;

                _r[idx] = (ushort)((r * alpha + _r[idx] * a0 * (1f - alpha)) / aOut);
                _g[idx] = (ushort)((g * alpha + _g[idx] * a0 * (1f - alpha)) / aOut);
                _b[idx] = (ushort)((b * alpha + _b[idx] * a0 * (1f - alpha)) / aOut);
                _a[idx] = aOut;
            }
        }

        private void BlendPixel(int x, int y, ushort r, ushort g, ushort b, float alpha, ushort br, ushort bg, ushort bb)
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height || alpha <= 0f)
                return;

            var idx = y * _width + x;

            if (alpha >= 1f)
            {
                _r[idx] = r;
                _g[idx] = g;
                _b[idx] = b;
            }
            else
            {
                _r[idx] = (ushort)(r * alpha + br * (1f - alpha));
                _g[idx] = (ushort)(g * alpha + bg * (1f - alpha));
                _b[idx] = (ushort)(b * alpha + bb * (1f - alpha));
            }
        }

        // ---------------------------------------------------------------
        // Line
        // ---------------------------------------------------------------

        private void RenderLine(StraightLineVectorSegment s, float ox, float oy)
        {
            var x0 = CX(s.X1, ox);
            var y0 = CY(s.Y1, oy);
            var x1 = CX(s.X2, ox);
            var y1 = CY(s.Y2, oy);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawThickLine(x0, y0, x1, y1, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private void DrawThickLine(float x0, float y0, float x1, float y1, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = MathF.Sqrt(dx * dx + dy * dy);

            if (len < 0.001f)
            {
                var half = thickness * 0.5f;
                FillRect(x0 - half, y0 - half, thickness, thickness, r, g, b, alpha);
                return;
            }

            var nx = -dy / len * thickness * 0.5f;
            var ny = dx / len * thickness * 0.5f;

            // Build polygon of the thick line as a quad
            Span<(float x, float y)> pts = stackalloc (float, float)[4];
            pts[0] = (x0 + nx, y0 + ny);
            pts[1] = (x0 - nx, y0 - ny);
            pts[2] = (x1 - nx, y1 - ny);
            pts[3] = (x1 + nx, y1 + ny);

            FillPolygonScanline(pts, r, g, b, alpha);
        }

        private void DrawWuLine(float x0, float y0, float x1, float y1,
            ushort r, ushort g, ushort b, float alpha)
        {
            if (MathF.Abs(y1 - y0) < MathF.Abs(x1 - x0))
            {
                if (x0 > x1) { Swap(ref x0, ref x1); Swap(ref y0, ref y1); }
                DrawWuLineLow(x0, y0, x1, y1, r, g, b, alpha);
            }
            else
            {
                if (y0 > y1) { Swap(ref x0, ref x1); Swap(ref y0, ref y1); }
                DrawWuLineHigh(x0, y0, x1, y1, r, g, b, alpha);
            }
        }

        private void DrawWuLineLow(float x0, float y0, float x1, float y1,
            ushort r, ushort g, ushort b, float alpha)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var yi = 1f;
            if (dy < 0) { yi = -1f; dy = -dy; }

            var d = 2f * dy - dx;
            var y = y0;

            for (var x = (int)x0; x <= (int)x1; x++)
            {
                // Main pixel
                var coverage = 1f - (y - MathF.Floor(y));
                BlendPixel(x, (int)(y), r, g, b, alpha * coverage);
                BlendPixel(x, (int)(y) + (yi > 0 ? 1 : -1), r, g, b, alpha * (1f - coverage));

                if (d > 0)
                {
                    y += yi;
                    d -= 2f * dx;
                }
                d += 2f * dy;
            }
        }

        private void DrawWuLineHigh(float x0, float y0, float x1, float y1,
            ushort r, ushort g, ushort b, float alpha)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var xi = 1f;
            if (dx < 0) { xi = -1f; dx = -dx; }

            var d = 2f * dx - dy;
            var x = x0;

            for (var y = (int)y0; y <= (int)y1; y++)
            {
                var coverage = 1f - (x - MathF.Floor(x));
                BlendPixel((int)x, y, r, g, b, alpha * coverage);
                BlendPixel((int)(x) + (xi > 0 ? 1 : -1), y, r, g, b, alpha * (1f - coverage));

                if (d > 0)
                {
                    x += xi;
                    d -= 2f * dy;
                }
                d += 2f * dx;
            }
        }

        // ---------------------------------------------------------------
        // Rectangle
        // ---------------------------------------------------------------

        private void RenderRect(RectangleVectorSegment s, float ox, float oy)
        {
            var rx = CX(s.X, ox);
            var ry = CY(s.Y, oy);
            var rw = s.Width * _width;
            var rh = s.Height * _height;

            if (s.FillA > 0f)
                FillRect(rx, ry, rw, rh, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawRectStroke(rx, ry, rw, rh, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private void FillRect(float x, float y, float w, float h,
            ushort r, ushort g, ushort b, float alpha)
        {
            var x0 = Math.Clamp((int)(x + 0.5f), 0, _width - 1);
            var y0 = Math.Clamp((int)(y + 0.5f), 0, _height - 1);
            var x1 = Math.Clamp((int)(x + w + 0.5f), 0, _width - 1);
            var y1 = Math.Clamp((int)(y + h + 0.5f), 0, _height - 1);

            for (var py = y0; py <= y1; py++)
            {
                var row = py * _width;
                for (var px = x0; px <= x1; px++)
                {
                    var idx = row + px;
                    if (alpha >= 1f)
                    {
                        _r[idx] = r; _g[idx] = g; _b[idx] = b;
                    }
                    else
                    {
                        var a0 = _a[idx];
                        var aOut = a0 + alpha * (1f - a0);
                        _r[idx] = (ushort)((r * alpha + _r[idx] * a0 * (1f - alpha)) / aOut);
                        _g[idx] = (ushort)((g * alpha + _g[idx] * a0 * (1f - alpha)) / aOut);
                        _b[idx] = (ushort)((b * alpha + _b[idx] * a0 * (1f - alpha)) / aOut);
                        _a[idx] = aOut;
                    }
                }
            }
        }

        private void DrawRectStroke(float x, float y, float w, float h, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            var half = thickness * 0.5f;
            // Top
            FillRect(x - half, y - half, w + thickness, thickness, r, g, b, alpha);
            // Bottom
            FillRect(x - half, y + h - half, w + thickness, thickness, r, g, b, alpha);
            // Left
            FillRect(x - half, y + half, thickness, h - thickness, r, g, b, alpha);
            // Right
            FillRect(x + w - half, y + half, thickness, h - thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Rounded Rectangle
        // ---------------------------------------------------------------

        private void RenderRoundedRect(RoundedRectangleVectorSegment s, float ox, float oy)
        {
            var rx = CX(s.X, ox);
            var ry = CY(s.Y, oy);
            var rw = s.Width * _width;
            var rh = s.Height * _height;
            var radius = s.CornerRadius * MathF.Min(_width, _height);

            radius = MathF.Min(radius, MathF.Min(rw, rh) * 0.5f);

            if (s.FillA > 0f)
                FillRoundedRect(rx, ry, rw, rh, radius, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawRoundedRectStroke(rx, ry, rw, rh, radius, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private void FillRoundedRect(float x, float y, float w, float h, float radius,
            ushort r, ushort g, ushort b, float alpha)
        {
            if (radius <= 0.5f)
            {
                FillRect(x, y, w, h, r, g, b, alpha);
                return;
            }

            var x0 = (int)(x + 0.5f);
            var y0 = (int)(y + 0.5f);
            var x1 = (int)(x + w + 0.5f);
            var y1 = (int)(y + h + 0.5f);

            // Compute corner bounds: for each pixel, check if it's inside the rounded rect
            for (var py = Math.Max(y0, 0); py <= Math.Min(y1, _height - 1); py++)
            {
                for (var px = Math.Max(x0, 0); px <= Math.Min(x1, _width - 1); px++)
                {
                    // Determine which corner region the pixel falls into (or none = interior)
                    var cx = px + 0.5f;
                    var cy = py + 0.5f;

                    // Check if inside the rounded rect shape
                    var inside = false;

                    if (cx >= x + radius && cx <= x + w - radius)
                    {
                        // Between the vertical rounded corners — always inside if within horizontal range
                        inside = cy >= y && cy <= y + h;
                    }
                    else if (cy >= y + radius && cy <= y + h - radius)
                    {
                        // Between the horizontal rounded corners
                        inside = cx >= x && cx <= x + w;
                    }
                    else
                    {
                        // In one of the four corner quadrants — check distance from corner center
                        var cornerX = cx < x + w * 0.5f ? x + radius : x + w - radius;
                        var cornerY = cy < y + h * 0.5f ? y + radius : y + h - radius;
                        var dx = cx - cornerX;
                        var dy = cy - cornerY;
                        inside = (dx * dx + dy * dy) <= radius * radius;
                    }

                    if (inside)
                    {
                        var idx = py * _width + px;
                        if (alpha >= 1f)
                        {
                            _r[idx] = r; _g[idx] = g; _b[idx] = b;
                        }
                        else
                        {
                            var a0 = _a[idx];
                            var aOut = a0 + alpha * (1f - a0);
                            _r[idx] = (ushort)((r * alpha + _r[idx] * a0 * (1f - alpha)) / aOut);
                            _g[idx] = (ushort)((g * alpha + _g[idx] * a0 * (1f - alpha)) / aOut);
                            _b[idx] = (ushort)((b * alpha + _b[idx] * a0 * (1f - alpha)) / aOut);
                            _a[idx] = aOut;
                        }
                    }
                }
            }
        }

        private void DrawRoundedRectStroke(float x, float y, float w, float h, float radius, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Draw straight edge segments as filled rects
            var half = thickness * 0.5f;
            var ri = MathF.Max(0, radius - half); // inner radius approx

            // Top edge (between top-left and top-right corners)
            if (w > 2 * radius)
                FillRect(x + radius, y - half, w - 2 * radius, thickness, r, g, b, alpha);
            // Bottom edge
            if (w > 2 * radius)
                FillRect(x + radius, y + h - half, w - 2 * radius, thickness, r, g, b, alpha);
            // Left edge
            if (h > 2 * radius)
                FillRect(x - half, y + radius, thickness, h - 2 * radius, r, g, b, alpha);
            // Right edge
            if (h > 2 * radius)
                FillRect(x + w - half, y + radius, thickness, h - 2 * radius, r, g, b, alpha);

            // Draw arc segments for corners: subdivide each 90-degree corner arc into line segments
            var segments = Math.Max(4, (int)(radius * 0.5f));
            var cx1 = x + radius;
            var cy1 = y + radius;
            var cx2 = x + w - radius;
            var cy2 = y + radius;
            var cx3 = x + w - radius;
            var cy3 = y + h - radius;
            var cx4 = x + radius;
            var cy4 = y + h - radius;

            DrawArcLines(cx1, cy1, radius, MathF.PI, -MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(cx2, cy2, radius, MathF.PI * 1.5f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(cx3, cy3, radius, 0f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(cx4, cy4, radius, MathF.PI * 0.5f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Ellipse
        // ---------------------------------------------------------------

        private void RenderEllipse(EllipseVectorSegment s, float ox, float oy)
        {
            var cx = CX(s.X, ox);
            var cy = CY(s.Y, oy);
            var rx = s.RadiusX * _width;
            var ry = s.RadiusY * _height;

            if (rx <= 0f || ry <= 0f) return;

            if (s.FillA > 0f)
                FillEllipse(cx, cy, rx, ry, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawEllipseStroke(cx, cy, rx, ry, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private void FillEllipse(float cx, float cy, float rx, float ry,
            ushort r, ushort g, ushort b, float alpha)
        {
            var x0 = Math.Clamp((int)(cx - rx + 0.5f), 0, _width - 1);
            var y0 = Math.Clamp((int)(cy - ry + 0.5f), 0, _height - 1);
            var x1 = Math.Clamp((int)(cx + rx + 0.5f), 0, _width - 1);
            var y1 = Math.Clamp((int)(cy + ry + 0.5f), 0, _height - 1);

            var rx2 = rx * rx;
            var ry2 = ry * ry;

            for (var py = y0; py <= y1; py++)
            {
                var dy = py + 0.5f - cy;
                var dy2 = dy * dy;

                // Solve ellipse equation: (x-cx)²/rx² + dy²/ry² <= 1
                // => x = cx ± rx * sqrt(1 - dy²/ry²)
                var t = 1f - dy2 / ry2;
                if (t < 0) continue;

                var halfSpan = rx * MathF.Sqrt(t);
                var left = Math.Clamp((int)(cx - halfSpan + 0.5f), 0, _width - 1);
                var right = Math.Clamp((int)(cx + halfSpan + 0.5f), 0, _width - 1);

                for (var px = left; px <= right; px++)
                {
                    var idx = py * _width + px;
                    if (alpha >= 1f)
                    {
                        _r[idx] = r; _g[idx] = g; _b[idx] = b;
                    }
                    else
                    {
                        var a0 = _a[idx];
                        var aOut = a0 + alpha * (1f - a0);
                        _r[idx] = (ushort)((r * alpha + _r[idx] * a0 * (1f - alpha)) / aOut);
                        _g[idx] = (ushort)((g * alpha + _g[idx] * a0 * (1f - alpha)) / aOut);
                        _b[idx] = (ushort)((b * alpha + _b[idx] * a0 * (1f - alpha)) / aOut);
                        _a[idx] = aOut;
                    }
                }
            }
        }

        private void DrawEllipseStroke(float cx, float cy, float rx, float ry, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Approximate ellipse as line segments
            var numSegments = Math.Max(12, (int)(MathF.PI * MathF.Sqrt(rx + ry) * 0.5f));
            DrawArcLines(cx, cy, rx, ry, 0f, MathF.PI * 2f, numSegments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Arc (always stroke)
        // ---------------------------------------------------------------

        private void RenderArc(ArcVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var cx = CX(s.X, ox);
            var cy = CY(s.Y, oy);
            var rx = s.RadiusX * _width;
            var ry = s.RadiusY * _height;

            if (rx <= 0f || ry <= 0f) return;

            var numSegments = Math.Max(4, (int)(MathF.Abs(s.SweepAngle) * MathF.Sqrt(rx + ry) * 0.3f));
            DrawArcLines(cx, cy, rx, ry, s.StartAngle, s.SweepAngle, numSegments,
                s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        // ---------------------------------------------------------------
        // Cubic Bezier (always stroke)
        // ---------------------------------------------------------------

        private void RenderCubicBezier(CubicBezierVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var p0 = (x: CX(s.X1, ox), y: CY(s.Y1, oy));
            var p1 = (x: CX(s.X2, ox), y: CY(s.Y2, oy));
            var p2 = (x: CX(s.X3, ox), y: CY(s.Y3, oy));
            var p3 = (x: CX(s.X4, ox), y: CY(s.Y4, oy));

            // Flatten cubic bezier into line segments, then draw with thickness
            var points = new List<(float x, float y)>();
            FlattenCubicBezier(p0.x, p0.y, p1.x, p1.y, p2.x, p2.y, p3.x, p3.y, points, 0);

            DrawPolylineSegments(points, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void FlattenCubicBezier(float x0, float y0, float x1, float y1,
            float x2, float y2, float x3, float y3,
            List<(float x, float y)> points, int depth)
        {
            if (depth > 12) return;

            // Flatness test: distance from control points to chord
            var dx = x3 - x0;
            var dy = y3 - y0;
            var len2 = dx * dx + dy * dy;

            if (len2 > 0f)
            {
                var t1 = ((x1 - x0) * dx + (y1 - y0) * dy) / len2;
                var t2 = ((x2 - x0) * dx + (y2 - y0) * dy) / len2;
                var d1 = MathF.Abs((y1 - y0) - t1 * dy) + MathF.Abs((x1 - x0) - t1 * dx);
                var d2 = MathF.Abs((y2 - y0) - t2 * dy) + MathF.Abs((x2 - x0) - t2 * dx);

                if ((d1 + d2) * (d1 + d2) < len2 * 0.001f)
                {
                    points.Add((x3, y3));
                    return;
                }
            }

            // De Casteljau subdivision
            var mx01 = (x0 + x1) * 0.5f; var my01 = (y0 + y1) * 0.5f;
            var mx12 = (x1 + x2) * 0.5f; var my12 = (y1 + y2) * 0.5f;
            var mx23 = (x2 + x3) * 0.5f; var my23 = (y2 + y3) * 0.5f;
            var mx012 = (mx01 + mx12) * 0.5f; var my012 = (my01 + my12) * 0.5f;
            var mx123 = (mx12 + mx23) * 0.5f; var my123 = (my12 + my23) * 0.5f;
            var mx0123 = (mx012 + mx123) * 0.5f; var my0123 = (my012 + my123) * 0.5f;

            points.Add((mx0123, my0123));
            FlattenCubicBezier(x0, y0, mx01, my01, mx012, my012, mx0123, my0123, points, depth + 1);
            FlattenCubicBezier(mx0123, my0123, mx123, my123, mx23, my23, x3, y3, points, depth + 1);
        }

        // ---------------------------------------------------------------
        // Quadratic Bezier (always stroke)
        // ---------------------------------------------------------------

        private void RenderQuadraticBezier(QuadraticBezierVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var p0 = (x: CX(s.X1, ox), y: CY(s.Y1, oy));
            var p1 = (x: CX(s.X2, ox), y: CY(s.Y2, oy));
            var p2 = (x: CX(s.X3, ox), y: CY(s.Y3, oy));

            var points = new List<(float x, float y)>();
            FlattenQuadraticBezier(p0.x, p0.y, p1.x, p1.y, p2.x, p2.y, points, 0);

            DrawPolylineSegments(points, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void FlattenQuadraticBezier(float x0, float y0, float x1, float y1,
            float x2, float y2, List<(float x, float y)> points, int depth)
        {
            if (depth > 12) return;

            var dx = x2 - x0;
            var dy = y2 - y0;
            var len2 = dx * dx + dy * dy;

            if (len2 > 0f)
            {
                var t = ((x1 - x0) * dx + (y1 - y0) * dy) / len2;
                var d = MathF.Abs((y1 - y0) - t * dy) + MathF.Abs((x1 - x0) - t * dx);
                if (d * d < len2 * 0.001f)
                {
                    points.Add((x2, y2));
                    return;
                }
            }

            var mx01 = (x0 + x1) * 0.5f; var my01 = (y0 + y1) * 0.5f;
            var mx12 = (x1 + x2) * 0.5f; var my12 = (y1 + y2) * 0.5f;
            var mx012 = (mx01 + mx12) * 0.5f; var my012 = (my01 + my12) * 0.5f;

            points.Add((mx012, my012));
            FlattenQuadraticBezier(x0, y0, mx01, my01, mx012, my012, points, depth + 1);
            FlattenQuadraticBezier(mx012, my012, mx12, my12, x2, y2, points, depth + 1);
        }

        // ---------------------------------------------------------------
        // Polygon (fill + stroke)
        // ---------------------------------------------------------------

        private void RenderPolygon(PolygonVectorSegment s, float ox, float oy)
        {
            var pts = s.Points;
            if (pts.Length < 3) return;

            Span<(float x, float y)> canvasPts = stackalloc (float, float)[pts.Length];
            for (var i = 0; i < pts.Length; i++)
                canvasPts[i] = (CX(pts[i].X, ox), CY(pts[i].Y, oy));

            if (s.FillA > 0f)
                FillPolygonScanline(canvasPts, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
            {
                for (var i = 0; i < pts.Length; i++)
                {
                    var j = (i + 1) % pts.Length;
                    DrawThickLine(canvasPts[i].x, canvasPts[i].y, canvasPts[j].x, canvasPts[j].y,
                        s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
                }
            }
        }

        // ---------------------------------------------------------------
        // Polyline (always stroke)
        // ---------------------------------------------------------------

        private void RenderPolyline(PolylineVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var pts = s.Points;
            if (pts.Length < 2) return;

            for (var i = 1; i < pts.Length; i++)
            {
                DrawThickLine(CX(pts[i - 1].X, ox), CY(pts[i - 1].Y, oy),
                    CX(pts[i].X, ox), CY(pts[i].Y, oy),
                    s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
            }
        }

        // ---------------------------------------------------------------
        // Generic thick polyline (segment list from bezier flattening)
        // ---------------------------------------------------------------

        private void DrawPolylineSegments(List<(float x, float y)> points, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            for (var i = 1; i < points.Count; i++)
                DrawThickLine(points[i - 1].x, points[i - 1].y, points[i].x, points[i].y,
                    thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Arc subdivision helper
        // ---------------------------------------------------------------

        private void DrawArcLines(float cx, float cy, float rx, float ry,
            float startAngle, float sweepAngle, int segments, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            var step = sweepAngle / segments;
            var angle = startAngle;
            var cosStart = MathF.Cos(angle);
            var sinStart = MathF.Sin(angle);
            var prevX = cx + rx * cosStart;
            var prevY = cy + ry * sinStart;

            for (var i = 1; i <= segments; i++)
            {
                angle += step;
                var cosA = MathF.Cos(angle);
                var sinA = MathF.Sin(angle);
                var currX = cx + rx * cosA;
                var currY = cy + ry * sinA;

                DrawThickLine(prevX, prevY, currX, currY, thickness, r, g, b, alpha);
                prevX = currX;
                prevY = currY;
            }
        }

        private void DrawArcLines(float cx, float cy, float radius,
            float startAngle, float sweepAngle, int segments, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            DrawArcLines(cx, cy, radius, radius, startAngle, sweepAngle, segments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Scanline polygon fill
        // ---------------------------------------------------------------

        private void FillPolygonScanline(ReadOnlySpan<(float x, float y)> pts,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Find Y bounds
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < pts.Length; i++)
            {
                if (pts[i].y < minY) minY = pts[i].y;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }

            var y0 = Math.Clamp((int)(minY + 0.5f), 0, _height - 1);
            var y1 = Math.Clamp((int)(maxY + 0.5f), 0, _height - 1);

            // For each scanline, find intersections with polygon edges
            var intersections = new float[pts.Length];

            for (var py = y0; py <= y1; py++)
            {
                var y = py + 0.5f;
                var count = 0;

                for (var i = 0; i < pts.Length; i++)
                {
                    var j = (i + 1) % pts.Length;
                    var yA = pts[i].y;
                    var yB = pts[j].y;

                    if ((yA <= y && yB > y) || (yB <= y && yA > y))
                    {
                        var t = (y - yA) / (yB - yA);
                        intersections[count++] = pts[i].x + t * (pts[j].x - pts[i].x);
                    }
                }

                if (count < 2) continue;

                // Sort intersections
                Array.Sort(intersections, 0, count);

                // Fill between pairs
                for (var k = 0; k < count - 1; k += 2)
                {
                    var xL = Math.Clamp((int)(intersections[k] + 0.5f), 0, _width - 1);
                    var xR = Math.Clamp((int)(intersections[k + 1] + 0.5f), 0, _width - 1);

                    for (var px = xL; px <= xR; px++)
                    {
                        var idx = py * _width + px;
                        if (alpha >= 1f)
                        {
                            _r[idx] = r; _g[idx] = g; _b[idx] = b;
                        }
                        else
                        {
                            var a0 = _a[idx];
                            var aOut = a0 + alpha * (1f - a0);
                            _r[idx] = (ushort)((r * alpha + _r[idx] * a0 * (1f - alpha)) / aOut);
                            _g[idx] = (ushort)((g * alpha + _g[idx] * a0 * (1f - alpha)) / aOut);
                            _b[idx] = (ushort)((b * alpha + _b[idx] * a0 * (1f - alpha)) / aOut);
                            _a[idx] = aOut;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // Utility
        // ---------------------------------------------------------------

        private static void Swap(ref float a, ref float b)
        {
            var t = a; a = b; b = t;
        }
    }
}
