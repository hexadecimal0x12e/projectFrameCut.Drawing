using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Processing.Composing
{
    public interface IPictureComposer
    {
        IPicture<ushort> Compose(IPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode);
        IPicture<byte> Compose(IPicture<byte> basePicture, IPicture<byte> topPicture, BlendMode blendMode);
        IHDRPicture<ushort> Compose(IHDRPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode);

        IPicture<ushort> Compose(IPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight);
        IPicture<byte> Compose(IPicture<byte> basePicture, IPicture<byte> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight);
        IHDRPicture<ushort> Compose(IHDRPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight);
    }

    public static class PictureComposer
    {
        public static IPictureComposer Default = new CPUBlendPictureComposer();

        public static IPicture Compose(IPicture basePicture, IPicture topPicture, BlendMode blendMode)
        {
            if (basePicture is IHDRPicture<ushort> hdrBase && topPicture is IPicture<ushort> ushortTop)
                return Default.Compose(hdrBase, ushortTop, blendMode);
            if (basePicture is IPicture<ushort> ushortBase && topPicture is IPicture<ushort> ushortTop2)
                return Default.Compose(ushortBase, ushortTop2, blendMode);
            if (basePicture is IPicture<byte> byteBase && topPicture is IPicture<byte> byteTop)
                return Default.Compose(byteBase, byteTop, blendMode);

            throw new NotSupportedException($"Unsupported picture types: {basePicture.GetType().FullName} and {topPicture.GetType().FullName}");
        }

        extension<T>(ProcessableIPictureContext<T> ctx) where T : IPicture
        {
            public ProcessableIPictureContext<T> Compose(IPicture topPicture, BlendMode blendMode)
            {
                return ctx.SetAndReturn((T?)Compose(ctx.Result, topPicture, blendMode));
            }
        }


    }
}
