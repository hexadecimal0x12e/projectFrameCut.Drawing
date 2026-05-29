using projectFrameCut.Drawing.Base;

namespace projectFrameCut.Drawing.Processing.Cropping
{
    public interface IPictureCropper
    {
        IPicture<ushort> Crop(IPicture<ushort> source, int startX, int startY, int width, int height);
        IPicture<byte> Crop(IPicture<byte> source, int startX, int startY, int width, int height);
        IHDRPicture<ushort> Crop(IHDRPicture<ushort> source, int startX, int startY, int width, int height);
    }

    public static class PictureCropper
    {
        /// <summary>
        /// Default cropper implementation used by extension methods.
        /// </summary>
        public static IPictureCropper Default = new CPUPictureCropper();

        public static IPicture Crop(IPicture picture, int startX, int startY, int width, int height)
        {
            if (picture is IHDRPicture<ushort> hdrPicture)
                return Default.Crop(hdrPicture, startX, startY, width, height);
            if (picture is IPicture<ushort> ushortPicture)
                return Default.Crop(ushortPicture, startX, startY, width, height);
            if (picture is IPicture<byte> bytePicture)
                return Default.Crop(bytePicture, startX, startY, width, height);

            throw new NotSupportedException($"Unsupported picture type: {picture.GetType().FullName}");
        }

        extension<T>(ProcessableIPictureContext<T> ctx) where T : IPicture
        {
            public ProcessableIPictureContext<T> Crop(int startX, int startY, int width, int height)
            {
                return ctx.SetAndReturn((T?)Crop(ctx.Result, startX, startY, width, height));
            }
        }

    }
}
