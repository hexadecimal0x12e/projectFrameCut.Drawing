using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class SharpenEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float amount)
    {
        ArgumentNullException.ThrowIfNull(picture);
        amount = Math.Clamp(amount, 0f, 5f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (amount <= 0f)
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
                OperationDisplayName = "Sharpen",
                Operator = typeof(SharpenEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object> { { "Amount", amount } },
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
            int yTop = Math.Max(0, y - 1);
            int yBottom = Math.Min(height - 1, y + 1);
            for (int x = 0; x < width; x++)
            {
                int xLeft = Math.Max(0, x - 1);
                int xRight = Math.Min(width - 1, x + 1);

                int i = y * width + x;
                int left = y * width + xLeft;
                int right = y * width + xRight;
                int top = yTop * width + x;
                int bottom = yBottom * width + x;

                float rAvg = (picture.r[left] + picture.r[right] + picture.r[top] + picture.r[bottom]) * 0.25f;
                float gAvg = (picture.g[left] + picture.g[right] + picture.g[top] + picture.g[bottom]) * 0.25f;
                float bAvg = (picture.b[left] + picture.b[right] + picture.b[top] + picture.b[bottom]) * 0.25f;

                result.r[i] = (byte)Math.Clamp((int)Math.Round(picture.r[i] + amount * (picture.r[i] - rAvg)), 0, 255);
                result.g[i] = (byte)Math.Clamp((int)Math.Round(picture.g[i] + amount * (picture.g[i] - gAvg)), 0, 255);
                result.b[i] = (byte)Math.Clamp((int)Math.Round(picture.b[i] + amount * (picture.b[i] - bAvg)), 0, 255);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Sharpen",
            Operator = typeof(SharpenEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Amount", amount } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float amount)
    {
        ArgumentNullException.ThrowIfNull(picture);
        amount = Math.Clamp(amount, 0f, 5f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (amount <= 0f)
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
                OperationDisplayName = "Sharpen",
                Operator = typeof(SharpenEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object> { { "Amount", amount } },
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

        const int max16 = 65535;
        for (int y = 0; y < height; y++)
        {
            int yTop = Math.Max(0, y - 1);
            int yBottom = Math.Min(height - 1, y + 1);
            for (int x = 0; x < width; x++)
            {
                int xLeft = Math.Max(0, x - 1);
                int xRight = Math.Min(width - 1, x + 1);

                int i = y * width + x;
                int left = y * width + xLeft;
                int right = y * width + xRight;
                int top = yTop * width + x;
                int bottom = yBottom * width + x;

                float rAvg = (picture.r[left] + picture.r[right] + picture.r[top] + picture.r[bottom]) * 0.25f;
                float gAvg = (picture.g[left] + picture.g[right] + picture.g[top] + picture.g[bottom]) * 0.25f;
                float bAvg = (picture.b[left] + picture.b[right] + picture.b[top] + picture.b[bottom]) * 0.25f;

                result.r[i] = (ushort)Math.Clamp((int)Math.Round(picture.r[i] + amount * (picture.r[i] - rAvg)), 0, max16);
                result.g[i] = (ushort)Math.Clamp((int)Math.Round(picture.g[i] + amount * (picture.g[i] - gAvg)), 0, max16);
                result.b[i] = (ushort)Math.Clamp((int)Math.Round(picture.b[i] + amount * (picture.b[i] - bAvg)), 0, max16);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Sharpen",
            Operator = typeof(SharpenEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Amount", amount } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float amount)
    {
        ArgumentNullException.ThrowIfNull(picture);
        amount = Math.Clamp(amount, 0f, 5f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (amount <= 0f)
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
                OperationDisplayName = "Sharpen",
                Operator = typeof(SharpenEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object> { { "Amount", amount } },
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

        const int max16 = 65535;
        for (int y = 0; y < height; y++)
        {
            int yTop = Math.Max(0, y - 1);
            int yBottom = Math.Min(height - 1, y + 1);
            for (int x = 0; x < width; x++)
            {
                int xLeft = Math.Max(0, x - 1);
                int xRight = Math.Min(width - 1, x + 1);

                int i = y * width + x;
                int left = y * width + xLeft;
                int right = y * width + xRight;
                int top = yTop * width + x;
                int bottom = yBottom * width + x;

                float rAvg = (picture.r[left] + picture.r[right] + picture.r[top] + picture.r[bottom]) * 0.25f;
                float gAvg = (picture.g[left] + picture.g[right] + picture.g[top] + picture.g[bottom]) * 0.25f;
                float bAvg = (picture.b[left] + picture.b[right] + picture.b[top] + picture.b[bottom]) * 0.25f;

                result.r[i] = (ushort)Math.Clamp((int)Math.Round(picture.r[i] + amount * (picture.r[i] - rAvg)), 0, max16);
                result.g[i] = (ushort)Math.Clamp((int)Math.Round(picture.g[i] + amount * (picture.g[i] - gAvg)), 0, max16);
                result.b[i] = (ushort)Math.Clamp((int)Math.Round(picture.b[i] + amount * (picture.b[i] - bAvg)), 0, max16);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness!, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Sharpen",
            Operator = typeof(SharpenEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object> { { "Amount", amount } },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture Process(IPicture source, float amount)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, amount),
            IPicture<ushort> p16 when p16 is IHDRPicture<ushort> hdr => Process(hdr, amount),
            IPicture<ushort> p16 => Process(p16, amount),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Sharpen(float amount)
            => context.SetAndReturn(Process(context.Result, amount));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Sharpen(float amount)
            => context.SetAndReturn(Process(context.Result, amount));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Sharpen(float amount)
            => context.SetAndReturn(Process(context.Result, amount));
    }
}
