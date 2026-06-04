using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class GammaEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float gamma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (gamma <= 0f || !float.IsFinite(gamma))
            return picture;

        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new Picture8bpp(width, height)
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

        float invGamma = 1f / gamma;
        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (byte)Math.Clamp((int)(MathF.Pow(picture.r[i] / 255f, invGamma) * 255f + 0.5f), 0, 255);
            result.g[i] = (byte)Math.Clamp((int)(MathF.Pow(picture.g[i] / 255f, invGamma) * 255f + 0.5f), 0, 255);
            result.b[i] = (byte)Math.Clamp((int)(MathF.Pow(picture.b[i] / 255f, invGamma) * 255f + 0.5f), 0, 255);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Gamma",
            Operator = typeof(GammaEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Gamma", gamma } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float gamma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (gamma <= 0f || !float.IsFinite(gamma))
            return picture;

        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new Picture16bpp(width, height)
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

        const float max16 = 65535f;
        float invGamma = 1f / gamma;
        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.r[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.g[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.b[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Gamma",
            Operator = typeof(GammaEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Gamma", gamma } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float gamma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (gamma <= 0f || !float.IsFinite(gamma))
            return picture;

        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new HDRPicture16bpp(width, height)
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

        const float max16 = 65535f;
        float invGamma = 1f / gamma;
        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.r[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.g[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(MathF.Pow(picture.b[i] / max16, invGamma) * max16 + 0.5f), 0, 65535);
        }

        // Apply gamma correction to HDR Brightness channel
        if (picture.Brightness != null && picture.Brightness.Length == pixels)
        {
            for (int i = 0; i < pixels; i++)
                result.Brightness[i] = MathF.Pow(Math.Clamp(picture.Brightness[i], 0f, 1f), invGamma);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Gamma",
            Operator = typeof(GammaEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Gamma", gamma } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Gamma(float gamma)
            => context.SetAndReturn(Process(context.Result, gamma));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Gamma(float gamma)
            => context.SetAndReturn(Process(context.Result, gamma));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Gamma(float gamma)
            => context.SetAndReturn(Process(context.Result, gamma));
    }
}
