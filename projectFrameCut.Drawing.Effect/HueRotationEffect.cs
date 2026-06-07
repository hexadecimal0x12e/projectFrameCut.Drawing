using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class HueRotationEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        IPicture<byte> result = new Picture8bpp(width, height)
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

        float angleRad = angleDegrees * MathF.PI / 180f;

        for (int i = 0; i < pixels; i++)
        {
            float r = picture.r[i] / 255f;
            float g = picture.g[i] / 255f;
            float b = picture.b[i] / 255f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0f, s = 0f, l = (max + min) / 2f;

            if (delta > 0.0001f)
            {
                s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

                if (max == r)
                    h = (g - b) / delta + (g < b ? 6f : 0f);
                else if (max == g)
                    h = (b - r) / delta + 2f;
                else
                    h = (r - g) / delta + 4f;

                h /= 6f;
            }

            h = (h + angleDegrees / 360f) % 1f;
            if (h < 0f) h += 1f;

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            float rc = HueToRgb(p, q, h + 1f / 3f);
            float gc = HueToRgb(p, q, h);
            float bc = HueToRgb(p, q, h - 1f / 3f);

            result.r[i] = (byte)Math.Clamp((int)(rc * 255f + 0.5f), 0, 255);
            result.g[i] = (byte)Math.Clamp((int)(gc * 255f + 0.5f), 0, 255);
            result.b[i] = (byte)Math.Clamp((int)(bc * 255f + 0.5f), 0, 255);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "HueRotation",
            Operator = typeof(HueRotationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "AngleDegrees", angleDegrees } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        IPicture<ushort> result = new Picture16bpp(width, height)
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

        for (int i = 0; i < pixels; i++)
        {
            float r = picture.r[i] / 65535f;
            float g = picture.g[i] / 65535f;
            float b = picture.b[i] / 65535f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0f, s = 0f, l = (max + min) / 2f;

            if (delta > 0.0001f)
            {
                s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

                if (max == r)
                    h = (g - b) / delta + (g < b ? 6f : 0f);
                else if (max == g)
                    h = (b - r) / delta + 2f;
                else
                    h = (r - g) / delta + 4f;

                h /= 6f;
            }

            h = (h + angleDegrees / 360f) % 1f;
            if (h < 0f) h += 1f;

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            float rc = HueToRgb(p, q, h + 1f / 3f);
            float gc = HueToRgb(p, q, h);
            float bc = HueToRgb(p, q, h - 1f / 3f);

            result.r[i] = (ushort)Math.Clamp((int)(rc * 65535f + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(gc * 65535f + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(bc * 65535f + 0.5f), 0, 65535);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "HueRotation",
            Operator = typeof(HueRotationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "AngleDegrees", angleDegrees } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        IHDRPicture<ushort> result = new HDRPicture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = picture.HasAlphaChannel && picture.a != null
                ? GC.AllocateUninitializedArray<float>(pixels) : null,
            HasAlphaChannel = picture.HasAlphaChannel,
            Brightness = GC.AllocateUninitializedArray<float>(pixels),
            MaximumBrightness = picture.MaximumBrightness > 0f && float.IsFinite(picture.MaximumBrightness)
                ? picture.MaximumBrightness : 1000f,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int i = 0; i < pixels; i++)
        {
            float r = picture.r[i] / 65535f;
            float g = picture.g[i] / 65535f;
            float b = picture.b[i] / 65535f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0f, s = 0f, l = (max + min) / 2f;

            if (delta > 0.0001f)
            {
                s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

                if (max == r)
                    h = (g - b) / delta + (g < b ? 6f : 0f);
                else if (max == g)
                    h = (b - r) / delta + 2f;
                else
                    h = (r - g) / delta + 4f;

                h /= 6f;
            }

            h = (h + angleDegrees / 360f) % 1f;
            if (h < 0f) h += 1f;

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            result.r[i] = (ushort)Math.Clamp((int)(HueToRgb(p, q, h + 1f / 3f) * 65535f + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(HueToRgb(p, q, h) * 65535f + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(HueToRgb(p, q, h - 1f / 3f) * 65535f + 0.5f), 0, 65535);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "HueRotation",
            Operator = typeof(HueRotationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "AngleDegrees", angleDegrees } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> HueRotation(float angleDegrees)
            => context.SetAndReturn(Process(context.Result, angleDegrees));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> HueRotation(float angleDegrees)
            => context.SetAndReturn(Process(context.Result, angleDegrees));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> HueRotation(float angleDegrees)
            => context.SetAndReturn(Process(context.Result, angleDegrees));
    }
}
