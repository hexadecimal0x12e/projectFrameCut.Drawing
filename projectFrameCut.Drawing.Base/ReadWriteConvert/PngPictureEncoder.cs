using System.Buffers.Binary;
using System.IO.Compression;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public sealed class PngPictureEncoder(bool? saveAlpha = null) : IPictureEncoder
    {
        private readonly bool? _saveAlpha = saveAlpha;
        private static readonly uint[] Crc32Table = CreateCrc32Table();
        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

        public void Save(IPicture<byte> picture, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentNullException.ThrowIfNull(outputStream);

            lock (picture)
            {
                float[]? aa = picture.HasAlphaChannel ? picture.a : null;
                bool alpha = _saveAlpha ?? (picture.HasAlphaChannel && aa is not null);
                WritePng8(outputStream, picture.Width, picture.Height, picture.r, picture.g, picture.b, alpha, aa);
            }
        }

        public void Save(IPicture<ushort> picture, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentNullException.ThrowIfNull(outputStream);

            lock (picture)
            {
                float[]? aa = picture.HasAlphaChannel ? picture.a : null;
                bool alpha = _saveAlpha ?? (picture.HasAlphaChannel && aa is not null);
                WritePng16(outputStream, picture.Width, picture.Height, picture.r, picture.g, picture.b, alpha, aa);
            }
        }

        public void Save(IHDRPicture<ushort> picture, Stream outputStream)
            => Save((IPicture<ushort>)picture, outputStream);

        private static void WritePng8(Stream stream, int width, int height, byte[] rr, byte[] gg, byte[] bb, bool writeAlpha, float[]? aa)
        {
            int pixelCount = checked(width * height);
            bool hasAlpha = writeAlpha;
            int bytesPerPixel = hasAlpha ? 4 : 3;
            int rowLength = checked(width * bytesPerPixel);

            WritePngHeader(stream, width, height, bitDepth: 8, colorType: (byte)(hasAlpha ? 6 : 2));

            using var compressed = new MemoryStream(Math.Max(1024, pixelCount * bytesPerPixel / 2));
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] row = new byte[rowLength + 1];
                for (int y = 0; y < height; y++)
                {
                    int rowPixelOffset = checked(y * width);
                    row[0] = 0;
                    int offset = 1;
                    for (int x = 0; x < width; x++)
                    {
                        int i = rowPixelOffset + x;
                        row[offset++] = rr[i];
                        row[offset++] = gg[i];
                        row[offset++] = bb[i];
                        if (hasAlpha)
                        {
                            row[offset++] = (byte)(Math.Clamp(aa is null ? 1f : aa[i], 0f, 1f) * 255f);
                        }
                    }
                    zlib.Write(row, 0, row.Length);
                }
            }

            WritePngChunk(stream, "IDAT"u8, compressed.ToArray());
            WritePngChunk(stream, "IEND"u8, ReadOnlySpan<byte>.Empty);
        }

        private static void WritePng16(Stream stream, int width, int height, ushort[] rr, ushort[] gg, ushort[] bb, bool writeAlpha, float[]? aa)
        {
            int pixelCount = checked(width * height);
            bool hasAlpha = writeAlpha;
            int bytesPerPixel = hasAlpha ? 8 : 6;
            int rowLength = checked(width * bytesPerPixel);

            WritePngHeader(stream, width, height, bitDepth: 16, colorType: (byte)(hasAlpha ? 6 : 2));

            using var compressed = new MemoryStream(Math.Max(1024, pixelCount * bytesPerPixel / 2));
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] row = new byte[rowLength + 1];
                for (int y = 0; y < height; y++)
                {
                    int rowPixelOffset = checked(y * width);
                    row[0] = 0;
                    int offset = 1;
                    for (int x = 0; x < width; x++)
                    {
                        int i = rowPixelOffset + x;
                        BinaryPrimitives.WriteUInt16BigEndian(row.AsSpan(offset, 2), rr[i]);
                        offset += 2;
                        BinaryPrimitives.WriteUInt16BigEndian(row.AsSpan(offset, 2), gg[i]);
                        offset += 2;
                        BinaryPrimitives.WriteUInt16BigEndian(row.AsSpan(offset, 2), bb[i]);
                        offset += 2;
                        if (hasAlpha)
                        {
                            BinaryPrimitives.WriteUInt16BigEndian(row.AsSpan(offset, 2), (ushort)(Math.Clamp(aa is null ? 1f : aa[i], 0f, 1f) * 65535f));
                            offset += 2;
                        }
                    }
                    zlib.Write(row, 0, row.Length);
                }
            }

            WritePngChunk(stream, "IDAT"u8, compressed.ToArray());
            WritePngChunk(stream, "IEND"u8, ReadOnlySpan<byte>.Empty);
        }

        private static void WritePngHeader(Stream stream, int width, int height, byte bitDepth, byte colorType)
        {
            stream.Write(PngSignature);

            Span<byte> ihdr = stackalloc byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr[..4], (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), (uint)height);
            ihdr[8] = bitDepth;
            ihdr[9] = colorType;
            ihdr[10] = 0;
            ihdr[11] = 0;
            ihdr[12] = 0;
            WritePngChunk(stream, "IHDR"u8, ihdr);
        }

        private static void WritePngChunk(Stream stream, ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
        {
            Span<byte> lengthBuffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)data.Length);
            stream.Write(lengthBuffer);
            stream.Write(chunkType);
            if (!data.IsEmpty)
            {
                stream.Write(data);
            }

            uint crc = ComputePngCrc32(chunkType, data);
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, crc);
            stream.Write(lengthBuffer);
        }

        private static uint ComputePngCrc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < first.Length; i++)
            {
                crc = Crc32Table[(crc ^ first[i]) & 0xFF] ^ (crc >> 8);
            }
            for (int i = 0; i < second.Length; i++)
            {
                crc = Crc32Table[(crc ^ second[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] CreateCrc32Table()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }
                table[i] = value;
            }
            return table;
        }
    }
}
