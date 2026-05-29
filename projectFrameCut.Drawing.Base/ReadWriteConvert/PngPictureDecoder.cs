using projectFrameCut.Drawing.Base.Picture;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public sealed class PngPictureDecoder : IPictureDecoder
    {
        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly uint[] Crc32Table = CreateCrc32Table();

        public IPicture<byte> LoadByte(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            IPicture loaded = DecodePng(inputStream);

            if (loaded is Picture8bpp picture8)
            {
                return picture8;
            }

            Picture16bpp picture16 = (Picture16bpp)loaded;
            int pixels = picture16.Pixels;
            var converted = new Picture8bpp(picture16.Width, picture16.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<byte>(pixels),
                g = GC.AllocateUninitializedArray<byte>(pixels),
                b = GC.AllocateUninitializedArray<byte>(pixels),
                a = null,
                HasAlphaChannel = picture16.HasAlphaChannel,
            };

            for (int i = 0; i < pixels; i++)
            {
                converted.r[i] = (byte)(picture16.r[i] >> 8);
                converted.g[i] = (byte)(picture16.g[i] >> 8);
                converted.b[i] = (byte)(picture16.b[i] >> 8);
            }

            if (picture16.HasAlphaChannel && picture16.a is not null)
            {
                converted.a = GC.AllocateUninitializedArray<float>(pixels);
                Array.Copy(picture16.a, converted.a, pixels);
            }

            converted.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream (8bpp output)",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return converted;
        }

        public IPicture<ushort> LoadUshort(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            IPicture loaded = DecodePng(inputStream);

            if (loaded is Picture16bpp picture16)
            {
                return picture16;
            }

            Picture8bpp picture8 = (Picture8bpp)loaded;
            int pixels = picture8.Pixels;
            var converted = new Picture16bpp(picture8.Width, picture8.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                a = null,
                HasAlphaChannel = picture8.HasAlphaChannel,
            };

            for (int i = 0; i < pixels; i++)
            {
                converted.r[i] = (ushort)(picture8.r[i] * 257u);
                converted.g[i] = (ushort)(picture8.g[i] * 257u);
                converted.b[i] = (ushort)(picture8.b[i] * 257u);
            }

            if (picture8.HasAlphaChannel && picture8.a is not null)
            {
                converted.a = GC.AllocateUninitializedArray<float>(pixels);
                Array.Copy(picture8.a, converted.a, pixels);
            }

            converted.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream (16bpp output)",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return converted;
        }

        public int GetBitPerPixel(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            long startPosition = inputStream.CanSeek ? inputStream.Position : 0;
            try
            {
                return ReadBitDepth(inputStream);
            }
            finally
            {
                if (inputStream.CanSeek)
                {
                    inputStream.Position = startPosition;
                }
            }
        }

        public IHDRPicture<ushort> LoadHDR(Stream inputStream)
        {
            throw new NotImplementedException("PNG does not support HDR formats. Consider use another format.");
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out IPicture? picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);
            long startPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                picture = DecodePng(stream);
                return true;
            }
            catch (InvalidDataException)
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
                return false;
            }
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out Picture8bpp picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);
            long startPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                IPicture loaded = DecodePng(stream);
                if (loaded is Picture8bpp picture8)
                {
                    picture = picture8;
                    return true;
                }

                picture = ConvertTo8Bpp((Picture16bpp)loaded);
                return true;
            }
            catch (InvalidDataException)
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
                return false;
            }
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out Picture16bpp picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);
            long startPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                IPicture loaded = DecodePng(stream);
                if (loaded is Picture16bpp picture16)
                {
                    picture = picture16;
                    return true;
                }

                picture = ConvertTo16Bpp((Picture8bpp)loaded);
                return true;
            }
            catch (InvalidDataException)
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
                return false;
            }
        }

        private static IPicture DecodePng(Stream stream)
        {
            PngHeader header = ReadHeader(stream);
            byte[] idatData = ReadImageData(stream);
            return header.BitDepth switch
            {
                8 => DecodePixelData8(header, idatData),
                16 => DecodePixelData16(header, idatData),
                _ => throw new InvalidDataException("Only 8-bit and 16-bit PNG are supported."),
            };
        }

        private static PngHeader ReadHeader(Stream stream)
        {
            ValidateSignature(stream);

            uint chunkLength = ReadUInt32BigEndian(stream);
            if (chunkLength != 13)
            {
                throw new InvalidDataException("Invalid IHDR chunk size.");
            }

            Span<byte> chunkType = stackalloc byte[4];
            ReadExactly(stream, chunkType);
            if (!chunkType.SequenceEqual("IHDR"u8))
            {
                throw new InvalidDataException("PNG IHDR chunk is missing.");
            }

            Span<byte> payload = stackalloc byte[13];
            ReadExactly(stream, payload);

            uint expectedCrc = ReadUInt32BigEndian(stream);
            uint actualCrc = ComputePngCrc32(chunkType, payload);
            if (expectedCrc != actualCrc)
            {
                throw new InvalidDataException("PNG chunk CRC mismatch.");
            }

            return ParseHeader(payload);
        }

        private static byte[] ReadImageData(Stream stream)
        {
            using var idatData = new MemoryStream();
            bool reachedIend = false;
            byte[] chunkType = new byte[4];

            while (!reachedIend)
            {
                uint chunkLength = ReadUInt32BigEndian(stream);
                ReadExactly(stream, chunkType);

                if (chunkLength > int.MaxValue)
                {
                    throw new InvalidDataException("PNG chunk is too large.");
                }

                byte[] payload = GC.AllocateUninitializedArray<byte>((int)chunkLength);
                ReadExactly(stream, payload);

                uint expectedCrc = ReadUInt32BigEndian(stream);
                uint actualCrc = ComputePngCrc32(chunkType, payload);
                if (expectedCrc != actualCrc)
                {
                    throw new InvalidDataException("PNG chunk CRC mismatch.");
                }

                if (chunkType.SequenceEqual("IDAT"u8))
                {
                    idatData.Write(payload);
                }
                else if (chunkType.SequenceEqual("IEND"u8))
                {
                    reachedIend = true;
                }
            }

            if (idatData.Length == 0)
            {
                throw new InvalidDataException("PNG IDAT payload is missing.");
            }

            return idatData.ToArray();
        }

        private static int ReadBitDepth(Stream stream)
        {
            PngHeader header = ReadHeader(stream);
            return header.BitDepth switch
            {
                8 => 8,
                16 => 16,
                _ => throw new InvalidDataException("Only 8-bit and 16-bit PNG are supported."),
            };
        }

        private static Picture8bpp DecodePixelData8(PngHeader header, byte[] idatData)
        {
            int channels = header.ColorType switch
            {
                2 => 3,
                6 => 4,
                _ => throw new InvalidDataException("Only RGB/RGBA PNG color types are supported."),
            };
            int bytesPerPixel = channels;
            int scanlineLength = checked(header.Width * bytesPerPixel);
            int expectedSize = checked(header.Height * (scanlineLength + 1));
            byte[] raw = Decompress(expectedSize, idatData);
            int pixelCount = checked(header.Width * header.Height);
            bool hasAlpha = channels == 4;

            var picture = new Picture8bpp(header.Width, header.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<byte>(pixelCount),
                g = GC.AllocateUninitializedArray<byte>(pixelCount),
                b = GC.AllocateUninitializedArray<byte>(pixelCount),
                a = null,
                HasAlphaChannel = hasAlpha,
            };

            if (hasAlpha)
            {
                picture.a = GC.AllocateUninitializedArray<float>(pixelCount);
            }

            byte[] previousRow = GC.AllocateUninitializedArray<byte>(scanlineLength);
            byte[] currentRow = GC.AllocateUninitializedArray<byte>(scanlineLength);
            int rawOffset = 0;

            for (int y = 0; y < header.Height; y++)
            {
                byte filterType = raw[rawOffset++];
                Buffer.BlockCopy(raw, rawOffset, currentRow, 0, scanlineLength);
                rawOffset += scanlineLength;
                ApplyFilter(filterType, currentRow, previousRow, bytesPerPixel);

                int rowPixelBase = checked(y * header.Width);
                Parse8BitRow(currentRow, rowPixelBase, picture, hasAlpha);

                (previousRow, currentRow) = (currentRow, previousRow);
            }

            picture.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return picture;
        }

        private static Picture16bpp DecodePixelData16(PngHeader header, byte[] idatData)
        {
            int channels = header.ColorType switch
            {
                2 => 3,
                6 => 4,
                _ => throw new InvalidDataException("Only RGB/RGBA PNG color types are supported."),
            };
            int bytesPerSample = 2;
            int bytesPerPixel = checked(channels * bytesPerSample);
            int scanlineLength = checked(header.Width * bytesPerPixel);
            int expectedSize = checked(header.Height * (scanlineLength + 1));
            byte[] raw = Decompress(expectedSize, idatData);
            int pixelCount = checked(header.Width * header.Height);
            bool hasAlpha = channels == 4;

            var picture = new Picture16bpp(header.Width, header.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixelCount),
                g = GC.AllocateUninitializedArray<ushort>(pixelCount),
                b = GC.AllocateUninitializedArray<ushort>(pixelCount),
                a = null,
                HasAlphaChannel = hasAlpha,
            };

            if (hasAlpha)
            {
                picture.a = GC.AllocateUninitializedArray<float>(pixelCount);
            }

            byte[] previousRow = GC.AllocateUninitializedArray<byte>(scanlineLength);
            byte[] currentRow = GC.AllocateUninitializedArray<byte>(scanlineLength);
            int rawOffset = 0;

            for (int y = 0; y < header.Height; y++)
            {
                byte filterType = raw[rawOffset++];
                Buffer.BlockCopy(raw, rawOffset, currentRow, 0, scanlineLength);
                rawOffset += scanlineLength;
                ApplyFilter(filterType, currentRow, previousRow, bytesPerPixel);

                int rowPixelBase = checked(y * header.Width);
                Parse16BitRow(currentRow, rowPixelBase, picture, hasAlpha);

                (previousRow, currentRow) = (currentRow, previousRow);
            }

            picture.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return picture;
        }

        private static void Parse8BitRow(byte[] row, int rowPixelBase, Picture8bpp picture, bool hasAlpha)
        {
            int channels = hasAlpha ? 4 : 3;
            for (int x = 0; x < picture.Width; x++)
            {
                int source = x * channels;
                int destination = rowPixelBase + x;

                picture.r[destination] = row[source];
                picture.g[destination] = row[source + 1];
                picture.b[destination] = row[source + 2];

                if (hasAlpha && picture.a is not null)
                {
                    picture.a[destination] = row[source + 3] / 255f;
                }
            }
        }

        private static void Parse16BitRow(byte[] row, int rowPixelBase, Picture16bpp picture, bool hasAlpha)
        {
            int channels = hasAlpha ? 4 : 3;
            int bytesPerPixel = channels * 2;
            for (int x = 0; x < picture.Width; x++)
            {
                int source = x * bytesPerPixel;
                int destination = rowPixelBase + x;

                picture.r[destination] = BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(source, 2));
                picture.g[destination] = BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(source + 2, 2));
                picture.b[destination] = BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(source + 4, 2));

                if (hasAlpha && picture.a is not null)
                {
                    ushort alpha = BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(source + 6, 2));
                    picture.a[destination] = alpha / 65535f;
                }
            }
        }

        private static byte[] Decompress(int expectedSize, byte[] idatData)
        {
            byte[] data = GC.AllocateUninitializedArray<byte>(expectedSize);
            using var compressed = new MemoryStream(idatData, writable: false);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            ReadExactly(zlib, data);
            if (zlib.ReadByte() != -1)
            {
                throw new InvalidDataException("PNG stream contains more image data than expected.");
            }
            return data;
        }

        private static PngHeader ParseHeader(ReadOnlySpan<byte> payload)
        {
            if (payload.Length != 13)
            {
                throw new InvalidDataException("Invalid IHDR chunk size.");
            }

            int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[..4]));
            int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)));
            byte bitDepth = payload[8];
            byte colorType = payload[9];
            byte compression = payload[10];
            byte filter = payload[11];
            byte interlace = payload[12];

            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException("PNG width/height must be greater than 0.");
            }
            if (compression != 0 || filter != 0)
            {
                throw new InvalidDataException("PNG uses unsupported compression/filter method.");
            }
            if (interlace != 0)
            {
                throw new InvalidDataException("Interlaced PNG is not supported.");
            }

            return new PngHeader(width, height, bitDepth, colorType);
        }

        private static void ApplyFilter(byte filterType, byte[] current, byte[] previous, int bytesPerPixel)
        {
            switch (filterType)
            {
                case 0:
                    return;
                case 1:
                    for (int i = bytesPerPixel; i < current.Length; i++)
                    {
                        current[i] = unchecked((byte)(current[i] + current[i - bytesPerPixel]));
                    }
                    return;
                case 2:
                    for (int i = 0; i < current.Length; i++)
                    {
                        current[i] = unchecked((byte)(current[i] + previous[i]));
                    }
                    return;
                case 3:
                    for (int i = 0; i < current.Length; i++)
                    {
                        int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                        int up = previous[i];
                        current[i] = unchecked((byte)(current[i] + ((left + up) >> 1)));
                    }
                    return;
                case 4:
                    for (int i = 0; i < current.Length; i++)
                    {
                        int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                        int up = previous[i];
                        int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                        current[i] = unchecked((byte)(current[i] + PaethPredictor(left, up, upLeft)));
                    }
                    return;
                default:
                    throw new InvalidDataException($"Unsupported PNG filter type: {filterType}.");
            }
        }

        private static int PaethPredictor(int left, int up, int upLeft)
        {
            int p = left + up - upLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - up);
            int pc = Math.Abs(p - upLeft);

            if (pa <= pb && pa <= pc)
            {
                return left;
            }
            if (pb <= pc)
            {
                return up;
            }
            return upLeft;
        }

        private static void ValidateSignature(Stream stream)
        {
            Span<byte> signature = stackalloc byte[8];
            ReadExactly(stream, signature);
            if (!signature.SequenceEqual(PngSignature))
            {
                throw new InvalidDataException("Input stream does not contain a PNG picture.");
            }
        }

        private static uint ReadUInt32BigEndian(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            ReadExactly(stream, buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        private static void ReadExactly(Stream stream, Span<byte> destination)
        {
            while (!destination.IsEmpty)
            {
                int read = stream.Read(destination);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of PNG stream.");
                }
                destination = destination.Slice(read);
            }
        }

        private static Picture8bpp ConvertTo8Bpp(Picture16bpp picture16)
        {
            int pixels = picture16.Pixels;
            var converted = new Picture8bpp(picture16.Width, picture16.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<byte>(pixels),
                g = GC.AllocateUninitializedArray<byte>(pixels),
                b = GC.AllocateUninitializedArray<byte>(pixels),
                a = null,
                HasAlphaChannel = picture16.HasAlphaChannel,
            };

            for (int i = 0; i < pixels; i++)
            {
                converted.r[i] = (byte)(picture16.r[i] >> 8);
                converted.g[i] = (byte)(picture16.g[i] >> 8);
                converted.b[i] = (byte)(picture16.b[i] >> 8);
            }

            if (picture16.HasAlphaChannel && picture16.a is not null)
            {
                converted.a = GC.AllocateUninitializedArray<float>(pixels);
                Array.Copy(picture16.a, converted.a, pixels);
            }

            converted.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream (8bpp output)",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return converted;
        }

        private static Picture16bpp ConvertTo16Bpp(Picture8bpp picture8)
        {
            int pixels = picture8.Pixels;
            var converted = new Picture16bpp(picture8.Width, picture8.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                a = null,
                HasAlphaChannel = picture8.HasAlphaChannel,
            };

            for (int i = 0; i < pixels; i++)
            {
                converted.r[i] = (ushort)(picture8.r[i] * 257u);
                converted.g[i] = (ushort)(picture8.g[i] * 257u);
                converted.b[i] = (ushort)(picture8.b[i] * 257u);
            }

            if (picture8.HasAlphaChannel && picture8.a is not null)
            {
                converted.a = GC.AllocateUninitializedArray<float>(pixels);
                Array.Copy(picture8.a, converted.a, pixels);
            }

            converted.ProcessStack =
            [
                new PictureProcessStack
                {
                    OperationDisplayName = "Loaded from PNG stream (16bpp output)",
                    Operator = typeof(PngPictureDecoder),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }
            ];

            return converted;
        }

        private static uint ComputePngCrc32(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < chunkType.Length; i++)
            {
                crc = Crc32Table[(crc ^ chunkType[i]) & 0xFF] ^ (crc >> 8);
            }
            for (int i = 0; i < data.Length; i++)
            {
                crc = Crc32Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
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

        private readonly record struct PngHeader(int Width, int Height, byte BitDepth, byte ColorType);
    }
}
