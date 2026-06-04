using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class ThresholdEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
    {
        ArgumentNullException.ThrowIfNull(picture);
        float t = Math.Clamp(threshold, 0f, 1f);
        byte low = (byte)Math.Clamp((int)(lowValue * 255f + 0.5f), 0, 255);
        byte high = (byte)Math.Clamp((int)(highValue * 255f + 0.5f), 0, 255);

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
            float luminance = (picture.r[i] * 0.2126f + picture.g[i] * 0.7152f + picture.b[i] * 0.0722f) / 255f;
            byte val = luminance >= t ? high : low;
            result.r[i] = val;
            result.g[i] = val;
            result.b[i] = val;
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Threshold",
            Operator = typeof(ThresholdEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Threshold", t },
                { "LowValue", lowValue },
                { "HighValue", highValue },
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
    {
        ArgumentNullException.ThrowIfNull(picture);
        float t = Math.Clamp(threshold, 0f, 1f);
        ushort low = (ushort)Math.Clamp((int)(lowValue * 65535f + 0.5f), 0, 65535);
        ushort high = (ushort)Math.Clamp((int)(highValue * 65535f + 0.5f), 0, 65535);

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
            float luminance = (picture.r[i] * 0.2126f + picture.g[i] * 0.7152f + picture.b[i] * 0.0722f) / max16;
            ushort val = luminance >= t ? high : low;
            result.r[i] = val;
            result.g[i] = val;
            result.b[i] = val;
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Threshold",
            Operator = typeof(ThresholdEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Threshold", t },
                { "LowValue", lowValue },
                { "HighValue", highValue },
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
    {
        ArgumentNullException.ThrowIfNull(picture);
        float t = Math.Clamp(threshold, 0f, 1f);
        ushort low = (ushort)Math.Clamp((int)(lowValue * 65535f + 0.5f), 0, 65535);
        ushort high = (ushort)Math.Clamp((int)(highValue * 65535f + 0.5f), 0, 65535);

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
            float luminance = (picture.r[i] * 0.2126f + picture.g[i] * 0.7152f + picture.b[i] * 0.0722f) / max16;
            ushort val = luminance >= t ? high : low;
            result.r[i] = val;
            result.g[i] = val;
            result.b[i] = val;
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Threshold",
            Operator = typeof(ThresholdEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Threshold", t },
                { "LowValue", lowValue },
                { "HighValue", highValue },
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Threshold(float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
            => context.SetAndReturn(Process(context.Result, threshold, lowValue, highValue));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Threshold(float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
            => context.SetAndReturn(Process(context.Result, threshold, lowValue, highValue));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Threshold(float threshold = 0.5f, float lowValue = 0f, float highValue = 1f)
            => context.SetAndReturn(Process(context.Result, threshold, lowValue, highValue));
    }
}
