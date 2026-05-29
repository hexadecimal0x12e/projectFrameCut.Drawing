using projectFrameCut.Drawing.Base.Picture;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    public sealed class VfdPictureDecoder : IPictureDecoder
    {
        private const int VfdHeaderSize = 18;

        public IPicture<byte> LoadByte(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            if (!TryLoad(inputStream, out Picture8bpp? picture))
            {
                throw new InvalidDataException("Input stream does not contain a VFD 8bpp picture.");
            }
            return picture;
        }

        public IPicture<ushort> LoadUshort(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            if (!TryLoad(inputStream, out Picture16bpp? picture))
            {
                throw new InvalidDataException("Input stream does not contain a VFD 16bpp picture.");
            }
            return picture;
        }

        public IHDRPicture<ushort> LoadHDR(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            if (!TryLoad(inputStream, out HDRPicture16bpp? picture))
            {
                throw new InvalidDataException("Input stream does not contain a VFD HDR picture.");
            }
            return picture;
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out IPicture? picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);

            long startPosition = stream.CanSeek ? stream.Position : 0;
            if (!TryReadVfdHeader(stream, out var header))
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
                return false;
            }

            Stream payload = header.Compressed ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : stream;
            try
            {
                picture = header.FrameType switch
                {
                    0 => ReadVfdPicture8bpp(payload, header.Width, header.Height, header.HasAlpha),
                    1 => ReadVfdPicture16bpp(payload, header.Width, header.Height, header.HasAlpha),
                    2 => ReadVfdPictureHdr(payload, header.Width, header.Height, header.HasAlpha, header.MaximumBrightness),
                    _ => throw new InvalidDataException("Unsupported VFD frame type.")
                };
                return true;
            }
            finally
            {
                if (!ReferenceEquals(payload, stream))
                {
                    payload.Dispose();
                }
            }
        }

        public int GetBitPerPixel(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            long startPosition = inputStream.CanSeek ? inputStream.Position : 0;
            try
            {
                if (!TryReadVfdHeader(inputStream, out var header))
                {
                    throw new InvalidDataException("Input stream does not contain a VFD picture.");
                }

                return header.FrameType switch
                {
                    0 => 8,
                    1 => 16,
                    2 => 16,
                    _ => throw new InvalidDataException("Unsupported VFD frame type."),
                };
            }
            finally
            {
                if (inputStream.CanSeek)
                {
                    inputStream.Position = startPosition;
                }
            }
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out Picture8bpp picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);

            if (!TryReadVfdHeader(stream, out var header) || header.FrameType != 0)
            {
                return false;
            }

            Stream payload = header.Compressed ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : stream;
            try
            {
                picture = ReadVfdPicture8bpp(payload, header.Width, header.Height, header.HasAlpha);
                return true;
            }
            finally
            {
                if (!ReferenceEquals(payload, stream))
                {
                    payload.Dispose();
                }
            }
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out Picture16bpp picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);

            if (!TryReadVfdHeader(stream, out var header) || header.FrameType != 1)
            {
                return false;
            }

            Stream payload = header.Compressed ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : stream;
            try
            {
                picture = ReadVfdPicture16bpp(payload, header.Width, header.Height, header.HasAlpha);
                return true;
            }
            finally
            {
                if (!ReferenceEquals(payload, stream))
                {
                    payload.Dispose();
                }
            }
        }

        public bool TryLoad(Stream stream, [MaybeNullWhen(false)] out HDRPicture16bpp picture)
        {
            picture = null;
            ArgumentNullException.ThrowIfNull(stream);

            if (!TryReadVfdHeader(stream, out var header) || header.FrameType != 2)
            {
                return false;
            }

            Stream payload = header.Compressed ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : stream;
            try
            {
                picture = ReadVfdPictureHdr(payload, header.Width, header.Height, header.HasAlpha, header.MaximumBrightness);
                return true;
            }
            finally
            {
                if (!ReferenceEquals(payload, stream))
                {
                    payload.Dispose();
                }
            }
        }

        private static bool TryReadVfdHeader(Stream stream, out VfdFrameHeader header)
        {
            Span<byte> buffer = stackalloc byte[VfdHeaderSize];
            if (!TryReadExactly(stream, buffer))
            {
                header = default;
                return false;
            }

            if (buffer[0] != (byte)'V' || buffer[1] != (byte)'F' || buffer[2] != (byte)'C' || buffer[3] != (byte)'D')
            {
                header = default;
                return false;
            }

            int frameType = buffer[4];
            if (frameType < 0 || frameType > 2)
            {
                header = default;
                return false;
            }

            int width = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5, 4));
            int height = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(9, 4));
            bool hasAlpha = (buffer[13] & 1) != 0;
            bool compressed = (buffer[13] & 2) == 0;
            float maxBrightness = MemoryMarshal.Read<float>(buffer.Slice(14, 4));

            if (width <= 0 || height <= 0)
            {
                header = default;
                return false;
            }

            header = new VfdFrameHeader(frameType, width, height, hasAlpha, compressed, maxBrightness);
            return true;
        }

        private static Picture8bpp ReadVfdPicture8bpp(Stream stream, int width, int height, bool hasAlpha)
        {
            int pixels = checked(width * height);
            var picture = new Picture8bpp(width, height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<byte>(pixels),
                g = GC.AllocateUninitializedArray<byte>(pixels),
                b = GC.AllocateUninitializedArray<byte>(pixels),
                a = null,
                HasAlphaChannel = hasAlpha,
            };

            ReadRawBytes(stream, picture.r);
            ReadRawBytes(stream, picture.g);
            ReadRawBytes(stream, picture.b);

            if (hasAlpha)
            {
                picture.a = GC.AllocateUninitializedArray<float>(pixels);
                ReadRawBytes(stream, picture.a);
            }

            picture.ProcessStack = [new PictureProcessStack
            {
                OperationDisplayName = "Loaded from VFD cache",
                Operator = typeof(VfdPictureDecoder),
                ProcessingFuncStackTrace = new StackTrace(true),
            }];

            return picture;
        }

        private static Picture16bpp ReadVfdPicture16bpp(Stream stream, int width, int height, bool hasAlpha)
        {
            int pixels = checked(width * height);
            var picture = new Picture16bpp(width, height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                a = null,
                HasAlphaChannel = hasAlpha,
            };

            ReadRawBytes(stream, picture.r);
            ReadRawBytes(stream, picture.g);
            ReadRawBytes(stream, picture.b);

            if (hasAlpha)
            {
                picture.a = GC.AllocateUninitializedArray<float>(pixels);
                ReadRawBytes(stream, picture.a);
            }

            picture.ProcessStack = [new PictureProcessStack
            {
                OperationDisplayName = "Loaded from VFD cache",
                Operator = typeof(VfdPictureDecoder),
                ProcessingFuncStackTrace = new StackTrace(true),
            }];

            return picture;
        }

        private static HDRPicture16bpp ReadVfdPictureHdr(Stream stream, int width, int height, bool hasAlpha, float maximumBrightness)
        {
            int pixels = checked(width * height);
            var picture = new HDRPicture16bpp(width, height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                a = null,
                HasAlphaChannel = hasAlpha,
                Brightness = GC.AllocateUninitializedArray<float>(pixels),
                MaximumBrightness = maximumBrightness > 0f && float.IsFinite(maximumBrightness) ? maximumBrightness : 1000f,
            };

            ReadRawBytes(stream, picture.r);
            ReadRawBytes(stream, picture.g);
            ReadRawBytes(stream, picture.b);
            ReadRawBytes(stream, picture.Brightness);

            if (hasAlpha)
            {
                picture.a = GC.AllocateUninitializedArray<float>(pixels);
                ReadRawBytes(stream, picture.a);
            }

            picture.ProcessStack = [new PictureProcessStack
            {
                OperationDisplayName = "Loaded from VFD cache",
                Operator = typeof(VfdPictureDecoder),
                ProcessingFuncStackTrace = new StackTrace(true),
            }];

            return picture;
        }

        private static void ReadRawBytes<T>(Stream stream, T[] destination)
            where T : unmanaged
        {
            ReadRawBytes(stream, destination.AsSpan());
        }

        private static void ReadRawBytes<T>(Stream stream, Span<T> destination)
            where T : unmanaged
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(destination);
            while (!bytes.IsEmpty)
            {
                int read = stream.Read(bytes);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of VFD payload.");
                }
                bytes = bytes.Slice(read);
            }
        }

        private static bool TryReadExactly(Stream stream, Span<byte> destination)
        {
            while (!destination.IsEmpty)
            {
                int read = stream.Read(destination);
                if (read <= 0)
                {
                    return false;
                }
                destination = destination.Slice(read);
            }

            return true;
        }

        private readonly record struct VfdFrameHeader(int FrameType, int Width, int Height, bool HasAlpha, bool Compressed, float MaximumBrightness);
    }
}
