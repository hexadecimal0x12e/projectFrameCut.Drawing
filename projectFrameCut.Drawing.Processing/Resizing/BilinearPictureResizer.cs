using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Processing.Resizing
{
    /// <summary>CPU-based bilinear picture resizer implementing <see cref="IPictureResizer"/>.</summary>
    public class BilinearPictureResizer : IPictureResizer
    {
        public IPicture<ushort> Resize(IPicture<ushort> source, int targetWidth, int targetHeight, bool preserveAspect)
        {
            var sw = Stopwatch.StartNew();
            if (targetWidth == source.Width && targetHeight == source.Height) return source;

            if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentException("targetWidth and targetHeight must be positive");
            if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("Source image has invalid dimensions");

            int destW = targetWidth;
            int destH = targetHeight;

            if (preserveAspect)
            {
                double sx = (double)targetWidth / source.Width;
                double sy = (double)targetHeight / source.Height;
                double s = Math.Min(sx, sy);
                destW = Math.Max(1, (int)(source.Width * s + 0.5));
                destH = Math.Max(1, (int)(source.Height * s + 0.5));
                if (destW == source.Width && destH == source.Height) return source;
            }
            int dstPixels = checked(destW * destH);

            var result = new Picture16bpp(destW, destH)
            {
                r = new ushort[dstPixels],
                g = new ushort[dstPixels],
                b = new ushort[dstPixels],
                a = source.HasAlphaChannel ? new float[dstPixels] : null,
                HasAlphaChannel = source.HasAlphaChannel,
                ProcessStack = source.ProcessStack
            };

            double xRatio = (double)source.Width / destW;
            double yRatio = (double)source.Height / destH;

            for (int y = 0; y < destH; y++)
            {
                double srcY = (y + 0.5) * yRatio - 0.5;
                int y0 = (int)Math.Floor(srcY);
                int y1 = y0 + 1;
                double wy = srcY - y0;
                if (y0 < 0)
                {
                    y0 = 0; y1 = 0; wy = 0;
                }
                else if (y0 >= source.Height) { y0 = source.Height - 1; y1 = source.Height - 1; wy = 0; }
                if (y1 >= source.Height) { y1 = source.Height - 1; }

                for (int x = 0; x < destW; x++)
                {
                    double srcX = (x + 0.5) * xRatio - 0.5;
                    int x0 = (int)Math.Floor(srcX);
                    int x1 = x0 + 1;
                    double wx = srcX - x0;
                    if (x0 < 0)
                    {
                        x0 = 0; x1 = 0; wx = 0;
                    }
                    else if (x0 >= source.Width) { x0 = source.Width - 1; x1 = source.Width - 1; wx = 0; }
                    if (x1 >= source.Width) { x1 = source.Width - 1; }

                    int k00 = y0 * source.Width + x0;
                    int k10 = y0 * source.Width + x1;
                    int k01 = y1 * source.Width + x0;
                    int k11 = y1 * source.Width + x1;

                    float r00 = source.r[k00];
                    float r10 = source.r[k10];
                    float r01 = source.r[k01];
                    float r11 = source.r[k11];

                    float g00 = source.g[k00];
                    float g10 = source.g[k10];
                    float g01 = source.g[k01];
                    float g11 = source.g[k11];

                    float b00 = source.b[k00];
                    float b10 = source.b[k10];
                    float b01 = source.b[k01];
                    float b11 = source.b[k11];

                    float wxa = (float)(1.0 - wx);
                    float wya = (float)(1.0 - wy);
                    float fwx = (float)wx;
                    float fwy = (float)wy;

                    float rInterp = r00 * wxa * wya + r10 * fwx * wya + r01 * wxa * fwy + r11 * fwx * fwy;
                    float gInterp = g00 * wxa * wya + g10 * fwx * wya + g01 * wxa * fwy + g11 * fwx * fwy;
                    float bInterp = b00 * wxa * wya + b10 * fwx * wya + b01 * wxa * fwy + b11 * fwx * fwy;

                    int dstIdx = y * destW + x;
                    int rr = (int)(rInterp + 0.5f);
                    int gg = (int)(gInterp + 0.5f);
                    int bb = (int)(bInterp + 0.5f);
                    if (rr < 0) rr = 0; if (rr > 65535) rr = 65535;
                    if (gg < 0) gg = 0; if (gg > 65535) gg = 65535;
                    if (bb < 0) bb = 0; if (bb > 65535) bb = 65535;
                    result.r[dstIdx] = (ushort)rr;
                    result.g[dstIdx] = (ushort)gg;
                    result.b[dstIdx] = (ushort)bb;

                    if (source.HasAlphaChannel && source.a != null)
                    {
                        float a00 = source.a[k00];
                        float a10 = source.a[k10];
                        float a01 = source.a[k01];
                        float a11 = source.a[k11];
                        float aInterp = a00 * wxa * wya + a10 * fwx * wya + a01 * wxa * fwy + a11 * fwx * fwy;
                        if (float.IsNaN(aInterp) || float.IsInfinity(aInterp)) aInterp = 1f;
                        if (aInterp < 0f) aInterp = 0f; if (aInterp > 1f) aInterp = 1f;
                        result.a![dstIdx] = aInterp;
                    }
                }
            }
            result.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Resize (IPicture)",
                Operator = typeof(Picture16bpp),
                ProcessingFuncStackTrace = new(true),
                Properties = new Dictionary<string, object>
                {
                    { "SourceWidth", source.Width },
                    { "SourceHeight", source.Height },
                    { "TargetWidth", targetWidth },
                    { "TargetHeight", targetHeight },
                    { "PreserveAspect", preserveAspect },
                },
                Elapsed = sw.Elapsed
            });

            return result;
        }

        public IPicture<byte> Resize(IPicture<byte> source, int targetWidth, int targetHeight, bool preserveAspect)
        {
            var sw = Stopwatch.StartNew();
            if (targetWidth == source.Width && targetHeight == source.Height) return source;

            if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentException("targetWidth and targetHeight must be positive");
            if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("Source image has invalid dimensions");

            int destW = targetWidth;
            int destH = targetHeight;

            if (preserveAspect)
            {
                double sx = (double)targetWidth / source.Width;
                double sy = (double)targetHeight / source.Height;
                double s = Math.Min(sx, sy);
                destW = Math.Max(1, (int)(source.Width * s + 0.5));
                destH = Math.Max(1, (int)(source.Height * s + 0.5));
                if (destW == source.Width && destH == source.Height) return source;
            }
            int dstPixels = checked(destW * destH);

            var result = new Picture8bpp(destW, destH)
            {
                r = new byte[dstPixels],
                g = new byte[dstPixels],
                b = new byte[dstPixels],
                a = source.HasAlphaChannel ? new float[dstPixels] : null,
                HasAlphaChannel = source.HasAlphaChannel,
                ProcessStack = source.ProcessStack
            };

            double xRatio = (double)source.Width / destW;
            double yRatio = (double)source.Height / destH;

            for (int y = 0; y < destH; y++)
            {
                double srcY = (y + 0.5) * yRatio - 0.5;
                int y0 = (int)Math.Floor(srcY);
                int y1 = y0 + 1;
                double wy = srcY - y0;
                if (y0 < 0)
                {
                    y0 = 0; y1 = 0; wy = 0;
                }
                else if (y0 >= source.Height) { y0 = source.Height - 1; y1 = source.Height - 1; wy = 0; }
                if (y1 >= source.Height) { y1 = source.Height - 1; }

                for (int x = 0; x < destW; x++)
                {
                    double srcX = (x + 0.5) * xRatio - 0.5;
                    int x0 = (int)Math.Floor(srcX);
                    int x1 = x0 + 1;
                    double wx = srcX - x0;
                    if (x0 < 0)
                    {
                        x0 = 0; x1 = 0; wx = 0;
                    }
                    else if (x0 >= source.Width) { x0 = source.Width - 1; x1 = source.Width - 1; wx = 0; }
                    if (x1 >= source.Width) { x1 = source.Width - 1; }

                    int k00 = y0 * source.Width + x0;
                    int k10 = y0 * source.Width + x1;
                    int k01 = y1 * source.Width + x0;
                    int k11 = y1 * source.Width + x1;

                    float r00 = source.r[k00];
                    float r10 = source.r[k10];
                    float r01 = source.r[k01];
                    float r11 = source.r[k11];

                    float g00 = source.g[k00];
                    float g10 = source.g[k10];
                    float g01 = source.g[k01];
                    float g11 = source.g[k11];

                    float b00 = source.b[k00];
                    float b10 = source.b[k10];
                    float b01 = source.b[k01];
                    float b11 = source.b[k11];

                    float wxa = (float)(1.0 - wx);
                    float wya = (float)(1.0 - wy);
                    float fwx = (float)wx;
                    float fwy = (float)wy;

                    float rInterp = r00 * wxa * wya + r10 * fwx * wya + r01 * wxa * fwy + r11 * fwx * fwy;
                    float gInterp = g00 * wxa * wya + g10 * fwx * wya + g01 * wxa * fwy + g11 * fwx * fwy;
                    float bInterp = b00 * wxa * wya + b10 * fwx * wya + b01 * wxa * fwy + b11 * fwx * fwy;

                    int dstIdx = y * destW + x;
                    int rr = (int)(rInterp + 0.5f);
                    int gg = (int)(gInterp + 0.5f);
                    int bb = (int)(bInterp + 0.5f);
                    if (rr < 0) rr = 0; if (rr > 255) rr = 255;
                    if (gg < 0) gg = 0; if (gg > 255) gg = 255;
                    if (bb < 0) bb = 0; if (bb > 255) bb = 255;
                    result.r[dstIdx] = (byte)rr;
                    result.g[dstIdx] = (byte)gg;
                    result.b[dstIdx] = (byte)bb;

                    if (source.HasAlphaChannel && source.a != null)
                    {
                        float a00 = source.a[k00];
                        float a10 = source.a[k10];
                        float a01 = source.a[k01];
                        float a11 = source.a[k11];
                        float aInterp = a00 * wxa * wya + a10 * fwx * wya + a01 * wxa * fwy + a11 * fwx * fwy;
                        if (float.IsNaN(aInterp) || float.IsInfinity(aInterp)) aInterp = 1f;
                        if (aInterp < 0f) aInterp = 0f; if (aInterp > 1f) aInterp = 1f;
                        result.a![dstIdx] = aInterp;
                    }
                }
            }
            result.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Resize (IPicture)",
                Operator = typeof(Picture8bpp),
                ProcessingFuncStackTrace = new(true),
                Properties = new Dictionary<string, object>
                {
                    { "SourceWidth", source.Width },
                    { "SourceHeight", source.Height },
                    { "TargetWidth", targetWidth },
                    { "TargetHeight", targetHeight },
                    { "PreserveAspect", preserveAspect },
                },
                Elapsed = sw.Elapsed
            });
            return result;
        }

        public IHDRPicture<ushort> Resize(IHDRPicture<ushort> source, int targetWidth, int targetHeight, bool preserveAspect)
        {
            var sw = Stopwatch.StartNew();
            if (targetWidth == source.Width && targetHeight == source.Height) return source;

            if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentException("targetWidth and targetHeight must be positive");
            if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("Source image has invalid dimensions");

            int destW = targetWidth;
            int destH = targetHeight;

            if (preserveAspect)
            {
                double sx = (double)targetWidth / source.Width;
                double sy = (double)targetHeight / source.Height;
                double s = Math.Min(sx, sy);
                destW = Math.Max(1, (int)(source.Width * s + 0.5));
                destH = Math.Max(1, (int)(source.Height * s + 0.5));
                if (destW == source.Width && destH == source.Height) return source;
            }

            int dstPixels = checked(destW * destH);
            var result = new HDRPicture16bpp(destW, destH, allocateArrays: false)
            {
                r = new ushort[dstPixels],
                g = new ushort[dstPixels],
                b = new ushort[dstPixels],
                a = source.HasAlphaChannel ? new float[dstPixels] : null,
                HasAlphaChannel = source.HasAlphaChannel,
                Brightness = new float[dstPixels],
                MaximumBrightness = (source.MaximumBrightness > 0f && float.IsFinite(source.MaximumBrightness))
                    ? source.MaximumBrightness
                    : 1000f,
                ProcessStack = source.ProcessStack
            };

            float[]? sourceBrightness = (source.Brightness != null && source.Brightness.Length == source.Pixels) ? source.Brightness : null;
            bool hasBrightness = sourceBrightness != null;

            double xRatio = (double)source.Width / destW;
            double yRatio = (double)source.Height / destH;

            for (int y = 0; y < destH; y++)
            {
                double srcY = (y + 0.5) * yRatio - 0.5;
                int y0 = (int)Math.Floor(srcY);
                int y1 = y0 + 1;
                double wy = srcY - y0;
                if (y0 < 0)
                {
                    y0 = 0; y1 = 0; wy = 0;
                }
                else if (y0 >= source.Height) { y0 = source.Height - 1; y1 = source.Height - 1; wy = 0; }
                if (y1 >= source.Height) { y1 = source.Height - 1; }

                for (int x = 0; x < destW; x++)
                {
                    double srcX = (x + 0.5) * xRatio - 0.5;
                    int x0 = (int)Math.Floor(srcX);
                    int x1 = x0 + 1;
                    double wx = srcX - x0;
                    if (x0 < 0)
                    {
                        x0 = 0; x1 = 0; wx = 0;
                    }
                    else if (x0 >= source.Width) { x0 = source.Width - 1; x1 = source.Width - 1; wx = 0; }
                    if (x1 >= source.Width) { x1 = source.Width - 1; }

                    int k00 = y0 * source.Width + x0;
                    int k10 = y0 * source.Width + x1;
                    int k01 = y1 * source.Width + x0;
                    int k11 = y1 * source.Width + x1;

                    float r00 = source.r[k00];
                    float r10 = source.r[k10];
                    float r01 = source.r[k01];
                    float r11 = source.r[k11];

                    float g00 = source.g[k00];
                    float g10 = source.g[k10];
                    float g01 = source.g[k01];
                    float g11 = source.g[k11];

                    float b00 = source.b[k00];
                    float b10 = source.b[k10];
                    float b01 = source.b[k01];
                    float b11 = source.b[k11];

                    float wxa = (float)(1.0 - wx);
                    float wya = (float)(1.0 - wy);
                    float fwx = (float)wx;
                    float fwy = (float)wy;

                    float rInterp = r00 * wxa * wya + r10 * fwx * wya + r01 * wxa * fwy + r11 * fwx * fwy;
                    float gInterp = g00 * wxa * wya + g10 * fwx * wya + g01 * wxa * fwy + g11 * fwx * fwy;
                    float bInterp = b00 * wxa * wya + b10 * fwx * wya + b01 * wxa * fwy + b11 * fwx * fwy;

                    int dstIdx = y * destW + x;
                    int rr = (int)(rInterp + 0.5f);
                    int gg = (int)(gInterp + 0.5f);
                    int bb = (int)(bInterp + 0.5f);
                    if (rr < 0) rr = 0; if (rr > 65535) rr = 65535;
                    if (gg < 0) gg = 0; if (gg > 65535) gg = 65535;
                    if (bb < 0) bb = 0; if (bb > 65535) bb = 65535;
                    result.r[dstIdx] = (ushort)rr;
                    result.g[dstIdx] = (ushort)gg;
                    result.b[dstIdx] = (ushort)bb;

                    if (source.HasAlphaChannel && source.a != null)
                    {
                        float a00 = source.a[k00];
                        float a10 = source.a[k10];
                        float a01 = source.a[k01];
                        float a11 = source.a[k11];
                        float aInterp = a00 * wxa * wya + a10 * fwx * wya + a01 * wxa * fwy + a11 * fwx * fwy;
                        if (float.IsNaN(aInterp) || float.IsInfinity(aInterp)) aInterp = 1f;
                        if (aInterp < 0f) aInterp = 0f; if (aInterp > 1f) aInterp = 1f;
                        result.a![dstIdx] = aInterp;
                    }

                    float br00, br10, br01, br11;
                    if (sourceBrightness != null)
                    {
                        br00 = sourceBrightness[k00];
                        br10 = sourceBrightness[k10];
                        br01 = sourceBrightness[k01];
                        br11 = sourceBrightness[k11];
                    }
                    else
                    {
                        br00 = Math.Clamp((0.2627f * r00 + 0.6780f * g00 + 0.0593f * b00) / 65535f, 0f, 1f);
                        br10 = Math.Clamp((0.2627f * r10 + 0.6780f * g10 + 0.0593f * b10) / 65535f, 0f, 1f);
                        br01 = Math.Clamp((0.2627f * r01 + 0.6780f * g01 + 0.0593f * b01) / 65535f, 0f, 1f);
                        br11 = Math.Clamp((0.2627f * r11 + 0.6780f * g11 + 0.0593f * b11) / 65535f, 0f, 1f);
                    }

                    float brightnessInterp = br00 * wxa * wya + br10 * fwx * wya + br01 * wxa * fwy + br11 * fwx * fwy;
                    if (float.IsNaN(brightnessInterp) || float.IsInfinity(brightnessInterp)) brightnessInterp = 0f;
                    result.Brightness[dstIdx] = Math.Clamp(brightnessInterp, 0f, 1f);
                }
            }

            result.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Resize (HDR IPicture)",
                Operator = typeof(HDRPicture16bpp),
                ProcessingFuncStackTrace = new(true),
                Properties = new Dictionary<string, object>
                {
                    { "SourceWidth", source.Width },
                    { "SourceHeight", source.Height },
                    { "TargetWidth", targetWidth },
                    { "TargetHeight", targetHeight },
                    { "PreserveAspect", preserveAspect },
                    { "MaximumBrightness", result.MaximumBrightness },
                    { "HasBrightnessChannel", hasBrightness },
                },
                Elapsed = sw.Elapsed
            });

            return result;
        }
    }
}
