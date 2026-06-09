using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Adjusts the brightness of a picture.</summary>
public static class BrightnessEffect
{
    /// <summary>Apply the brightness effect to an 8-bit picture.</summary>
    /// <param name="factor">Brightness adjustment. Positive values brighten, negative values darken.</param>
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

        if (factor >= 0f)
        {
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (byte)Math.Clamp((int)(picture.r[i] + (255 - picture.r[i]) * factor), 0, 255);
                result.g[i] = (byte)Math.Clamp((int)(picture.g[i] + (255 - picture.g[i]) * factor), 0, 255);
                result.b[i] = (byte)Math.Clamp((int)(picture.b[i] + (255 - picture.b[i]) * factor), 0, 255);
            }
        }
        else
        {
            float scale = 1f + factor;
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (byte)(picture.r[i] * scale);
                result.g[i] = (byte)(picture.g[i] * scale);
                result.b[i] = (byte)(picture.b[i] * scale);
            }
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Brightness",
            Operator = typeof(BrightnessEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the brightness effect to a 16-bit picture.</summary>
    /// <param name="factor">Brightness adjustment. Positive values brighten, negative values darken.</param>
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

        const int max16 = 65535;
        if (factor >= 0f)
        {
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (ushort)Math.Clamp((int)(picture.r[i] + (max16 - picture.r[i]) * factor), 0, max16);
                result.g[i] = (ushort)Math.Clamp((int)(picture.g[i] + (max16 - picture.g[i]) * factor), 0, max16);
                result.b[i] = (ushort)Math.Clamp((int)(picture.b[i] + (max16 - picture.b[i]) * factor), 0, max16);
            }
        }
        else
        {
            float scale = 1f + factor;
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (ushort)(picture.r[i] * scale);
                result.g[i] = (ushort)(picture.g[i] * scale);
                result.b[i] = (ushort)(picture.b[i] * scale);
            }
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Brightness",
            Operator = typeof(BrightnessEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the brightness effect to an HDR picture.</summary>
    /// <param name="factor">Brightness adjustment. Positive values brighten, negative values darken.</param>
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

        const int max16 = 65535;
        if (factor >= 0f)
        {
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (ushort)Math.Clamp((int)(picture.r[i] + (max16 - picture.r[i]) * factor), 0, max16);
                result.g[i] = (ushort)Math.Clamp((int)(picture.g[i] + (max16 - picture.g[i]) * factor), 0, max16);
                result.b[i] = (ushort)Math.Clamp((int)(picture.b[i] + (max16 - picture.b[i]) * factor), 0, max16);
            }
        }
        else
        {
            float scale = 1f + factor;
            for (int i = 0; i < pixels; i++)
            {
                result.r[i] = (ushort)(picture.r[i] * scale);
                result.g[i] = (ushort)(picture.g[i] * scale);
                result.b[i] = (ushort)(picture.b[i] * scale);
            }
        }

        // Apply brightness adjustment to HDR Brightness channel
        if (picture.Brightness != null && picture.Brightness.Length == pixels)
        {
            if (factor >= 0f)
            {
                for (int i = 0; i < pixels; i++)
                    result.Brightness[i] = Math.Clamp(picture.Brightness[i] + (1f - picture.Brightness[i]) * factor, 0f, 1f);
            }
            else
            {
                float scale = 1f + factor;
                for (int i = 0; i < pixels; i++)
                    result.Brightness[i] = Math.Clamp(picture.Brightness[i] * scale, 0f, 1f);
            }
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Brightness",
            Operator = typeof(BrightnessEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Factor", factor } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the brightness effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Brightness(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the brightness effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Brightness(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        /// <summary>Apply the brightness effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IHDRPicture<ushort>> Brightness(float factor)
            => context.SetAndReturn(Process(context.Result, factor));
    }
}
