using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class VignetteEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, float strength, float radius)
    {
        ArgumentNullException.ThrowIfNull(picture);
        strength = Math.Clamp(strength, 0f, 1f);
        radius = Math.Clamp(radius, 0.05f, 0.99f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (strength <= 0f)
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
                OperationDisplayName = "Vignette",
                Operator = typeof(VignetteEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Strength", strength },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        double centerX = (width - 1) / 2d;
        double centerY = (height - 1) / 2d;
        double maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);
        double startDistance = maxDistance * radius;
        double fadeRange = Math.Max(1e-6, maxDistance - startDistance);

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
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double t = distance <= startDistance ? 0d : Math.Clamp((distance - startDistance) / fadeRange, 0d, 1d);
                float factor = Math.Clamp((float)(1d - strength * t * t), 0f, 1f);

                result.r[i] = (byte)Math.Clamp((int)Math.Round(picture.r[i] * factor), 0, 255);
                result.g[i] = (byte)Math.Clamp((int)Math.Round(picture.g[i] * factor), 0, 255);
                result.b[i] = (byte)Math.Clamp((int)Math.Round(picture.b[i] * factor), 0, 255);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Vignette",
            Operator = typeof(VignetteEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Strength", strength },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, float strength, float radius)
    {
        ArgumentNullException.ThrowIfNull(picture);
        strength = Math.Clamp(strength, 0f, 1f);
        radius = Math.Clamp(radius, 0.05f, 0.99f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (strength <= 0f)
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
                OperationDisplayName = "Vignette",
                Operator = typeof(VignetteEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Strength", strength },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        double centerX = (width - 1) / 2d;
        double centerY = (height - 1) / 2d;
        double maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);
        double startDistance = maxDistance * radius;
        double fadeRange = Math.Max(1e-6, maxDistance - startDistance);

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
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double t = distance <= startDistance ? 0d : Math.Clamp((distance - startDistance) / fadeRange, 0d, 1d);
                float factor = Math.Clamp((float)(1d - strength * t * t), 0f, 1f);

                result.r[i] = (ushort)Math.Clamp((int)Math.Round(picture.r[i] * factor), 0, 65535);
                result.g[i] = (ushort)Math.Clamp((int)Math.Round(picture.g[i] * factor), 0, 65535);
                result.b[i] = (ushort)Math.Clamp((int)Math.Round(picture.b[i] * factor), 0, 65535);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Vignette",
            Operator = typeof(VignetteEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Strength", strength },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture, float strength, float radius)
    {
        ArgumentNullException.ThrowIfNull(picture);
        strength = Math.Clamp(strength, 0f, 1f);
        radius = Math.Clamp(radius, 0.05f, 0.99f);
        var sw = Stopwatch.StartNew();
        int pixels = picture.Pixels;
        int width = picture.Width;
        int height = picture.Height;

        if (strength <= 0f)
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
                OperationDisplayName = "Vignette",
                Operator = typeof(VignetteEffect),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Strength", strength },
                    { "Radius", radius }
                },
                Elapsed = TimeSpan.Zero,
            });
            return cloned;
        }

        double centerX = (width - 1) / 2d;
        double centerY = (height - 1) / 2d;
        double maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);
        double startDistance = maxDistance * radius;
        double fadeRange = Math.Max(1e-6, maxDistance - startDistance);

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
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double t = distance <= startDistance ? 0d : Math.Clamp((distance - startDistance) / fadeRange, 0d, 1d);
                float factor = Math.Clamp((float)(1d - strength * t * t), 0f, 1f);

                result.r[i] = (ushort)Math.Clamp((int)Math.Round(picture.r[i] * factor), 0, 65535);
                result.g[i] = (ushort)Math.Clamp((int)Math.Round(picture.g[i] * factor), 0, 65535);
                result.b[i] = (ushort)Math.Clamp((int)Math.Round(picture.b[i] * factor), 0, 65535);
                if (result.a != null && picture.a != null)
                    result.a[i] = picture.a[i];
            }
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness!, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Vignette",
            Operator = typeof(VignetteEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "Strength", strength },
                { "Radius", radius }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture Process(IPicture source, float strength, float radius)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, strength, radius),
            IPicture<ushort> p16 when p16 is IHDRPicture<ushort> hdr => Process(hdr, strength, radius),
            IPicture<ushort> p16 => Process(p16, strength, radius),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Vignette(float strength, float radius)
            => context.SetAndReturn(Process(context.Result, strength, radius));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Vignette(float strength, float radius)
            => context.SetAndReturn(Process(context.Result, strength, radius));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Vignette(float strength, float radius)
            => context.SetAndReturn(Process(context.Result, strength, radius));
    }
}
