using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Effect;

/// <summary>Crops a picture to a specified rectangle.</summary>
public static class CropEffect
{
    /// <summary>Apply the crop effect to an 8-bit picture.</summary>
    /// <param name="startX">X coordinate of the top-left corner in the source picture.</param>
    /// <param name="startY">Y coordinate of the top-left corner in the source picture.</param>
    /// <param name="width">Width of the crop rectangle.</param>
    /// <param name="height">Height of the crop rectangle.</param>
    public static IPicture<byte> Process(IPicture<byte> picture, int startX, int startY, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Width and Height must be positive.");
        if (startX < 0 || startY < 0 || startX + width > picture.Width || startY + height > picture.Height)
            throw new ArgumentOutOfRangeException(nameof(startX), "Crop rectangle must stay inside source bounds.");

        var sw = Stopwatch.StartNew();
        int pixels = width * height;
        int srcWidth = picture.Width;

        var result = new Picture8bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<byte>(pixels),
            g = GC.AllocateUninitializedArray<byte>(pixels),
            b = GC.AllocateUninitializedArray<byte>(pixels),
            a = picture.HasAlphaChannel ? new float[pixels] : null,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int y = 0; y < height; y++)
        {
            int srcOffset = (startY + y) * srcWidth + startX;
            int dstOffset = y * width;
            Array.Copy(picture.r, srcOffset, result.r, dstOffset, width);
            Array.Copy(picture.g, srcOffset, result.g, dstOffset, width);
            Array.Copy(picture.b, srcOffset, result.b, dstOffset, width);
            if (result.a != null && picture.a != null)
            {
                Array.Copy(picture.a, srcOffset, result.a, dstOffset, width);
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Crop",
            Operator = typeof(CropEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "StartX", startX },
                { "StartY", startY },
                { "Width", width },
                { "Height", height }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the crop effect to a 16-bit picture.</summary>
    /// <param name="startX">X coordinate of the top-left corner in the source picture.</param>
    /// <param name="startY">Y coordinate of the top-left corner in the source picture.</param>
    /// <param name="width">Width of the crop rectangle.</param>
    /// <param name="height">Height of the crop rectangle.</param>
    public static IPicture<ushort> Process(IPicture<ushort> picture, int startX, int startY, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Width and Height must be positive.");
        if (startX < 0 || startY < 0 || startX + width > picture.Width || startY + height > picture.Height)
            throw new ArgumentOutOfRangeException(nameof(startX), "Crop rectangle must stay inside source bounds.");

        var sw = Stopwatch.StartNew();
        int pixels = width * height;
        int srcWidth = picture.Width;

        var result = new Picture16bpp(width, height)
        {
            r = GC.AllocateUninitializedArray<ushort>(pixels),
            g = GC.AllocateUninitializedArray<ushort>(pixels),
            b = GC.AllocateUninitializedArray<ushort>(pixels),
            a = picture.HasAlphaChannel ? new float[pixels] : null,
            HasAlphaChannel = picture.HasAlphaChannel,
            Tag = picture.Tag,
            ProcessStack = new List<PictureProcessStack>(picture.ProcessStack),
        };

        for (int y = 0; y < height; y++)
        {
            int srcOffset = (startY + y) * srcWidth + startX;
            int dstOffset = y * width;
            Array.Copy(picture.r, srcOffset, result.r, dstOffset, width);
            Array.Copy(picture.g, srcOffset, result.g, dstOffset, width);
            Array.Copy(picture.b, srcOffset, result.b, dstOffset, width);
            if (result.a != null && picture.a != null)
            {
                Array.Copy(picture.a, srcOffset, result.a, dstOffset, width);
            }
        }

        result.ProcessStack.Add(new PictureProcessStack
        {
            OperationDisplayName = "Crop",
            Operator = typeof(CropEffect),
            ProcessingFuncStackTrace = new StackTrace(true),
            Properties = new Dictionary<string, object>
            {
                { "StartX", startX },
                { "StartY", startY },
                { "Width", width },
                { "Height", height }
            },
            Elapsed = sw.Elapsed,
        });

        return result;
    }

    /// <summary>Apply the crop effect using runtime type dispatch.</summary>
    /// <param name="startX">X coordinate of the top-left corner in the source picture.</param>
    /// <param name="startY">Y coordinate of the top-left corner in the source picture.</param>
    /// <param name="width">Width of the crop rectangle.</param>
    /// <param name="height">Height of the crop rectangle.</param>
    public static IPicture Process(IPicture source, int startX, int startY, int width, int height)
    {
        return source switch
        {
            IPicture<byte> p8 => Process(p8, startX, startY, width, height),
            IPicture<ushort> p16 => Process(p16, startX, startY, width, height),
            _ => throw new NotSupportedException($"Unsupported picture type: {source.GetType().Name}"),
        };
    }

    extension(ProcessableIPictureContext<IPicture<byte>> context)
    {
        /// <summary>Apply the crop effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<byte>> Crop(int startX, int startY, int width, int height)
            => context.SetAndReturn(Process(context.Result, startX, startY, width, height));
    }
    extension(ProcessableIPictureContext<IPicture<ushort>> context)
    {
        /// <summary>Apply the crop effect in a processing pipeline.</summary>
        public ProcessableIPictureContext<IPicture<ushort>> Crop(int startX, int startY, int width, int height)
            => context.SetAndReturn(Process(context.Result, startX, startY, width, height));
    }
}
