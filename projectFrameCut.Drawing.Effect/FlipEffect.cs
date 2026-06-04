using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class FlipEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, bool horizontal, bool vertical)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (!horizontal && !vertical)
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
                OperationDisplayName = "Flip",
                Operator = typeof(FlipEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Horizontal", horizontal },
                    { "Vertical", vertical }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

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

        for (int y = 0; y < height; y++)
        {
            int srcY = vertical ? height - 1 - y : y;
            int srcRow = srcY * width;
            int dstRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int srcX = horizontal ? width - 1 - x : x;
                int srcIndex = srcRow + srcX;
                int dstIndex = dstRow + x;
                result.r[dstIndex] = picture.r[srcIndex];
                result.g[dstIndex] = picture.g[srcIndex];
                result.b[dstIndex] = picture.b[srcIndex];
                if (result.a != null && picture.a != null)
                {
                    result.a[dstIndex] = picture.a[srcIndex];
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Flip",
            Operator = typeof(FlipEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Horizontal", horizontal },
                { "Vertical", vertical }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, bool horizontal, bool vertical)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (!horizontal && !vertical)
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
                OperationDisplayName = "Flip",
                Operator = typeof(FlipEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Horizontal", horizontal },
                    { "Vertical", vertical }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

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

        for (int y = 0; y < height; y++)
        {
            int srcY = vertical ? height - 1 - y : y;
            int srcRow = srcY * width;
            int dstRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int srcX = horizontal ? width - 1 - x : x;
                int srcIndex = srcRow + srcX;
                int dstIndex = dstRow + x;
                result.r[dstIndex] = picture.r[srcIndex];
                result.g[dstIndex] = picture.g[srcIndex];
                result.b[dstIndex] = picture.b[srcIndex];
                if (result.a != null && picture.a != null)
                {
                    result.a[dstIndex] = picture.a[srcIndex];
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Flip",
            Operator = typeof(FlipEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Horizontal", horizontal },
                { "Vertical", vertical }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, bool horizontal, bool vertical)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (!horizontal && !vertical)
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
                OperationDisplayName = "Flip",
                Operator = typeof(FlipEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Horizontal", horizontal },
                    { "Vertical", vertical }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

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

        for (int y = 0; y < height; y++)
        {
            int srcY = vertical ? height - 1 - y : y;
            int srcRow = srcY * width;
            int dstRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int srcX = horizontal ? width - 1 - x : x;
                int srcIndex = srcRow + srcX;
                int dstIndex = dstRow + x;
                result.r[dstIndex] = picture.r[srcIndex];
                result.g[dstIndex] = picture.g[srcIndex];
                result.b[dstIndex] = picture.b[srcIndex];
                if (result.a != null && picture.a != null)
                    result.a[dstIndex] = picture.a[srcIndex];
                if (result.Brightness != null && picture.Brightness != null && srcIndex < picture.Brightness.Length)
                    result.Brightness[dstIndex] = picture.Brightness[srcIndex];
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Flip",
            Operator = typeof(FlipEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Horizontal", horizontal },
                { "Vertical", vertical }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture Process(IPicture source, bool horizontal, bool vertical)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, horizontal, vertical),
            IPicture<ushort> p16 when p16 is IHDRPicture<ushort> hdr => Process(hdr, horizontal, vertical),
            IPicture<ushort> p16 => Process(p16, horizontal, vertical),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Flip(bool horizontal, bool vertical)
            => context.SetAndReturn(Process(context.Result, horizontal, vertical));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Flip(bool horizontal, bool vertical)
            => context.SetAndReturn(Process(context.Result, horizontal, vertical));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Flip(bool horizontal, bool vertical)
            => context.SetAndReturn(Process(context.Result, horizontal, vertical));
    }
}
