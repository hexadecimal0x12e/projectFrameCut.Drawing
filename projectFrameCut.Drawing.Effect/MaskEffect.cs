using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class MaskEffect
{
    public static IPicture<byte> Process(IPicture<byte> frame, BitMaskPicture maskPic)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(maskPic);
        var sw = Stopwatch.StartNew();
        int pixels = frame.Pixels;
        int width = frame.Width;
        int height = frame.Height;
        bool sizeMatch = maskPic.Width == width && maskPic.Height == height;

        var result = new Picture8bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<byte>(pixels),
            g = GC.AllocateUninitializedArray<byte>(pixels),
            b = GC.AllocateUninitializedArray<byte>(pixels),
            a = new float[pixels],
            HasAlphaChannel = true,
            Tag = frame.Tag,
            ProcessStack = new List<PictureProcessStack>(frame.ProcessStack),
        };

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                bool keepPixel = ResolveMaskValue(maskPic, sizeMatch, x, y, width, height);
                if (keepPixel)
                {
                    result.r[index] = frame.r[index];
                    result.g[index] = frame.g[index];
                    result.b[index] = frame.b[index];
                    result.a![index] = frame.a?[index] ?? 1f;
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Mask",
            Operator = typeof(MaskEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> frame, BitMaskPicture maskPic)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(maskPic);
        var sw = Stopwatch.StartNew();
        int pixels = frame.Pixels;
        int width = frame.Width;
        int height = frame.Height;
        bool sizeMatch = maskPic.Width == width && maskPic.Height == height;

        var result = new Picture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = new float[pixels],
            HasAlphaChannel = true,
            Tag = frame.Tag,
            ProcessStack = new List<PictureProcessStack>(frame.ProcessStack),
        };

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                bool keepPixel = ResolveMaskValue(maskPic, sizeMatch, x, y, width, height);
                if (keepPixel)
                {
                    result.r[index] = frame.r[index];
                    result.g[index] = frame.g[index];
                    result.b[index] = frame.b[index];
                    result.a![index] = frame.a?[index] ?? 1f;
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Mask",
            Operator = typeof(MaskEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    private static bool ResolveMaskValue(BitMaskPicture maskPic, bool sizeMatch, int x, int y, int frameWidth, int frameHeight)
    {
        if (sizeMatch)
        {
            return maskPic.r[y * maskPic.Width + x];
        }

        int maskX = (int)((float)x / frameWidth * maskPic.Width);
        int maskY = (int)((float)y / frameHeight * maskPic.Height);
        int maskIndex = maskY * maskPic.Width + maskX;
        return maskIndex >= 0 && maskIndex < maskPic.r.Length ? maskPic.r[maskIndex] : true;
    }

    public static IPicture Process(IPicture frame, BitMaskPicture maskPic)
    {
        return frame switch
        {
            IPicture<byte> p8 => Process(p8, maskPic),
            IPicture<ushort> p16 => Process(p16, maskPic),
            _ => throw new NotSupportedException($"Unsupported picture type: {frame.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Mask(BitMaskPicture maskPic)
            => context.SetAndReturn(Process(context.Result, maskPic));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Mask(BitMaskPicture maskPic)
            => context.SetAndReturn(Process(context.Result, maskPic));
    }
}
