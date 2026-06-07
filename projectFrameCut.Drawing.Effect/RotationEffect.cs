using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class RotationEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float angleDegrees, bool expandCanvas = true)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();

        int srcW = picture.Width, srcH = picture.Height;
        float angleRad = angleDegrees * MathF.PI / 180f;
        float cosA = MathF.Cos(angleRad);
        float sinA = MathF.Sin(angleRad);

        int dstW, dstH;
        float cx, cy; // rotation center in output

        if (expandCanvas)
        {
            dstW = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * cosA) + MathF.Abs(srcH * sinA)));
            dstH = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * sinA) + MathF.Abs(srcH * cosA)));
            cx = dstW / 2f;
            cy = dstH / 2f;
        }
        else
        {
            dstW = srcW;
            dstH = srcH;
            cx = srcW / 2f;
            cy = srcH / 2f;
        }

        int pixels = dstW * dstH;
        float srcCenterX = srcW / 2f;
        float srcCenterY = srcH / 2f;

        IPicture<byte> result = new Picture8bpp(dstW, dstH)
        {
            r = GC.AllocateUninitializedArray<byte>(pixels),
            g = GC.AllocateUninitializedArray<byte>(pixels),
            b = GC.AllocateUninitializedArray<byte>(pixels),
            a = picture.HasAlphaChannel && picture.a != null
                ? GC.AllocateUninitializedArray<float>(pixels) : null,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        Array.Fill(result.r, (byte)0);
        Array.Fill(result.g, (byte)0);
        Array.Fill(result.b, (byte)0);
        if (result.a != null) Array.Fill(result.a, 0f);

        for (int oy = 0; oy < dstH; oy++)
        {
            int dstRowStart = oy * dstW;
            for (int ox = 0; ox < dstW; ox++)
            {
                float tx = ox - cx;
                float ty = oy - cy;

                float sx = tx * cosA - ty * sinA + srcCenterX;
                float sy = tx * sinA + ty * cosA + srcCenterY;

                if (sx < -0.5f || sx >= srcW + 0.5f || sy < -0.5f || sy >= srcH + 0.5f)
                    continue; // leave as 0 (transparent black)

                int idx = dstRowStart + ox;
                ReadBilinear(picture, sx, sy, out byte rr, out byte gg, out byte bb);
                result.r[idx] = rr;
                result.g[idx] = gg;
                result.b[idx] = bb;
            }
        }

        if (result.a != null && picture.a != null)
        {
            for (int oy = 0; oy < dstH; oy++)
            {
                int dstRowStart = oy * dstW;
                for (int ox = 0; ox < dstW; ox++)
                {
                    float tx = ox - cx;
                    float ty = oy - cy;

                    float sx = tx * cosA - ty * sinA + srcCenterX;
                    float sy = tx * sinA + ty * cosA + srcCenterY;

                    if (sx < -0.5f || sx >= srcW + 0.5f || sy < -0.5f || sy >= srcH + 0.5f)
                        continue;

                    result.a[dstRowStart + ox] = ReadBilinearAlpha(picture, sx, sy);
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Rotation",
            Operator = typeof(RotationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "AngleDegrees", angleDegrees },
                { "ExpandCanvas", expandCanvas },
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float angleDegrees, bool expandCanvas = true)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();

        int srcW = picture.Width, srcH = picture.Height;
        float angleRad = angleDegrees * MathF.PI / 180f;
        float cosA = MathF.Cos(angleRad);
        float sinA = MathF.Sin(angleRad);

        int dstW, dstH;
        float cx, cy;

        if (expandCanvas)
        {
            dstW = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * cosA) + MathF.Abs(srcH * sinA)));
            dstH = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * sinA) + MathF.Abs(srcH * cosA)));
            cx = dstW / 2f;
            cy = dstH / 2f;
        }
        else
        {
            dstW = srcW;
            dstH = srcH;
            cx = srcW / 2f;
            cy = srcH / 2f;
        }

        int pixels = dstW * dstH;
        float srcCenterX = srcW / 2f;
        float srcCenterY = srcH / 2f;

        IPicture<ushort> result = new Picture16bpp(dstW, dstH)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = picture.HasAlphaChannel && picture.a != null
                ? GC.AllocateUninitializedArray<float>(pixels) : null,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        Array.Fill(result.r, (ushort)0);
        Array.Fill(result.g, (ushort)0);
        Array.Fill(result.b, (ushort)0);
        if (result.a != null) Array.Fill(result.a, 0f);

        for (int oy = 0; oy < dstH; oy++)
        {
            int dstRowStart = oy * dstW;
            for (int ox = 0; ox < dstW; ox++)
            {
                float tx = ox - cx;
                float ty = oy - cy;

                float sx = tx * cosA - ty * sinA + srcCenterX;
                float sy = tx * sinA + ty * cosA + srcCenterY;

                if (sx < -0.5f || sx >= srcW + 0.5f || sy < -0.5f || sy >= srcH + 0.5f)
                    continue;

                int idx = dstRowStart + ox;
                ReadBilinear(picture, sx, sy, out ushort rr, out ushort gg, out ushort bb);
                result.r[idx] = rr;
                result.g[idx] = gg;
                result.b[idx] = bb;
            }
        }

        if (result.a != null && picture.a != null)
        {
            for (int oy = 0; oy < dstH; oy++)
            {
                int dstRowStart = oy * dstW;
                for (int ox = 0; ox < dstW; ox++)
                {
                    float tx = ox - cx;
                    float ty = oy - cy;

                    float sx = tx * cosA - ty * sinA + srcCenterX;
                    float sy = tx * sinA + ty * cosA + srcCenterY;

                    if (sx < -0.5f || sx >= srcW + 0.5f || sy < -0.5f || sy >= srcH + 0.5f)
                        continue;

                    result.a[dstRowStart + ox] = ReadBilinearAlpha(picture, sx, sy);
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Rotation",
            Operator = typeof(RotationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "AngleDegrees", angleDegrees },
                { "ExpandCanvas", expandCanvas },
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture Process(IPicture source, float angleDegrees, bool expandCanvas = true)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, angleDegrees, expandCanvas),
            IPicture<ushort> p16 => Process(p16, angleDegrees, expandCanvas),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    private static void ReadBilinear(IPicture<byte> src, float x, float y, out byte r, out byte g, out byte b)
    {
        int x0 = (int)MathF.Floor(x); if (x < 0f) x0--;
        int y0 = (int)MathF.Floor(y); if (y < 0f) y0--;
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = x - x0;
        float fy = y - y0;

        int w = src.Width, h = src.Height;

        if (x0 < 0) x0 = 0; if (x1 >= w) x1 = w - 1;
        if (y0 < 0) y0 = 0; if (y1 >= h) y1 = h - 1;

        // All 4 corners valid
        int i00 = y0 * w + x0;
        int i10 = y0 * w + x1;
        int i01 = y1 * w + x0;
        int i11 = y1 * w + x1;

        float w00 = (1f - fx) * (1f - fy);
        float w10 = fx * (1f - fy);
        float w01 = (1f - fx) * fy;
        float w11 = fx * fy;

        r = (byte)Math.Clamp((int)(src.r[i00] * w00 + src.r[i10] * w10 + src.r[i01] * w01 + src.r[i11] * w11 + 0.5f), 0, 255);
        g = (byte)Math.Clamp((int)(src.g[i00] * w00 + src.g[i10] * w10 + src.g[i01] * w01 + src.g[i11] * w11 + 0.5f), 0, 255);
        b = (byte)Math.Clamp((int)(src.b[i00] * w00 + src.b[i10] * w10 + src.b[i01] * w01 + src.b[i11] * w11 + 0.5f), 0, 255);
    }

    private static void ReadBilinear(IPicture<ushort> src, float x, float y, out ushort r, out ushort g, out ushort b)
    {
        int x0 = (int)MathF.Floor(x); if (x < 0f) x0--;
        int y0 = (int)MathF.Floor(y); if (y < 0f) y0--;
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = x - x0;
        float fy = y - y0;

        int w = src.Width, h = src.Height;

        if (x0 < 0) x0 = 0; if (x1 >= w) x1 = w - 1;
        if (y0 < 0) y0 = 0; if (y1 >= h) y1 = h - 1;

        int i00 = y0 * w + x0;
        int i10 = y0 * w + x1;
        int i01 = y1 * w + x0;
        int i11 = y1 * w + x1;

        float w00 = (1f - fx) * (1f - fy);
        float w10 = fx * (1f - fy);
        float w01 = (1f - fx) * fy;
        float w11 = fx * fy;

        r = (ushort)Math.Clamp((int)(src.r[i00] * w00 + src.r[i10] * w10 + src.r[i01] * w01 + src.r[i11] * w11 + 0.5f), 0, 65535);
        g = (ushort)Math.Clamp((int)(src.g[i00] * w00 + src.g[i10] * w10 + src.g[i01] * w01 + src.g[i11] * w11 + 0.5f), 0, 65535);
        b = (ushort)Math.Clamp((int)(src.b[i00] * w00 + src.b[i10] * w10 + src.b[i01] * w01 + src.b[i11] * w11 + 0.5f), 0, 65535);
    }

    private static float ReadBilinearAlpha(IPicture<byte> src, float x, float y)
    {
        if (src.a == null) return 1f;

        int x0 = (int)MathF.Floor(x); if (x < 0f) x0--;
        int y0 = (int)MathF.Floor(y); if (y < 0f) y0--;
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = x - x0;
        float fy = y - y0;

        int w = src.Width, h = src.Height;

        if (x0 < 0) x0 = 0; if (x1 >= w) x1 = w - 1;
        if (y0 < 0) y0 = 0; if (y1 >= h) y1 = h - 1;

        int i00 = y0 * w + x0;
        int i10 = y0 * w + x1;
        int i01 = y1 * w + x0;
        int i11 = y1 * w + x1;

        float w00 = (1f - fx) * (1f - fy);
        float w10 = fx * (1f - fy);
        float w01 = (1f - fx) * fy;
        float w11 = fx * fy;

        return src.a[i00] * w00 + src.a[i10] * w10 + src.a[i01] * w01 + src.a[i11] * w11;
    }

    private static float ReadBilinearAlpha(IPicture<ushort> src, float x, float y)
    {
        if (src.a == null) return 1f;

        int x0 = (int)MathF.Floor(x); if (x < 0f) x0--;
        int y0 = (int)MathF.Floor(y); if (y < 0f) y0--;
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = x - x0;
        float fy = y - y0;

        int w = src.Width, h = src.Height;

        if (x0 < 0) x0 = 0; if (x1 >= w) x1 = w - 1;
        if (y0 < 0) y0 = 0; if (y1 >= h) y1 = h - 1;

        int i00 = y0 * w + x0;
        int i10 = y0 * w + x1;
        int i01 = y1 * w + x0;
        int i11 = y1 * w + x1;

        float w00 = (1f - fx) * (1f - fy);
        float w10 = fx * (1f - fy);
        float w01 = (1f - fx) * fy;
        float w11 = fx * fy;

        return src.a[i00] * w00 + src.a[i10] * w10 + src.a[i01] * w01 + src.a[i11] * w11;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Rotate(float angleDegrees, bool expandCanvas = true)
            => context.SetAndReturn(Process(context.Result, angleDegrees, expandCanvas));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Rotate(float angleDegrees, bool expandCanvas = true)
            => context.SetAndReturn(Process(context.Result, angleDegrees, expandCanvas));
    }
}
