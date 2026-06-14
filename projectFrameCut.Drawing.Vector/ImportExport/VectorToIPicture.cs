using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Vector.ImportExport
{
    public interface IVectorPictureRasterizer
    {
        IPicture Convert(VectorPicture canvas, int width, int height, bool transparentBackground = false, AntiAliasMode aaMode = AntiAliasMode.None, CancellationToken cancellationToken = default);
    }

    /// <summary>Renders a <see cref="VectorPicture"/> to a raster <see cref="IPicture"/> using CPU scanline rendering.</summary>
    public class CPUVectorPictureRasterizer : IVectorPictureRasterizer
    {
        /// <summary>Holds per-call render state so all sub-methods can be static and the class stateless.</summary>
        private sealed class RenderContext
        {
            public ushort[] r = null!;
            public ushort[] g = null!;
            public ushort[] b = null!;
            public float[] a = null!;
            public int width;
            public int height;
            public float scaleX;
            public float scaleY;
            public CancellationToken cancellationToken;
        }

        /// <summary>Convert a vector canvas to a raster picture.</summary>
        /// <param name="canvas">The vector canvas to render.</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="transparentBackground">Whether the background should be transparent.</param>
        /// <param name="aaMode">Anti-aliasing mode (None, SSAA2x, SSAA4x, SSAA8x).</param>
        /// <returns>A 16-bit picture with the rendered result.</returns>
        public IPicture Convert(VectorPicture canvas, int width, int height,
            bool transparentBackground = false, AntiAliasMode aaMode = AntiAliasMode.None,
            CancellationToken cancellationToken = default)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException($"Canvas size must be positive. Got {width}x{height}.");

            int scaleFactor = aaMode != 0 ? (int)aaMode : 1;
            if (scaleFactor <= 0 || (scaleFactor != 1 && scaleFactor % 2 != 0))
                throw new ArgumentException($"Anti-aliasing scale factor must be one of pre-defined enum values, 0 or 1 for no Anti-aliasing, or a non-negative integer which is power of 2. Got {scaleFactor}.");

            int renderWidth = width * scaleFactor;
            int renderHeight = height * scaleFactor;
            int pixels = renderWidth * renderHeight;

            var ctx = new RenderContext
            {
                r = new ushort[pixels],
                g = new ushort[pixels],
                b = new ushort[pixels],
                a = new float[pixels],
                width = renderWidth,
                height = renderHeight,
                scaleX = renderWidth,
                scaleY = renderHeight,
                cancellationToken = cancellationToken,
            };

            if (transparentBackground)
            {
                Array.Fill(ctx.a, 0f);
            }
            else
            {
                Array.Fill(ctx.a, 1f);
                Array.Fill(ctx.r, ushort.MaxValue);
                Array.Fill(ctx.g, ushort.MaxValue);
                Array.Fill(ctx.b, ushort.MaxValue);
            }

            foreach (var element in canvas.Elements.OrderBy(e => e.LayerIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (element.UseUniformScale)
                {
                    float us = Math.Min(renderWidth, renderHeight);
                    ctx.scaleX = us;
                    ctx.scaleY = us;
                    var ox = element.BaseX * renderWidth + element.RelativeX * us;
                    var oy = element.BaseY * renderHeight + element.RelativeY * us;
                    var segments = element.Draw();
                    RenderSegments(ctx, segments, ox, oy, element.Rotation);
                }
                else
                {
                    ctx.scaleX = renderWidth;
                    ctx.scaleY = renderHeight;
                    var ox = element.RelativeX * renderWidth;
                    var oy = element.RelativeY * renderHeight;
                    var segments = element.Draw();
                    RenderSegments(ctx, segments, ox, oy, element.Rotation);
                }
            }

            if (scaleFactor > 1)
                return DownsampleToOutput(ctx, width, height, scaleFactor, renderWidth);

            var needsAlpha = false;
            for (int i = 0; i < pixels; i++)
            {
                if (ctx.a[i] < 1f)
                { needsAlpha = true; break; }
            }

            return new Picture16bpp(width, height)
            {
                r = ctx.r,
                g = ctx.g,
                b = ctx.b,
                a = needsAlpha ? ctx.a : null,
                HasAlphaChannel = needsAlpha,
            };
        }

        // ---------------------------------------------------------------
        // Render dispatch
        // ---------------------------------------------------------------

        private static void RenderSegments(
            RenderContext ctx, VectorSegment[] segments,
            float ox, float oy, float rotation)
        {
            if (rotation != 0f)
            {
                float cosA = MathF.Cos(rotation);
                float sinA = MathF.Sin(rotation);
                foreach (var segment in segments)
                    RenderSegment(ctx, RotateSegment(segment, cosA, sinA), ox, oy);
            }
            else
            {
                foreach (var segment in segments)
                    RenderSegment(ctx, segment, ox, oy);
            }
        }

        private static void RenderSegment(RenderContext ctx, VectorSegment seg, float ox, float oy)
        {
            switch (seg)
            {
                case StraightLineVectorSegment s:
                    RenderLine(ctx, s, ox, oy);
                    break;
                case RoundedRectangleVectorSegment s:
                    RenderRoundedRect(ctx, s, ox, oy);
                    break;
                case RectangleVectorSegment s:
                    RenderRect(ctx, s, ox, oy);
                    break;
                case EllipseVectorSegment s:
                    RenderEllipse(ctx, s, ox, oy);
                    break;
                case CubicBezierVectorSegment s:
                    RenderCubicBezier(ctx, s, ox, oy);
                    break;
                case QuadraticBezierVectorSegment s:
                    RenderQuadraticBezier(ctx, s, ox, oy);
                    break;
                case ArcVectorSegment s:
                    RenderArc(ctx, s, ox, oy);
                    break;
                case PolygonVectorSegment s:
                    RenderPolygon(ctx, s, ox, oy);
                    break;
                case PolylineVectorSegment s:
                    RenderPolyline(ctx, s, ox, oy);
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static int ToPixel(float fraction) => (int)(fraction + 0.5f);
        private static float ToCanvas(RenderContext ctx, int pixel) => pixel / (float)ctx.width;

        private static float CX(RenderContext ctx, float segX, float ox) => ox + segX * ctx.scaleX;
        private static float CY(RenderContext ctx, float segY, float oy) => oy + segY * ctx.scaleY;

        private static void BlendPixel(RenderContext ctx, int x, int y, ushort r, ushort g, ushort b, float alpha)
        {
            if ((uint)x >= (uint)ctx.width || (uint)y >= (uint)ctx.height || alpha <= 0f)
                return;

            var idx = y * ctx.width + x;

            if (alpha >= 1f)
            {
                ctx.r[idx] = r;
                ctx.g[idx] = g;
                ctx.b[idx] = b;
                ctx.a[idx] = 1f;
            }
            else
            {
                var a0 = ctx.a[idx];
                var aOut = a0 + alpha * (1f - a0);
                if (aOut <= 0f) return;

                ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                ctx.a[idx] = aOut;
            }
        }

        private static void BlendPixel(RenderContext ctx, int x, int y, ushort r, ushort g, ushort b, float alpha, ushort br, ushort bg, ushort bb)
        {
            if ((uint)x >= (uint)ctx.width || (uint)y >= (uint)ctx.height || alpha <= 0f)
                return;

            var idx = y * ctx.width + x;

            if (alpha >= 1f)
            {
                ctx.r[idx] = r;
                ctx.g[idx] = g;
                ctx.b[idx] = b;
            }
            else
            {
                ctx.r[idx] = (ushort)(r * alpha + br * (1f - alpha));
                ctx.g[idx] = (ushort)(g * alpha + bg * (1f - alpha));
                ctx.b[idx] = (ushort)(b * alpha + bb * (1f - alpha));
            }
        }

        // ---------------------------------------------------------------
        // Line
        // ---------------------------------------------------------------

        private static void RenderLine(RenderContext ctx, StraightLineVectorSegment s, float ox, float oy)
        {
            var x0 = CX(ctx, s.X1, ox);
            var y0 = CY(ctx, s.Y1, oy);
            var x1 = CX(ctx, s.X2, ox);
            var y1 = CY(ctx, s.Y2, oy);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawThickLine(ctx, x0, y0, x1, y1, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void DrawThickLine(RenderContext ctx, float x0, float y0, float x1, float y1, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = MathF.Sqrt(dx * dx + dy * dy);

            if (len < 0.001f)
            {
                var half = thickness * 0.5f;
                FillRect(ctx, x0 - half, y0 - half, thickness, thickness, r, g, b, alpha);
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

            FillPolygonScanline(ctx, pts, r, g, b, alpha);
        }

        private static void DrawWuLine(RenderContext ctx, float x0, float y0, float x1, float y1,
            ushort r, ushort g, ushort b, float alpha)
        {
            if (MathF.Abs(y1 - y0) < MathF.Abs(x1 - x0))
            {
                if (x0 > x1) { Swap(ref x0, ref x1); Swap(ref y0, ref y1); }
                DrawWuLineLow(ctx, x0, y0, x1, y1, r, g, b, alpha);
            }
            else
            {
                if (y0 > y1) { Swap(ref x0, ref x1); Swap(ref y0, ref y1); }
                DrawWuLineHigh(ctx, x0, y0, x1, y1, r, g, b, alpha);
            }
        }

        private static void DrawWuLineLow(RenderContext ctx, float x0, float y0, float x1, float y1,
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
                BlendPixel(ctx, x, (int)(y), r, g, b, alpha * coverage);
                BlendPixel(ctx, x, (int)(y) + (yi > 0 ? 1 : -1), r, g, b, alpha * (1f - coverage));

                if (d > 0)
                {
                    y += yi;
                    d -= 2f * dx;
                }
                d += 2f * dy;
            }
        }

        private static void DrawWuLineHigh(RenderContext ctx, float x0, float y0, float x1, float y1,
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
                BlendPixel(ctx, (int)x, y, r, g, b, alpha * coverage);
                BlendPixel(ctx, (int)(x) + (xi > 0 ? 1 : -1), y, r, g, b, alpha * (1f - coverage));

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

        private static void RenderRect(RenderContext ctx, RectangleVectorSegment s, float ox, float oy)
        {
            var rx = CX(ctx, s.X, ox);
            var ry = CY(ctx, s.Y, oy);
            var rw = s.Width * ctx.scaleX;
            var rh = s.Height * ctx.scaleY;

            if (s.FillA > 0f)
                FillRect(ctx, rx, ry, rw, rh, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawRectStroke(ctx, rx, ry, rw, rh, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void FillRect(RenderContext ctx, float x, float y, float w, float h,
            ushort r, ushort g, ushort b, float alpha)
        {
            var x0 = Math.Clamp((int)(x + 0.5f), 0, ctx.width - 1);
            var y0 = Math.Clamp((int)(y + 0.5f), 0, ctx.height - 1);
            var x1 = Math.Clamp((int)(x + w + 0.5f), 0, ctx.width - 1);
            var y1 = Math.Clamp((int)(y + h + 0.5f), 0, ctx.height - 1);

            for (var py = y0; py <= y1; py++)
            {
                var row = py * ctx.width;
                for (var px = x0; px <= x1; px++)
                {
                    var idx = row + px;
                    if (alpha >= 1f)
                    {
                        ctx.r[idx] = r; ctx.g[idx] = g; ctx.b[idx] = b; ctx.a[idx] = 1f;
                    }
                    else
                    {
                        var a0 = ctx.a[idx];
                        var aOut = a0 + alpha * (1f - a0);
                        ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.a[idx] = aOut;
                    }
                }
            }
        }

        private static void DrawRectStroke(RenderContext ctx, float x, float y, float w, float h, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            var half = thickness * 0.5f;
            // Top
            FillRect(ctx, x - half, y - half, w + thickness, thickness, r, g, b, alpha);
            // Bottom
            FillRect(ctx, x - half, y + h - half, w + thickness, thickness, r, g, b, alpha);
            // Left
            FillRect(ctx, x - half, y + half, thickness, h - thickness, r, g, b, alpha);
            // Right
            FillRect(ctx, x + w - half, y + half, thickness, h - thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Rounded Rectangle
        // ---------------------------------------------------------------

        private static void RenderRoundedRect(RenderContext ctx, RoundedRectangleVectorSegment s, float ox, float oy)
        {
            var rx = CX(ctx, s.X, ox);
            var ry = CY(ctx, s.Y, oy);
            var rw = s.Width * ctx.scaleX;
            var rh = s.Height * ctx.scaleY;
            var radius = s.CornerRadius * MathF.Min(ctx.scaleX, ctx.scaleY);

            radius = MathF.Min(radius, MathF.Min(rw, rh) * 0.5f);

            if (s.FillA > 0f)
                FillRoundedRect(ctx, rx, ry, rw, rh, radius, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawRoundedRectStroke(ctx, rx, ry, rw, rh, radius, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void FillRoundedRect(RenderContext ctx, float x, float y, float w, float h, float radius,
            ushort r, ushort g, ushort b, float alpha)
        {
            if (radius <= 0.5f)
            {
                FillRect(ctx, x, y, w, h, r, g, b, alpha);
                return;
            }

            var x0 = (int)(x + 0.5f);
            var y0 = (int)(y + 0.5f);
            var x1 = (int)(x + w + 0.5f);
            var y1 = (int)(y + h + 0.5f);

            var clampY0 = Math.Max(y0, 0);
            var clampY1 = Math.Min(y1, ctx.height - 1);
            var clampX0 = Math.Max(x0, 0);
            var clampX1 = Math.Min(x1, ctx.width - 1);

            Parallel.For(clampY0, clampY1 + 1, new ParallelOptions { CancellationToken = ctx.cancellationToken }, py =>
            {
                for (var px = clampX0; px <= clampX1; px++)
                {
                    var cx = px + 0.5f;
                    var cy = py + 0.5f;

                    var inside = false;

                    if (cx >= x + radius && cx <= x + w - radius)
                    {
                        inside = cy >= y && cy <= y + h;
                    }
                    else if (cy >= y + radius && cy <= y + h - radius)
                    {
                        inside = cx >= x && cx <= x + w;
                    }
                    else
                    {
                        var cornerX = cx < x + w * 0.5f ? x + radius : x + w - radius;
                        var cornerY = cy < y + h * 0.5f ? y + radius : y + h - radius;
                        var dx = cx - cornerX;
                        var dy = cy - cornerY;
                        inside = (dx * dx + dy * dy) <= radius * radius;
                    }

                    if (inside)
                    {
                        var idx = py * ctx.width + px;
                        if (alpha >= 1f)
                        {
                            ctx.r[idx] = r; ctx.g[idx] = g; ctx.b[idx] = b; ctx.a[idx] = 1f;
                        }
                        else
                        {
                            var a0 = ctx.a[idx];
                            var aOut = a0 + alpha * (1f - a0);
                            ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.a[idx] = aOut;
                        }
                    }
                }
            });
        }

        private static void DrawRoundedRectStroke(RenderContext ctx, float x, float y, float w, float h, float radius, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Draw straight edge segments as filled rects
            var half = thickness * 0.5f;

            // Top edge (between top-left and top-right corners)
            if (w > 2 * radius)
                FillRect(ctx, x + radius, y - half, w - 2 * radius, thickness, r, g, b, alpha);
            // Bottom edge
            if (w > 2 * radius)
                FillRect(ctx, x + radius, y + h - half, w - 2 * radius, thickness, r, g, b, alpha);
            // Left edge
            if (h > 2 * radius)
                FillRect(ctx, x - half, y + radius, thickness, h - 2 * radius, r, g, b, alpha);
            // Right edge
            if (h > 2 * radius)
                FillRect(ctx, x + w - half, y + radius, thickness, h - 2 * radius, r, g, b, alpha);

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

            DrawArcLines(ctx, cx1, cy1, radius, MathF.PI, -MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(ctx, cx2, cy2, radius, MathF.PI * 1.5f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(ctx, cx3, cy3, radius, 0f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
            DrawArcLines(ctx, cx4, cy4, radius, MathF.PI * 0.5f, MathF.PI * 0.5f, segments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Ellipse
        // ---------------------------------------------------------------

        private static void RenderEllipse(RenderContext ctx, EllipseVectorSegment s, float ox, float oy)
        {
            var cx = CX(ctx, s.X, ox);
            var cy = CY(ctx, s.Y, oy);
            var rx = s.RadiusX * ctx.scaleX;
            var ry = s.RadiusY * ctx.scaleY;

            if (rx <= 0f || ry <= 0f) return;

            if (s.FillA > 0f)
                FillEllipse(ctx, cx, cy, rx, ry, s.FillR, s.FillG, s.FillB, s.FillA);

            if (s.Thickness > 0f && s.StrokeA > 0f)
                DrawEllipseStroke(ctx, cx, cy, rx, ry, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        private static void FillEllipse(RenderContext ctx, float cx, float cy, float rx, float ry,
            ushort r, ushort g, ushort b, float alpha)
        {
            var x0 = Math.Clamp((int)(cx - rx + 0.5f), 0, ctx.width - 1);
            var y0 = Math.Clamp((int)(cy - ry + 0.5f), 0, ctx.height - 1);
            var x1 = Math.Clamp((int)(cx + rx + 0.5f), 0, ctx.width - 1);
            var y1 = Math.Clamp((int)(cy + ry + 0.5f), 0, ctx.height - 1);

            var rx2 = rx * rx;
            var ry2 = ry * ry;

            Parallel.For(y0, y1 + 1, new ParallelOptions { CancellationToken = ctx.cancellationToken }, py =>
            {
                var dy = py + 0.5f - cy;
                var dy2 = dy * dy;

                var t = 1f - dy2 / ry2;
                if (t < 0) return;

                var halfSpan = rx * MathF.Sqrt(t);
                var left = Math.Clamp((int)(cx - halfSpan + 0.5f), 0, ctx.width - 1);
                var right = Math.Clamp((int)(cx + halfSpan + 0.5f), 0, ctx.width - 1);

                for (var px = left; px <= right; px++)
                {
                    var idx = py * ctx.width + px;
                    if (alpha >= 1f)
                    {
                        ctx.r[idx] = r; ctx.g[idx] = g; ctx.b[idx] = b; ctx.a[idx] = 1f;
                    }
                    else
                    {
                        var a0 = ctx.a[idx];
                        var aOut = a0 + alpha * (1f - a0);
                        ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                        ctx.a[idx] = aOut;
                    }
                }
            });
        }

        private static void DrawEllipseStroke(RenderContext ctx, float cx, float cy, float rx, float ry, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Approximate ellipse as line segments
            var numSegments = Math.Max(12, (int)(MathF.PI * MathF.Sqrt(rx + ry) * 0.5f));
            DrawArcLines(ctx, cx, cy, rx, ry, 0f, MathF.PI * 2f, numSegments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Arc (always stroke)
        // ---------------------------------------------------------------

        private static void RenderArc(RenderContext ctx, ArcVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var cx = CX(ctx, s.X, ox);
            var cy = CY(ctx, s.Y, oy);
            var rx = s.RadiusX * ctx.scaleX;
            var ry = s.RadiusY * ctx.scaleY;

            if (rx <= 0f || ry <= 0f) return;

            var numSegments = Math.Max(4, (int)(MathF.Abs(s.SweepAngle) * MathF.Sqrt(rx + ry) * 0.3f));
            DrawArcLines(ctx, cx, cy, rx, ry, s.StartAngle, s.SweepAngle, numSegments,
                s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
        }

        // ---------------------------------------------------------------
        // Cubic Bezier (always stroke)
        // ---------------------------------------------------------------

        private static void RenderCubicBezier(RenderContext ctx, CubicBezierVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var p0 = (x: CX(ctx, s.X1, ox), y: CY(ctx, s.Y1, oy));
            var p1 = (x: CX(ctx, s.X2, ox), y: CY(ctx, s.Y2, oy));
            var p2 = (x: CX(ctx, s.X3, ox), y: CY(ctx, s.Y3, oy));
            var p3 = (x: CX(ctx, s.X4, ox), y: CY(ctx, s.Y4, oy));

            // Flatten cubic bezier into line segments, then draw with thickness
            var points = new List<(float x, float y)>();
            FlattenCubicBezier(p0.x, p0.y, p1.x, p1.y, p2.x, p2.y, p3.x, p3.y, points, 0);

            DrawPolylineSegments(ctx, points, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
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

        private static void RenderQuadraticBezier(RenderContext ctx, QuadraticBezierVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var p0 = (x: CX(ctx, s.X1, ox), y: CY(ctx, s.Y1, oy));
            var p1 = (x: CX(ctx, s.X2, ox), y: CY(ctx, s.Y2, oy));
            var p2 = (x: CX(ctx, s.X3, ox), y: CY(ctx, s.Y3, oy));

            var points = new List<(float x, float y)>();
            FlattenQuadraticBezier(p0.x, p0.y, p1.x, p1.y, p2.x, p2.y, points, 0);

            DrawPolylineSegments(ctx, points, s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
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

        private static void RenderPolygon(RenderContext ctx, PolygonVectorSegment s, float ox, float oy)
        {
            var pts = s.Points;
            if (pts.Length < 3) return;

            Span<(float x, float y)> canvasPts = pts.Length <= 256
                ? stackalloc (float, float)[pts.Length]
                : new (float, float)[pts.Length];
            for (var i = 0; i < pts.Length; i++)
                canvasPts[i] = (CX(ctx, pts[i].X, ox), CY(ctx, pts[i].Y, oy));

            bool hasFill = s.FillA > 0f;
            bool hasStroke = s.Thickness > 0f && s.StrokeA > 0f;
            bool hasHoles = s.Holes is { Length: > 0 };

            if (hasFill)
            {
                if (hasHoles)
                {
                    FillPolygonScanlineNonZero(ctx, canvasPts, s.Holes!, ox, oy, s.FillR, s.FillG, s.FillB, s.FillA);
                }
                else
                {
                    FillPolygonScanline(ctx, canvasPts, s.FillR, s.FillG, s.FillB, s.FillA);
                }
            }

            if (hasStroke)
            {
                for (var i = 0; i < pts.Length; i++)
                {
                    var j = (i + 1) % pts.Length;
                    DrawThickLine(ctx, canvasPts[i].x, canvasPts[i].y, canvasPts[j].x, canvasPts[j].y,
                        s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
                }
            }
        }

        // ---------------------------------------------------------------
        // Polyline (always stroke)
        // ---------------------------------------------------------------

        private static void RenderPolyline(RenderContext ctx, PolylineVectorSegment s, float ox, float oy)
        {
            if (s.Thickness <= 0f || s.StrokeA <= 0f) return;

            var pts = s.Points;
            if (pts.Length < 2) return;

            for (var i = 1; i < pts.Length; i++)
            {
                DrawThickLine(ctx, CX(ctx, pts[i - 1].X, ox), CY(ctx, pts[i - 1].Y, oy),
                    CX(ctx, pts[i].X, ox), CY(ctx, pts[i].Y, oy),
                    s.Thickness, s.StrokeR, s.StrokeG, s.StrokeB, s.StrokeA);
            }
        }

        // ---------------------------------------------------------------
        // Generic thick polyline (segment list from bezier flattening)
        // ---------------------------------------------------------------

        private static void DrawPolylineSegments(RenderContext ctx, List<(float x, float y)> points, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            for (var i = 1; i < points.Count; i++)
                DrawThickLine(ctx, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y,
                    thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Arc subdivision helper
        // ---------------------------------------------------------------

        private static void DrawArcLines(RenderContext ctx, float cx, float cy, float rx, float ry,
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

                DrawThickLine(ctx, prevX, prevY, currX, currY, thickness, r, g, b, alpha);
                prevX = currX;
                prevY = currY;
            }
        }

        private static void DrawArcLines(RenderContext ctx, float cx, float cy, float radius,
            float startAngle, float sweepAngle, int segments, float thickness,
            ushort r, ushort g, ushort b, float alpha)
        {
            DrawArcLines(ctx, cx, cy, radius, radius, startAngle, sweepAngle, segments, thickness, r, g, b, alpha);
        }

        // ---------------------------------------------------------------
        // Scanline polygon fill (non-zero winding for a single contour)
        // ---------------------------------------------------------------

        private static void FillPolygonScanline(RenderContext ctx, ReadOnlySpan<(float x, float y)> pts,
            ushort r, ushort g, ushort b, float alpha)
        {
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < pts.Length; i++)
            {
                if (pts[i].y < minY) minY = pts[i].y;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }

            var y0 = Math.Clamp((int)(minY + 0.5f), 0, ctx.height - 1);
            var y1 = Math.Clamp((int)(maxY + 0.5f), 0, ctx.height - 1);
            var n = pts.Length;
            var edges = new (float yMin, float yMax, float xAtYMin, float dx, int windingDelta)[n];
            int edgeCount = 0;

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var yA = pts[i].y;
                var yB = pts[j].y;
                var xA = pts[i].x;
                var xB = pts[j].x;
                if (yA == yB)
                    continue;

                edges[edgeCount++] = yA < yB
                    ? (yA, yB, xA, (xB - xA) / (yB - yA), +1)
                    : (yB, yA, xB, (xA - xB) / (yA - yB), -1);
            }

            Parallel.For(y0, y1 + 1, new ParallelOptions { CancellationToken = ctx.cancellationToken }, py =>
            {
                var y = py + 0.5f;
                var intersections = new (float x, int delta)[edgeCount];
                var count = 0;

                for (var i = 0; i < edgeCount; i++)
                {
                    if (y >= edges[i].yMin && y < edges[i].yMax)
                        intersections[count++] = (edges[i].xAtYMin + (y - edges[i].yMin) * edges[i].dx, edges[i].windingDelta);
                }

                if (count < 2) return;

                Array.Sort(intersections, 0, count, Comparer<(float x, int delta)>.Create((a, b) => a.x.CompareTo(b.x)));

                var winding = 0;
                var k = 0;
                while (k < count)
                {
                    var xStart = intersections[k].x;
                    do
                    {
                        winding += intersections[k].delta;
                        k++;
                    } while (k < count && MathF.Abs(intersections[k].x - xStart) <= 1e-5f);

                    if (winding == 0 || k >= count)
                        continue;

                    var xEnd = intersections[k].x;
                    var xL = Math.Clamp((int)(xStart + 0.5f), 0, ctx.width - 1);
                    var xR = Math.Clamp((int)(xEnd + 0.5f), 0, ctx.width - 1);

                    for (var px = xL; px <= xR; px++)
                    {
                        var idx = py * ctx.width + px;
                        if (alpha >= 1f)
                        {
                            ctx.r[idx] = r; ctx.g[idx] = g; ctx.b[idx] = b; ctx.a[idx] = 1f;
                        }
                        else
                        {
                            var a0 = ctx.a[idx];
                            var aOut = a0 + alpha * (1f - a0);
                            ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.a[idx] = aOut;
                        }
                    }
                }
            });
        }

        // ---------------------------------------------------------------
        // Non-zero winding polygon fill (outer + holes)
        // ---------------------------------------------------------------

        private static void FillPolygonScanlineNonZero(
            RenderContext ctx,
            ReadOnlySpan<(float x, float y)> outerPts,
            Point[][] holes,
            float ox, float oy,
            ushort r, ushort g, ushort b, float alpha)
        {
            // Collect all edge segments: outer + every hole contour. Preserve
            // edge direction so the non-zero winding rule can distinguish
            // filled regions from counters, including outlines that touch or
            // nearly touch at shared scanlines.
            int totalEdges = outerPts.Length;
            foreach (var h in holes)
                totalEdges += h.Length;

            var edges = new (float yMin, float yMax, float xAtYMin, float dx, int windingDelta)[totalEdges];
            int ei = 0;

            void AddEdges(ReadOnlySpan<(float x, float y)> pts)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    int j = (i + 1) % pts.Length;
                    float yA = pts[i].y, yB = pts[j].y;
                    float xA = pts[i].x, xB = pts[j].x;
                    if (yA == yB) continue; // horizontal edge — skip
                    edges[ei++] = yA < yB
                        ? (yA, yB, xA, (xB - xA) / (yB - yA), +1)
                        : (yB, yA, xB, (xA - xB) / (yA - yB), -1);
                }
            }

            AddEdges(outerPts);
            Span<(float x, float y)> holeBuffer = stackalloc (float, float)[256];
            foreach (var hole in holes)
            {
                Span<(float x, float y)> holePts = hole.Length <= 256
                    ? holeBuffer[..hole.Length]
                    : new (float, float)[hole.Length];
                for (int i = 0; i < hole.Length; i++)
                    holePts[i] = (CX(ctx, hole[i].X, ox), CY(ctx, hole[i].Y, oy));
                AddEdges(holePts);
            }
            int edgeCount = ei;

            // Find Y bounds
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < edgeCount; i++)
            {
                if (edges[i].yMin < minY) minY = edges[i].yMin;
                if (edges[i].yMax > maxY) maxY = edges[i].yMax;
            }

            int y0 = Math.Clamp((int)(minY + 0.5f), 0, ctx.height - 1);
            int y1 = Math.Clamp((int)(maxY + 0.5f), 0, ctx.height - 1);

            var totalEdgesCount = totalEdges; // captured by closure

            Parallel.For(y0, y1 + 1, new ParallelOptions { CancellationToken = ctx.cancellationToken }, py =>
            {
                float y = py + 0.5f;
                var intersections = new (float x, int delta)[totalEdgesCount];
                int count = 0;

                for (int i = 0; i < edgeCount; i++)
                {
                    if (y >= edges[i].yMin && y < edges[i].yMax)
                        intersections[count++] = (edges[i].xAtYMin + (y - edges[i].yMin) * edges[i].dx, edges[i].windingDelta);
                }

                if (count < 2) return;

                Array.Sort(intersections, 0, count, Comparer<(float x, int delta)>.Create((a, b) => a.x.CompareTo(b.x)));

                int winding = 0;
                int k = 0;
                while (k < count)
                {
                    float xStart = intersections[k].x;
                    do
                    {
                        winding += intersections[k].delta;
                        k++;
                    } while (k < count && MathF.Abs(intersections[k].x - xStart) <= 1e-5f);

                    if (winding == 0 || k >= count)
                        continue;

                    float xEnd = intersections[k].x;
                    int xL = Math.Clamp((int)(xStart + 0.5f), 0, ctx.width - 1);
                    int xR = Math.Clamp((int)(xEnd + 0.5f), 0, ctx.width - 1);

                    for (int px = xL; px <= xR; px++)
                    {
                        int idx = py * ctx.width + px;
                        if (alpha >= 1f)
                        {
                            ctx.r[idx] = r; ctx.g[idx] = g; ctx.b[idx] = b; ctx.a[idx] = 1f;
                        }
                        else
                        {
                            var a0 = ctx.a[idx];
                            var aOut = a0 + alpha * (1f - a0);
                            ctx.r[idx] = (ushort)((r * alpha + ctx.r[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.g[idx] = (ushort)((g * alpha + ctx.g[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.b[idx] = (ushort)((b * alpha + ctx.b[idx] * a0 * (1f - alpha)) / aOut);
                            ctx.a[idx] = aOut;
                        }
                    }
                }
            });
        }

        // ---------------------------------------------------------------
        // Utility
        // ---------------------------------------------------------------

        private static void Swap(ref float a, ref float b)
        {
            var t = a; a = b; b = t;
        }

        // ---------------------------------------------------------------
        //  Rotation
        // ---------------------------------------------------------------

        private static VectorSegment RotateSegment(VectorSegment seg, float cosA, float sinA)
        {
            return seg switch
            {
                StraightLineVectorSegment s => s with
                {
                    X1 = s.X1 * cosA - s.Y1 * sinA,
                    Y1 = s.X1 * sinA + s.Y1 * cosA,
                    X2 = s.X2 * cosA - s.Y2 * sinA,
                    Y2 = s.X2 * sinA + s.Y2 * cosA,
                },
                QuadraticBezierVectorSegment s => s with
                {
                    X1 = s.X1 * cosA - s.Y1 * sinA,
                    Y1 = s.X1 * sinA + s.Y1 * cosA,
                    X2 = s.X2 * cosA - s.Y2 * sinA,
                    Y2 = s.X2 * sinA + s.Y2 * cosA,
                    X3 = s.X3 * cosA - s.Y3 * sinA,
                    Y3 = s.X3 * sinA + s.Y3 * cosA,
                },
                CubicBezierVectorSegment s => s with
                {
                    X1 = s.X1 * cosA - s.Y1 * sinA,
                    Y1 = s.X1 * sinA + s.Y1 * cosA,
                    X2 = s.X2 * cosA - s.Y2 * sinA,
                    Y2 = s.X2 * sinA + s.Y2 * cosA,
                    X3 = s.X3 * cosA - s.Y3 * sinA,
                    Y3 = s.X3 * sinA + s.Y3 * cosA,
                    X4 = s.X4 * cosA - s.Y4 * sinA,
                    Y4 = s.X4 * sinA + s.Y4 * cosA,
                },
                PolygonVectorSegment s => s with
                {
                    Points = Array.ConvertAll(s.Points, p => new Point(
                        p.X * cosA - p.Y * sinA,
                        p.X * sinA + p.Y * cosA)),
                    Holes = s.Holes?.Select(h => Array.ConvertAll(h, p => new Point(
                        p.X * cosA - p.Y * sinA,
                        p.X * sinA + p.Y * cosA))).ToArray(),
                },
                PolylineVectorSegment s => s with
                {
                    Points = Array.ConvertAll(s.Points, p => new Point(
                        p.X * cosA - p.Y * sinA,
                        p.X * sinA + p.Y * cosA)),
                },
                // Convert rectangles / rounded-rects to rotated polygons
                RoundedRectangleVectorSegment s => SegToRotatedPolygon(s.X, s.Y, s.Width, s.Height, cosA, sinA, s.CornerRadius, s),
                RectangleVectorSegment s => SegToRotatedPolygon(s.X, s.Y, s.Width, s.Height, cosA, sinA, null, s),
                // Rotate ellipse center; the axes-aligned ellipse becomes a rotated one
                EllipseVectorSegment s => s with
                {
                    X = s.X * cosA - s.Y * sinA,
                    Y = s.X * sinA + s.Y * cosA,
                },
                ArcVectorSegment s => s with
                {
                    X = s.X * cosA - s.Y * sinA,
                    Y = s.X * sinA + s.Y * cosA,
                    StartAngle = s.StartAngle + MathF.Atan2(sinA, cosA),
                },
                _ => seg,
            };
        }

        private static PolygonVectorSegment SegToRotatedPolygon(
            float x, float y, float w, float h, float cosA, float sinA, float? cornerRadius, VectorSegment props)
        {
            if (cornerRadius is > 0f)
            {
                // Rounded rect: approximate via a polygon with enough points
                var pts = new List<Point>();
                float r = cornerRadius.Value;
                const int cornerSegments = 8;

                void AddArc(float cx, float cy, float startAngle, float sweepAngle, int segs)
                {
                    for (int i = 0; i <= segs; i++)
                    {
                        float a = startAngle + sweepAngle * i / segs;
                        pts.Add(new Point(cx + r * MathF.Cos(a), cy + r * MathF.Sin(a)));
                    }
                }

                // Top-left (π to -π/2), Top-right (-π/2 to 0), Bottom-right (0 to π/2), Bottom-left (π/2 to π)
                float x0 = x + r, x1 = x + w - r, y0 = y + r, y1 = y + h - r;
                AddArc(x1, y0, -MathF.PI / 2f, MathF.PI / 2f, cornerSegments);
                AddArc(x1, y1, 0, MathF.PI / 2f, cornerSegments);
                AddArc(x0, y1, MathF.PI / 2f, MathF.PI / 2f, cornerSegments);
                AddArc(x0, y0, MathF.PI, MathF.PI / 2f, cornerSegments);

                var rotated = pts.ConvertAll(p => new Point(
                    p.X * cosA - p.Y * sinA,
                    p.X * sinA + p.Y * cosA));

                return new PolygonVectorSegment
                {
                    Points = rotated.ToArray(),
                    FillR = props.FillR,
                    FillG = props.FillG,
                    FillB = props.FillB,
                    FillA = props.FillA,
                    Thickness = props.Thickness,
                    StrokeR = props.StrokeR,
                    StrokeG = props.StrokeG,
                    StrokeB = props.StrokeB,
                    StrokeA = props.StrokeA,
                };
            }

            // Plain rectangle → 4-corner polygon
            Span<Point> corners = stackalloc Point[4]
            {
                new(x, y),
                new(x + w, y),
                new(x + w, y + h),
                new(x, y + h),
            };

            var rotatedCorners = new Point[4];
            for (int i = 0; i < 4; i++)
                rotatedCorners[i] = new Point(
                    corners[i].X * cosA - corners[i].Y * sinA,
                    corners[i].X * sinA + corners[i].Y * cosA);

            return new PolygonVectorSegment
            {
                Points = rotatedCorners,
                FillR = props.FillR,
                FillG = props.FillG,
                FillB = props.FillB,
                FillA = props.FillA,
                Thickness = props.Thickness,
                StrokeR = props.StrokeR,
                StrokeG = props.StrokeG,
                StrokeB = props.StrokeB,
                StrokeA = props.StrokeA,
            };
        }

        // ---------------------------------------------------------------
        //  SSAA 下采样
        // ---------------------------------------------------------------

        private static IPicture DownsampleToOutput(
            RenderContext ctx,
            int outWidth, int outHeight, int scaleFactor, int renderWidth)
        {
            int pixels = outWidth * outHeight;
            int blockSize = scaleFactor * scaleFactor;

            var outR = new ushort[pixels];
            var outG = new ushort[pixels];
            var outB = new ushort[pixels];
            var outA = new float[pixels];

            Parallel.For(0, outHeight, new ParallelOptions { CancellationToken = ctx.cancellationToken }, y =>
            {
                int inBaseY = y * scaleFactor;
                for (int x = 0; x < outWidth; x++)
                {
                    int inBaseX = x * scaleFactor;
                    long sumR = 0, sumG = 0, sumB = 0;
                    long sumA = 0;

                    for (int sy = 0; sy < scaleFactor; sy++)
                    {
                        int row = (inBaseY + sy) * renderWidth + inBaseX;
                        for (int sx = 0; sx < scaleFactor; sx++)
                        {
                            int idx = row + sx;
                            sumR += ctx.r[idx];
                            sumG += ctx.g[idx];
                            sumB += ctx.b[idx];
                            sumA += (long)(ctx.a[idx] * ushort.MaxValue);
                        }
                    }

                    int oi = y * outWidth + x;
                    outR[oi] = (ushort)(sumR / blockSize);
                    outG[oi] = (ushort)(sumG / blockSize);
                    outB[oi] = (ushort)(sumB / blockSize);
                    outA[oi] = (float)sumA / (blockSize * ushort.MaxValue);
                }
            });

            var needsAlpha = false;
            for (int i = 0; i < pixels; i++)
            {
                if (outA[i] < 1f)
                { needsAlpha = true; break; }
            }

            return new Picture16bpp(outWidth, outHeight)
            {
                r = outR,
                g = outG,
                b = outB,
                a = needsAlpha ? outA : null,
                HasAlphaChannel = needsAlpha,
            };
        }
    }
}
