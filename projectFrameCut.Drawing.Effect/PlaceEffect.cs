using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

public static class PlaceEffect
{
    public static IPicture<byte> Process(IPicture<byte> picture, int startX, int startY, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentException("targetWidth and targetHeight must be positive");

        var sw = Stopwatch.StartNew();
        int dstX = Math.Max(0, startX);
        int dstY = Math.Max(0, startY);
        int srcX = Math.Max(0, -startX);
        int srcY = Math.Max(0, -startY);
        int copyWidth = Math.Min(picture.Width - srcX, targetWidth - dstX);
        int copyHeight = Math.Min(picture.Height - srcY, targetHeight - dstY);

        var result = new Picture8bpp(targetWidth, targetHeight)
        {
            r = GC.AllocateUninitializedArray<byte>(targetWidth * targetHeight),
            g = GC.AllocateUninitializedArray<byte>(targetWidth * targetHeight),
            b = GC.AllocateUninitializedArray<byte>(targetWidth * targetHeight),
            a = new float[targetWidth * targetHeight],
            HasAlphaChannel = true,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        if (copyWidth > 0 && copyHeight > 0)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                int srcOffset = (srcY + y) * picture.Width + srcX;
                int dstOffset = (dstY + y) * targetWidth + dstX;
                Array.Copy(picture.r, srcOffset, result.r, dstOffset, copyWidth);
                Array.Copy(picture.g, srcOffset, result.g, dstOffset, copyWidth);
                Array.Copy(picture.b, srcOffset, result.b, dstOffset, copyWidth);
                if (picture.a != null)
                {
                    Array.Copy(picture.a, srcOffset, result.a!, dstOffset, copyWidth);
                }
                else
                {
                    Array.Fill(result.a!, 1f, dstOffset, copyWidth);
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Place",
            Operator = typeof(PlaceEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "StartX", startX },
                { "StartY", startY },
                { "TargetWidth", targetWidth },
                { "TargetHeight", targetHeight }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture<ushort> Process(IPicture<ushort> picture, int startX, int startY, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentException("targetWidth and targetHeight must be positive");

        var sw = Stopwatch.StartNew();
        int dstX = Math.Max(0, startX);
        int dstY = Math.Max(0, startY);
        int srcX = Math.Max(0, -startX);
        int srcY = Math.Max(0, -startY);
        int copyWidth = Math.Min(picture.Width - srcX, targetWidth - dstX);
        int copyHeight = Math.Min(picture.Height - srcY, targetHeight - dstY);

        var result = new Picture16bpp(targetWidth, targetHeight)
        {
            r = GC.AllocateUninitializedArray<ushort>(targetWidth * targetHeight),
            g = GC.AllocateUninitializedArray<ushort>(targetWidth * targetHeight),
            b = GC.AllocateUninitializedArray<ushort>(targetWidth * targetHeight),
            a = new float[targetWidth * targetHeight],
            HasAlphaChannel = true,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        if (copyWidth > 0 && copyHeight > 0)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                int srcOffset = (srcY + y) * picture.Width + srcX;
                int dstOffset = (dstY + y) * targetWidth + dstX;
                Array.Copy(picture.r, srcOffset, result.r, dstOffset, copyWidth);
                Array.Copy(picture.g, srcOffset, result.g, dstOffset, copyWidth);
                Array.Copy(picture.b, srcOffset, result.b, dstOffset, copyWidth);
                if (picture.a != null)
                {
                    Array.Copy(picture.a, srcOffset, result.a!, dstOffset, copyWidth);
                }
                else
                {
                    Array.Fill(result.a!, 1f, dstOffset, copyWidth);
                }
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Place",
            Operator = typeof(PlaceEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "StartX", startX },
                { "StartY", startY },
                { "TargetWidth", targetWidth },
                { "TargetHeight", targetHeight }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    public static IPicture Process(IPicture source, int startX, int startY, int targetWidth, int targetHeight)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, startX, startY, targetWidth, targetHeight),
            IPicture<ushort> p16 => Process(p16, startX, startY, targetWidth, targetHeight),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        public ProcessableIPictureContext<IPicture<byte>> Place(int startX, int startY, int targetWidth, int targetHeight)
            => context.SetAndReturn(Process(context.Result, startX, startY, targetWidth, targetHeight));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        public ProcessableIPictureContext<IPicture<ushort>> Place(int startX, int startY, int targetWidth, int targetHeight)
            => context.SetAndReturn(Process(context.Result, startX, startY, targetWidth, targetHeight));
    }
}
