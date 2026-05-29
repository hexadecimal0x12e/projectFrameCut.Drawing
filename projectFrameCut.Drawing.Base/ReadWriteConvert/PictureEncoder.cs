using System.Runtime.CompilerServices;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public interface IPictureEncoder
    {
        public void Save(IPicture<byte> picture, Stream outputStream);
        public void Save(IPicture<ushort> picture, Stream outputStream);
        public void Save(IHDRPicture<ushort> picture, Stream outputStream);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SaveIPicture(IPicture source, Stream outputStream)
        {
            if (source is IPicture<byte> bytePicture)
            {
                Save(bytePicture, outputStream);
            }
            else if (source is IHDRPicture<ushort> ihdrUShortPicture)
            {
                Save(ihdrUShortPicture, outputStream);
            }
            else if (source is IPicture<ushort> ushortPicture)
            {
                Save(ushortPicture, outputStream);
            }
            else
            {
                throw new NotSupportedException($"Unsupported picture type for {nameof(IPictureEncoder)}: {source.GetType().FullName}");
            }
        }
    }
}
