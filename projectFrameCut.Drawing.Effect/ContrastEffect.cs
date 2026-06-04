using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class ContrastEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float factor)
    {
        ArgumentNullException.ThrowIfNull(picture);
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

        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (byte)Math.Clamp((int)(((picture.r[i] / 255f - 0.5f) * factor + 0.5f) * 255f + 0.5f), 0, 255);
            result.g[i] = (byte)Math.Clamp((int)(((picture.g[i] / 255f - 0.5f) * factor + 0.5f) * 255f + 0.5f), 0, 255);
            result.b[i] = (byte)Math.Clamp((int)(((picture.b[i] / 255f - 0.5f) * factor + 0.5f) * 255f + 0.5f), 0, 255);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Contrast",
            Operator = typeof(ContrastEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float factor)
    {
        ArgumentNullException.ThrowIfNull(picture);
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
        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)(((picture.r[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(((picture.g[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(((picture.b[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Contrast",
            Operator = typeof(ContrastEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float factor)
    {
        ArgumentNullException.ThrowIfNull(picture);
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
        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)(((picture.r[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(((picture.g[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(((picture.b[i] / max16 - 0.5f) * factor + 0.5f) * max16 + 0.5f), 0, 65535);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Contrast",
            Operator = typeof(ContrastEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Contrast(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Contrast(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Contrast(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
}
