using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Processing.Resizing
{
    public interface IPictureResizer
    {
        IPicture<ushort> Resize(IPicture<ushort> source, int targetWidth, int targetHeight, bool preserveAspect);
        IPicture<byte> Resize(IPicture<byte> source, int targetWidth, int targetHeight, bool preserveAspect);
        IHDRPicture<ushort> Resize(IHDRPicture<ushort> source, int targetWidth, int targetHeight, bool preserveAspect);
    }

    public static class PictureResizer
    {
        /// <summary>
        /// A default picture resizer using CPU and bilinear interpolation.
        /// You can implement your own resizer and set it to this property to use it in these extension methods.
        /// </summary>
        public static IPictureResizer Default = new CPUBilinearPictureResizer();

        extension<T>(ProcessableIPictureContext<T> ctx) where T : IPicture
        {
            public ProcessableIPictureContext<T> Resize(int targetWidth, int targetHeight, bool preserveAspect)
            {
                return ctx.SetAndReturn((T?)ctx.Result.Resize(targetWidth, targetHeight, preserveAspect));
            }
        }

        extension(IPicture picture)
        {
            /// <summary>
            /// Resize the picture to the target width and height. If preserveAspect is true, the aspect ratio will be preserved and the resulting image may be smaller than the target size in one dimension.
            /// </summary>
            public IPicture Resize(int targetWidth, int targetHeight, bool preserveAspect)
            {
                if (picture is IPicture<ushort> ushortPicture)
                    return Default.Resize(ushortPicture, targetWidth, targetHeight, preserveAspect);
                else if (picture is IPicture<byte> bytePicture)
                    return Default.Resize(bytePicture, targetWidth, targetHeight, preserveAspect);
                else if (picture is IHDRPicture<ushort> hdrUShortPicture)
                    return Default.Resize(hdrUShortPicture, targetWidth, targetHeight, preserveAspect);
                else
                    throw new NotSupportedException($"Unsupported picture type: {picture.GetType().FullName}");
            }
                
        }
        extension(IPicture<ushort> picture)
        {
            /// <summary>
            /// Resize the picture to the target width and height. If preserveAspect is true, the aspect ratio will be preserved and the resulting image may be smaller than the target size in one dimension.
            /// </summary>
            public IPicture<ushort> Resize(int targetWidth, int targetHeight, bool preserveAspect)
                => Default.Resize(picture, targetWidth, targetHeight, preserveAspect);
        }
        extension(IPicture<byte> picture)
        {
            /// <summary>
            /// Resize the picture to the target width and height. If preserveAspect is true, the aspect ratio will be preserved and the resulting image may be smaller than the target size in one dimension.
            /// </summary>
            public IPicture<byte> Resize(int targetWidth, int targetHeight, bool preserveAspect)
                => Default.Resize(picture, targetWidth, targetHeight, preserveAspect);
        }
        extension(IHDRPicture<ushort> picture)
        {
            /// <summary>
            /// Resize the picture to the target width and height. If preserveAspect is true, the aspect ratio will be preserved and the resulting image may be smaller than the target size in one dimension.
            /// </summary>
            public IHDRPicture<ushort> Resize(int targetWidth, int targetHeight, bool preserveAspect)
                => Default.Resize(picture, targetWidth, targetHeight, preserveAspect);
        }
        
    }
}
