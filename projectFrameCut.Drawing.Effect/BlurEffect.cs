using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Applies a box blur to a picture.</summary>
public static class BlurEffect
{
    /// <summary>Apply the blur effect to an 8-bit picture.</summary>
    /// <param name="sigma">Blur sigma. Radius is derived as ceil(sigma). A value of 0 returns a clone.</param>
    public static IPicture<byte> Process(IPicture<byte> picture, float sigma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int radius = Math.Max(0, (int)Math.Ceiling(sigma));
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (radius == 0)
        {
            var cloned = new Picture8bpp(width, height)
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
            Array.Copy(picture.r, cloned.r, pixels);
            Array.Copy(picture.g, cloned.g, pixels);
            Array.Copy(picture.b, cloned.b, pixels);
            if (cloned.a != null && picture.a != null)
                Array.Copy(picture.a, cloned.a, pixels);

            cloned.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Blur",
                Operator = typeof(BlurEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Sigma", sigma },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        var rBlur = BoxBlurChannel(picture.r, width, height, radius);
        var gBlur = BoxBlurChannel(picture.g, width, height, radius);
        var bBlur = BoxBlurChannel(picture.b, width, height, radius);
        var aBlur = picture.a is null ? null : BoxBlurChannel(picture.a, width, height, radius);

        var result = new Picture8bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<byte>(pixels),
            g = GC.AllocateUninitializedArray<byte>(pixels),
            b = GC.AllocateUninitializedArray<byte>(pixels),
            a = aBlur,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (byte)Math.Clamp((int)Math.Round(rBlur[i]), 0, 255);
            result.g[i] = (byte)Math.Clamp((int)Math.Round(gBlur[i]), 0, 255);
            result.b[i] = (byte)Math.Clamp((int)Math.Round(bBlur[i]), 0, 255);
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Blur",
            Operator = typeof(BlurEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Sigma", sigma },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the blur effect to a 16-bit picture.</summary>
    /// <param name="sigma">Blur sigma. A value of 0 returns a clone.</param>
    public static IPicture<ushort> Process(IPicture<ushort> picture, float sigma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int radius = Math.Max(0, (int)Math.Ceiling(sigma));
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (radius == 0)
        {
            var cloned = new Picture16bpp(width, height)
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
            Array.Copy(picture.r, cloned.r, pixels);
            Array.Copy(picture.g, cloned.g, pixels);
            Array.Copy(picture.b, cloned.b, pixels);
            if (cloned.a != null && picture.a != null)
                Array.Copy(picture.a, cloned.a, pixels);

            cloned.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Blur",
                Operator = typeof(BlurEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Sigma", sigma },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        var rBlur = BoxBlurChannel(picture.r, width, height, radius);
        var gBlur = BoxBlurChannel(picture.g, width, height, radius);
        var bBlur = BoxBlurChannel(picture.b, width, height, radius);
        var aBlur = picture.a is null ? null : BoxBlurChannel(picture.a, width, height, radius);

        const int max16 = 65535;
        var result = new Picture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = aBlur,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)Math.Round(rBlur[i]), 0, max16);
            result.g[i] = (ushort)Math.Clamp((int)Math.Round(gBlur[i]), 0, max16);
            result.b[i] = (ushort)Math.Clamp((int)Math.Round(bBlur[i]), 0, max16);
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Blur",
            Operator = typeof(BlurEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Sigma", sigma },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the blur effect to an HDR picture.</summary>
    /// <param name="sigma">Blur sigma. A value of 0 returns a clone.</param>
    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float sigma)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int radius = Math.Max(0, (int)Math.Ceiling(sigma));
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (radius == 0)
        {
            var cloned = new HDRPicture16bpp(width, height)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                a = picture.HasAlphaChannel && picture.a != null
                    ? GC.AllocateUninitializedArray<float>(pixels) : null,
                HasAlphaChannel = picture.HasAlphaChannel,
                Brightness = GC.AllocateUninitializedArray<float>(pixels),
                MaximumBrightness = picture.MaximumBrightness,
                Tag = picture.Tag,
                ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
            };
            Array.Copy(picture.r, cloned.r, pixels);
            Array.Copy(picture.g, cloned.g, pixels);
            Array.Copy(picture.b, cloned.b, pixels);
            if (cloned.a != null && picture.a != null)
                Array.Copy(picture.a, cloned.a, pixels);
            if (picture.Brightness != null && picture.Brightness.Length == pixels)
                Array.Copy(picture.Brightness, cloned.Brightness!, pixels);

            cloned.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = "Blur",
                Operator = typeof(BlurEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Sigma", sigma },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        var rBlur = BoxBlurChannel(picture.r, width, height, radius);
        var gBlur = BoxBlurChannel(picture.g, width, height, radius);
        var bBlur = BoxBlurChannel(picture.b, width, height, radius);
        var aBlur = picture.a is null ? null : BoxBlurChannel(picture.a, width, height, radius);

        const int max16 = 65535;
        var result = new HDRPicture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = aBlur,
            HasAlphaChannel = picture.HasAlphaChannel,
            Brightness = GC.AllocateUninitializedArray<float>(pixels),
            MaximumBrightness = picture.MaximumBrightness > 0f && float.IsFinite(picture.MaximumBrightness)
                ? picture.MaximumBrightness : 1000f,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int i = 0; i < pixels; i++)
        {
            result.r[i] = (ushort)Math.Clamp((int)Math.Round(rBlur[i]), 0, max16);
            result.g[i] = (ushort)Math.Clamp((int)Math.Round(gBlur[i]), 0, max16);
            result.b[i] = (ushort)Math.Clamp((int)Math.Round(bBlur[i]), 0, max16);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness!, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Blur",
            Operator = typeof(BlurEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Sigma", sigma },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the blur effect using runtime type dispatch.</summary>
    /// <param name="sigma">Blur sigma. A value of 0 returns a clone.</param>
    public static IPicture Process(IPicture source, float sigma)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, sigma),
            IPicture<ushort> p16 when p16 is IHDRPicture<ushort> hdr => Process(hdr, sigma),
            IPicture<ushort> p16 => Process(p16, sigma),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    private static float[] BoxBlurChannel(float[] source, int width, int height, int radius)
    {
        var horizontal = new float[source.Length];
        var result = new float[source.Length];

        for (int y = 0; y < height; y++)
        {
            var prefix = new double[width + 1];
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                prefix[x + 1] = prefix[x] + source[rowOffset + x];
            }
            for (int x = 0; x < width; x++)
            {
                int left = Math.Max(0, x - radius);
                int right = Math.Min(width - 1, x + radius);
                horizontal[rowOffset + x] = (float)((prefix[right + 1] - prefix[left]) / (right - left + 1));
            }
        }

        for (int x = 0; x < width; x++)
        {
            var prefix = new double[height + 1];
            for (int y = 0; y < height; y++)
            {
                prefix[y + 1] = prefix[y] + horizontal[y * width + x];
            }
            for (int y = 0; y < height; y++)
            {
                int top = Math.Max(0, y - radius);
                int bottom = Math.Min(height - 1, y + radius);
                result[y * width + x] = (float)((prefix[bottom + 1] - prefix[top]) / (bottom - top + 1));
            }
        }

        return result;
    }

    private static float[] BoxBlurChannel(byte[] source, int width, int height, int radius)
    {
        int pixels = source.Length;
        var converted = GC.AllocateUninitializedArray<float>(pixels);
        for (int i = 0; i < pixels; i++)
            converted[i] = source[i];
        return BoxBlurChannel(converted, width, height, radius);
    }

    private static float[] BoxBlurChannel(ushort[] source, int width, int height, int radius)
    {
        int pixels = source.Length;
        var converted = GC.AllocateUninitializedArray<float>(pixels);
        for (int i = 0; i < pixels; i++)
            converted[i] = source[i];
        return BoxBlurChannel(converted, width, height, radius);
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the blur effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Blur(float sigma)
            => context.SetAndReturn(Process(context.Result, sigma));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the blur effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Blur(float sigma)
            => context.SetAndReturn(Process(context.Result, sigma));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        /// <summary>Apply the blur effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IHDRPicture<ushort>> Blur(float sigma)
            => context.SetAndReturn(Process(context.Result, sigma));
    }
}
