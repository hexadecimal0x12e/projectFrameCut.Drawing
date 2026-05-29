using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public interface IPictureDecoder
    {
        public IPicture<byte> LoadByte(Stream inputStream);
        public IPicture<ushort> LoadUshort(Stream inputStream);
        public IHDRPicture<ushort> LoadHDR(Stream inputStream);
        public bool TryLoad(Stream inputStream, out IPicture? picture);
    }
}
