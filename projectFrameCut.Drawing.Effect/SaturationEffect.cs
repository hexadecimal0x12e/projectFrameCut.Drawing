using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Adjusts the color saturation of a picture.</summary>
public static class SaturationEffect
{
    private const float RWeight = 0.2126f;
    private const float GWeight = 0.7152f;
    private const float BWeight = 0.0722f;

    /// <summary>Apply the saturation effect to an 8-bit picture.</summary>
    /// <param name="factor">Saturation multiplier. 0.0 = grayscale, 1.0 = original, greater than 1 = oversaturated.</param>
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
            float gray = picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight;
            result.r[i] = (byte)Math.Clamp((int)(gray + factor * (picture.r[i] - gray) + 0.5f), 0, 255);
            result.g[i] = (byte)Math.Clamp((int)(gray + factor * (picture.g[i] - gray) + 0.5f), 0, 255);
            result.b[i] = (byte)Math.Clamp((int)(gray + factor * (picture.b[i] - gray) + 0.5f), 0, 255);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Saturation",
            Operator = typeof(SaturationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the saturation effect to a 16-bit picture.</summary>
    /// <param name="factor">Saturation multiplier. 0.0 = grayscale, 1.0 = original, greater than 1 = oversaturated.</param>
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

        for (int i = 0; i < pixels; i++)
        {
            float gray = picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight;
            result.r[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.r[i] - gray) + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.g[i] - gray) + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.b[i] - gray) + 0.5f), 0, 65535);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Saturation",
            Operator = typeof(SaturationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the saturation effect to an HDR picture.</summary>
    /// <param name="factor">Saturation multiplier. 0.0 = grayscale, 1.0 = original, greater than 1 = oversaturated.</param>
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

        for (int i = 0; i < pixels; i++)
        {
            float gray = picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight;
            result.r[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.r[i] - gray) + 0.5f), 0, 65535);
            result.g[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.g[i] - gray) + 0.5f), 0, 65535);
            result.b[i] = (ushort)Math.Clamp((int)(gray + factor * (picture.b[i] - gray) + 0.5f), 0, 65535);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Saturation",
            Operator = typeof(SaturationEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the saturation effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Saturation(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the saturation effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Saturation(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        /// <summary>Apply the saturation effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IHDRPicture<ushort>> Saturation(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
}
