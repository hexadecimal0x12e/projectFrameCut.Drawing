using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Adjusts the opacity (alpha channel) of a picture.</summary>
public static class OpacityEffect
{
    /// <summary>Apply the opacity effect to an 8-bit picture.</summary>
    /// <param name="opacity">Opacity value (0.0 = fully transparent, 1.0 = fully opaque).</param>
    public static IPicture<byte> Process(IPicture<byte> picture, float opacity)
    {
        ArgumentNullException.ThrowIfNull(picture);
        opacity = Math.Clamp(opacity, 0f, 1f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new Picture8bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<byte>(pixels),
            g = GC.AllocateUninitializedArray<byte>(pixels),
            b = GC.AllocateUninitializedArray<byte>(pixels),
            a = new float[pixels],
            HasAlphaChannel = true,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };
        Array.Copy(picture.r, result.r, pixels);
        Array.Copy(picture.g, result.g, pixels);
        Array.Copy(picture.b, result.b, pixels);

        if (picture.a != null)
        {
            for (int i = 0; i < pixels; i++)
                result.a[i] = Math.Clamp(picture.a[i] * opacity, 0f, 1f);
        }
        else
        {
            Array.Fill(result.a!, opacity);
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Opacity",
            Operator = typeof(OpacityEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Opacity", opacity } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the opacity effect to a 16-bit picture.</summary>
    /// <param name="opacity">Opacity value (0.0 = fully transparent, 1.0 = fully opaque).</param>
    public static IPicture<ushort> Process(IPicture<ushort> picture, float opacity)
    {
        ArgumentNullException.ThrowIfNull(picture);
        opacity = Math.Clamp(opacity, 0f, 1f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new Picture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = new float[pixels],
            HasAlphaChannel = true,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };
        Array.Copy(picture.r, result.r, pixels);
        Array.Copy(picture.g, result.g, pixels);
        Array.Copy(picture.b, result.b, pixels);

        if (picture.a != null)
        {
            for (int i = 0; i < pixels; i++)
                result.a[i] = Math.Clamp(picture.a[i] * opacity, 0f, 1f);
        }
        else
        {
            Array.Fill(result.a!, opacity);
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Opacity",
            Operator = typeof(OpacityEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Opacity", opacity } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the opacity effect to an HDR picture.</summary>
    /// <param name="opacity">Opacity value (0.0 = fully transparent, 1.0 = fully opaque).</param>
    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float opacity)
    {
        ArgumentNullException.ThrowIfNull(picture);
        opacity = Math.Clamp(opacity, 0f, 1f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        var result = new HDRPicture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = new float[pixels],
            HasAlphaChannel = true,
            Brightness = GC.AllocateUninitializedArray<float>(pixels),
            MaximumBrightness = picture.MaximumBrightness > 0f && float.IsFinite(picture.MaximumBrightness)
                ? picture.MaximumBrightness : 1000f,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };
        Array.Copy(picture.r, result.r, pixels);
        Array.Copy(picture.g, result.g, pixels);
        Array.Copy(picture.b, result.b, pixels);

        if (picture.a != null)
        {
            for (int i = 0; i < pixels; i++)
                result.a[i] = Math.Clamp(picture.a[i] * opacity, 0f, 1f);
        }
        else
        {
            Array.Fill(result.a!, opacity);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness!, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Opacity",
            Operator = typeof(OpacityEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Opacity", opacity } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the opacity effect using runtime type dispatch.</summary>
    /// <param name="opacity">Opacity value (0.0 = fully transparent, 1.0 = fully opaque).</param>
    public static IPicture Process(IPicture source, float opacity)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, opacity),
            IPicture<ushort> p16 when p16 is IHDRPicture<ushort> hdr => Process(hdr, opacity),
            IPicture<ushort> p16 => Process(p16, opacity),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the opacity effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Opacity(float opacity)
            => context.SetAndReturn(Process(context.Result, opacity));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the opacity effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Opacity(float opacity)
            => context.SetAndReturn(Process(context.Result, opacity));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        /// <summary>Apply the opacity effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IHDRPicture<ushort>> Opacity(float opacity)
            => context.SetAndReturn(Process(context.Result, opacity));
    }
}
