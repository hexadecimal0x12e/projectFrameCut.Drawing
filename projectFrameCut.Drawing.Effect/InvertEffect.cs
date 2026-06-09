using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Inverts the color channels of a picture.</summary>
public static class InvertEffect
{
    /// <summary>Apply the invert effect to an 8-bit picture.</summary>
    public static IPicture<byte> Process(IPicture<byte> picture)
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
            result.r[i] = (byte)(255 - picture.r[i]);
            result.g[i] = (byte)(255 - picture.g[i]);
            result.b[i] = (byte)(255 - picture.b[i]);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Invert",
            Operator = typeof(InvertEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the invert effect to a 16-bit picture.</summary>
    public static IPicture<ushort> Process(IPicture<ushort> picture)
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
            result.r[i] = (ushort)(65535 - picture.r[i]);
            result.g[i] = (ushort)(65535 - picture.g[i]);
            result.b[i] = (ushort)(65535 - picture.b[i]);
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Invert",
            Operator = typeof(InvertEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the invert effect to an HDR picture.</summary>
    public static IHDRPicture<ushort> Process(IHDRPicture<ushort> picture)
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
            result.r[i] = (ushort)(65535 - picture.r[i]);
            result.g[i] = (ushort)(65535 - picture.g[i]);
            result.b[i] = (ushort)(65535 - picture.b[i]);
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Invert",
            Operator = typeof(InvertEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the invert effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Invert()
            => context.SetAndReturn(Process(context.Result));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the invert effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Invert()
            => context.SetAndReturn(Process(context.Result));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        /// <summary>Apply the invert effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IHDRPicture<ushort>> Invert()
            => context.SetAndReturn(Process(context.Result));
    }
}
