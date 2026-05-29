using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public sealed class VfdPictureEncoder(bool compress = true) : IPictureEncoder
    {
        private const int VfdHeaderSize = 18;
        private readonly bool _compress = compress;

        public void Save(IPicture<byte> picture, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentNullException.ThrowIfNull(outputStream);

            lock (picture)
            {
                bool hasAlpha = picture.HasAlphaChannel && picture.a is not null;
                WriteVfdHeader(outputStream, frameType: 0, picture.Width, picture.Height, hasAlpha, maxBrightness: 0f, _compress);
                Stream payload = _compress ? new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true) : outputStream;
                try
                {
                    WriteVfdPicture(payload, picture.r, picture.g, picture.b, hasAlpha, picture.a is null ? default : picture.a);
                }
                finally
                {
                    if (!ReferenceEquals(payload, outputStream))
                    {
                        payload.Dispose();
                    }
                }
            }
        }

        public void Save(IPicture<ushort> picture, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentNullException.ThrowIfNull(outputStream);

            if (picture is IHDRPicture<ushort> hdrPicture)
            {
                Save(hdrPicture, outputStream);
                return;
            }

            lock (picture)
            {
                bool hasAlpha = picture.HasAlphaChannel && picture.a is not null;
                WriteVfdHeader(outputStream, frameType: 1, picture.Width, picture.Height, hasAlpha, maxBrightness: 0f, _compress);
                Stream payload = _compress ? new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true) : outputStream;
                try
                {
                    WriteVfdPicture(payload, picture.r, picture.g, picture.b, hasAlpha, picture.a is null ? default : picture.a);
                }
                finally
                {
                    if (!ReferenceEquals(payload, outputStream))
                    {
                        payload.Dispose();
                    }
                }
            }
        }

        public void Save(IHDRPicture<ushort> picture, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentNullException.ThrowIfNull(outputStream);

            lock (picture)
            {
                bool hasAlpha = picture.HasAlphaChannel && picture.a is not null;
                WriteVfdHeader(outputStream, frameType: 2, picture.Width, picture.Height, hasAlpha, picture.MaximumBrightness, _compress);
                Stream payload = _compress ? new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true) : outputStream;
                try
                {
                    WriteVfdHdrPicture(payload, picture.r, picture.g, picture.b, picture.Brightness, hasAlpha, picture.a is null ? default : picture.a);
                }
                finally
                {
                    if (!ReferenceEquals(payload, outputStream))
                    {
                        payload.Dispose();
                    }
                }
            }
        }

        private static void WriteVfdHeader(Stream stream, int frameType, int width, int height, bool hasAlpha, float maxBrightness, bool compress)
        {
            Span<byte> header = stackalloc byte[VfdHeaderSize];
            header[0] = (byte)'V';
            header[1] = (byte)'F';
            header[2] = (byte)'C';
            header[3] = (byte)'D';
            header[4] = (byte)frameType;
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(5, 4), width);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(9, 4), height);
            byte flags = hasAlpha ? (byte)1 : (byte)0;
            if (!compress)
            {
                flags |= 2;
            }
            header[13] = flags;
            MemoryMarshal.Write(header.Slice(14, 4), in maxBrightness);
            stream.Write(header);
        }

        private static void WriteVfdPicture<T>(Stream stream, ReadOnlySpan<T> r, ReadOnlySpan<T> g, ReadOnlySpan<T> b, bool hasAlpha, ReadOnlySpan<float> alpha = default)
            where T : unmanaged
        {
            WriteRawBytes(stream, r);
            WriteRawBytes(stream, g);
            WriteRawBytes(stream, b);

            if (hasAlpha)
            {
                WriteRawBytes(stream, alpha);
            }
        }

        private static void WriteVfdHdrPicture(Stream stream, ReadOnlySpan<ushort> r, ReadOnlySpan<ushort> g, ReadOnlySpan<ushort> b, ReadOnlySpan<float> brightness, bool hasAlpha, ReadOnlySpan<float> alpha = default)
        {
            WriteRawBytes(stream, r);
            WriteRawBytes(stream, g);
            WriteRawBytes(stream, b);
            WriteRawBytes(stream, brightness);

            if (hasAlpha)
            {
                WriteRawBytes(stream, alpha);
            }
        }

        private static void WriteRawBytes<T>(Stream stream, ReadOnlySpan<T> source)
            where T : unmanaged
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(source);
            stream.Write(bytes);
        }
    }
}
