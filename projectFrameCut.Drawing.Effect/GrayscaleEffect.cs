using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class GrayscaleEffect
{
    private const float RWeight = 0.2126f;
    private const float GWeight = 0.7152f;
    private const float BWeight = 0.0722f;

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
            byte gray = (byte)Math.Clamp(
                (int)(picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight + 0.5f),
                0, 255);
            result.r[i] = gray;
            result.g[i] = gray;
            result.b[i] = gray;
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Grayscale",
            Operator = typeof(GrayscaleEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

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
            ushort gray = (ushort)Math.Clamp(
                (int)(picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight + 0.5f),
                0, 65535);
            result.r[i] = gray;
            result.g[i] = gray;
            result.b[i] = gray;
        }

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Grayscale",
            Operator = typeof(GrayscaleEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

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
            ushort gray = (ushort)Math.Clamp(
                (int)(picture.r[i] * RWeight + picture.g[i] * GWeight + picture.b[i] * BWeight + 0.5f),
                0, 65535);
            result.r[i] = gray;
            result.g[i] = gray;
            result.b[i] = gray;
        }

        if (picture.Brightness != null && picture.Brightness.Length == pixels)
            Array.Copy(picture.Brightness, result.Brightness, pixels);

        if (result.a != null && picture.a != null)
            Array.Copy(picture.a, result.a, pixels);

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Grayscale",
            Operator = typeof(GrayscaleEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Grayscale()
            => context.SetAndReturn(Process(context.Result));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Grayscale()
            => context.SetAndReturn(Process(context.Result));
    }
    extension(ProcessableIPictureContext<IHDRPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IHDRPicture<ushort>> Grayscale()
            => context.SetAndReturn(Process(context.Result));
    }
}
