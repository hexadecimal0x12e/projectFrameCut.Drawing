using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace projectFrameCut.Drawing.Base.ReadWriteConvert
{
    internal static class PictureFileLoader
    {
        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly byte[] VfdSignature = [(byte)'V', (byte)'F', (byte)'C', (byte)'D'];

        internal static void LoadInto(Picture8bpp target, string imagePath)
        {
            ArgumentNullException.ThrowIfNull(target);
            var source = LoadSourcePicture(imagePath);
            try
            {
                InitializeCommon(target, source, imagePath);
                target.r = GC.AllocateUninitializedArray<byte>(target.Pixels);
                target.g = GC.AllocateUninitializedArray<byte>(target.Pixels);
                target.b = GC.AllocateUninitializedArray<byte>(target.Pixels);

                switch (source)
                {
                    case HDRPicture16bpp sourceHdr:
                        PictureBufferUtilities.ConvertUShortToByte(sourceHdr.r, target.r);
                        PictureBufferUtilities.ConvertUShortToByte(sourceHdr.g, target.g);
                        PictureBufferUtilities.ConvertUShortToByte(sourceHdr.b, target.b);
                        CopyAlpha(sourceHdr.a, target);
                        break;
                    case Picture8bpp source8:
                        CopyChannels(source8.r, source8.g, source8.b, target.r, target.g, target.b);
                        CopyAlpha(source8.a, target);
                        break;
                    case MonoChromePicture8bpp sourceMono:
                        CopyMonoToRgb(sourceMono.gray, target.r, target.g, target.b);
                        CopyAlpha(sourceMono.a, target);
                        break;
                    case Picture16bpp source16:
                        PictureBufferUtilities.ConvertUShortToByte(source16.r, target.r);
                        PictureBufferUtilities.ConvertUShortToByte(source16.g, target.g);
                        PictureBufferUtilities.ConvertUShortToByte(source16.b, target.b);
                        CopyAlpha(source16.a, target);
                        break;
                    case BitMaskPicture sourceMask:
                        CopyMaskToRgb(sourceMask.r, sourceMask.g, sourceMask.b, target.r, target.g, target.b);
                        break;
                    default:
                        PopulateFromFallback(source, target);
                        break;
                }

                target.ProcessStack = [BuildProcessStack(imagePath, target.GetType(), target.BitPerPixel.Value)];
            }
            finally
            {
                source.Dispose();
            }
        }

        internal static void LoadInto(Picture16bpp target, string imagePath)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (target is HDRPicture16bpp hdrTarget)
            {
                LoadInto(hdrTarget, imagePath);
                return;
            }

            var source = LoadSourcePicture(imagePath);
            try
            {
                InitializeCommon(target, source, imagePath);
                target.r = GC.AllocateUninitializedArray<ushort>(target.Pixels);
                target.g = GC.AllocateUninitializedArray<ushort>(target.Pixels);
                target.b = GC.AllocateUninitializedArray<ushort>(target.Pixels);

                switch (source)
                {
                    case HDRPicture16bpp sourceHdr:
                        CopyChannels(sourceHdr.r, sourceHdr.g, sourceHdr.b, target.r, target.g, target.b);
                        CopyAlpha(sourceHdr.a, target);
                        break;
                    case Picture16bpp source16:
                        CopyChannels(source16.r, source16.g, source16.b, target.r, target.g, target.b);
                        CopyAlpha(source16.a, target);
                        break;
                    case Picture8bpp source8:
                        PictureBufferUtilities.ConvertByteToUShort(source8.r, target.r);
                        PictureBufferUtilities.ConvertByteToUShort(source8.g, target.g);
                        PictureBufferUtilities.ConvertByteToUShort(source8.b, target.b);
                        CopyAlpha(source8.a, target);
                        break;
                    case MonoChromePicture8bpp sourceMono:
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.r);
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.g);
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.b);
                        CopyAlpha(sourceMono.a, target);
                        break;
                    case BitMaskPicture sourceMask:
                        CopyMaskToUShort(sourceMask.r, sourceMask.g, sourceMask.b, target.r, target.g, target.b);
                        break;
                    default:
                        PopulateFromFallback(source, target);
                        break;
                }

                target.ProcessStack = [BuildProcessStack(imagePath, target.GetType(), target.BitPerPixel.Value)];
            }
            finally
            {
                source.Dispose();
            }
        }

        internal static void LoadInto(MonoChromePicture8bpp target, string imagePath)
        {
            ArgumentNullException.ThrowIfNull(target);
            var source = LoadSourcePicture(imagePath);
            try
            {
                InitializeCommon(target, source, imagePath);
                target.gray = GC.AllocateUninitializedArray<byte>(target.Pixels);

                switch (source)
                {
                    case MonoChromePicture8bpp sourceMono:
                        Array.Copy(sourceMono.gray, target.gray, target.Pixels);
                        CopyAlpha(sourceMono.a, target);
                        break;
                    case BitMaskPicture sourceMask:
                        CopyMaskToGray(sourceMask.r, sourceMask.g, sourceMask.b, target.gray);
                        break;
                    case Picture8bpp source8:
                        CopyRgbToGray(source8.r, source8.g, source8.b, target.gray);
                        CopyAlpha(source8.a, target);
                        break;
                    case HDRPicture16bpp sourceHdr:
                        CopyRgbToGray(sourceHdr.r, sourceHdr.g, sourceHdr.b, target.gray);
                        CopyAlpha(sourceHdr.a, target);
                        break;
                    case Picture16bpp source16:
                        CopyRgbToGray(source16.r, source16.g, source16.b, target.gray);
                        CopyAlpha(source16.a, target);
                        break;
                    default:
                        PopulateFromFallback(source, target);
                        break;
                }

                target.ProcessStack = [BuildProcessStack(imagePath, target.GetType(), target.BitPerPixel.Value)];
            }
            finally
            {
                source.Dispose();
            }
        }

        internal static void LoadInto(HDRPicture16bpp target, string imagePath)
        {
            ArgumentNullException.ThrowIfNull(target);
            var source = LoadSourcePicture(imagePath);
            try
            {
                InitializeCommon(target, source, imagePath);
                target.r = GC.AllocateUninitializedArray<ushort>(target.Pixels);
                target.g = GC.AllocateUninitializedArray<ushort>(target.Pixels);
                target.b = GC.AllocateUninitializedArray<ushort>(target.Pixels);
                target.Brightness = GC.AllocateUninitializedArray<float>(target.Pixels);

                switch (source)
                {
                    case HDRPicture16bpp sourceHdr:
                        CopyChannels(sourceHdr.r, sourceHdr.g, sourceHdr.b, target.r, target.g, target.b);
                        CopyAlpha(sourceHdr.a, target);
                        Array.Copy(sourceHdr.Brightness, target.Brightness, target.Pixels);
                        target.MaximumBrightness = sourceHdr.MaximumBrightness;
                        break;
                    case Picture16bpp source16:
                        CopyChannels(source16.r, source16.g, source16.b, target.r, target.g, target.b);
                        CopyAlpha(source16.a, target);
                        FillBrightness(target.Brightness, 1f);
                        target.MaximumBrightness = 1000f;
                        break;
                    case Picture8bpp source8:
                        PictureBufferUtilities.ConvertByteToUShort(source8.r, target.r);
                        PictureBufferUtilities.ConvertByteToUShort(source8.g, target.g);
                        PictureBufferUtilities.ConvertByteToUShort(source8.b, target.b);
                        CopyAlpha(source8.a, target);
                        FillBrightness(target.Brightness, 1f);
                        target.MaximumBrightness = 1000f;
                        break;
                    case MonoChromePicture8bpp sourceMono:
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.r);
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.g);
                        PictureBufferUtilities.ConvertByteToUShort(sourceMono.gray, target.b);
                        CopyAlpha(sourceMono.a, target);
                        FillBrightness(target.Brightness, 1f);
                        target.MaximumBrightness = 1000f;
                        break;
                    case BitMaskPicture sourceMask:
                        CopyMaskToUShort(sourceMask.r, sourceMask.g, sourceMask.b, target.r, target.g, target.b);
                        FillBrightness(target.Brightness, 1f);
                        target.MaximumBrightness = 1000f;
                        break;
                    default:
                        PopulateFromFallback(source, target);
                        FillBrightness(target.Brightness, 1f);
                        target.MaximumBrightness = 1000f;
                        break;
                }

                target.ProcessStack = [BuildProcessStack(imagePath, target.GetType(), target.BitPerPixel.Value)];
            }
            finally
            {
                source.Dispose();
            }
        }

        private static IPicture LoadSourcePicture(string imagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            PictureFileKind fileKind = DetectFileKind(stream);
            stream.Position = 0;

            return fileKind switch
            {
                PictureFileKind.Png => LoadFromPng(stream, imagePath),
                PictureFileKind.Vfd => LoadFromVfd(stream, imagePath),
                _ => TryLoadByContent(stream, imagePath),
            };
        }

        private static IPicture LoadFromPng(Stream stream, string imagePath)
        {
            if (!PictureExtensions.SharedPngPictureDecoder.TryLoad(stream, out IPicture? picture) || picture is null)
            {
                throw new InvalidDataException($"Unsupported PNG picture: '{imagePath}'.");
            }
            return picture;
        }

        private static IPicture LoadFromVfd(Stream stream, string imagePath)
        {
            if (PictureExtensions.SharedVfdPictureDecoder.TryLoad(stream, out IPicture? picture) && picture is not null)
            {
                return picture;
            }

            throw new InvalidDataException($"Unsupported VFD picture: '{imagePath}'.");
        }

        private static IPicture TryLoadByContent(Stream stream, string imagePath)
        {
            try
            {
                return LoadFromPng(stream, imagePath);
            }
            catch (InvalidDataException)
            {
                stream.Position = 0;
                return LoadFromVfd(stream, imagePath);
            }
        }

        private static PictureFileKind DetectFileKind(Stream stream)
        {
            Span<byte> header = stackalloc byte[8];
            int read = ReadAtLeast(stream, header);
            if (read >= PngSignature.Length && header[..PngSignature.Length].SequenceEqual(PngSignature))
            {
                return PictureFileKind.Png;
            }

            if (read >= VfdSignature.Length && header[..VfdSignature.Length].SequenceEqual(VfdSignature))
            {
                return PictureFileKind.Vfd;
            }

            return PictureFileKind.Unknown;
        }

        private static int ReadAtLeast(Stream stream, Span<byte> destination)
        {
            int totalRead = 0;
            while (!destination.IsEmpty)
            {
                int read = stream.Read(destination);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
                destination = destination.Slice(read);
            }

            return totalRead;
        }

        private static void InitializeCommon(IPicture target, IPicture source, string imagePath)
        {
            target.Width = source.Width;
            target.Height = source.Height;
            target.Pixels = source.Pixels;
            target.Tag = imagePath;
        }

        private static PictureProcessStack BuildProcessStack(string imagePath, Type targetType, int bitPerPixel)
        {
            return new PictureProcessStack
            {
                OperationDisplayName = $"Loaded from file as {bitPerPixel}bpp",
                Operator = targetType,
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "FilePath", imagePath },
                    { "TargetType", targetType.FullName ?? targetType.Name },
                    { "BitPerPixel", bitPerPixel }
                }
            };
        }

        private static void CopyChannels<T>(ReadOnlySpan<T> sourceR, ReadOnlySpan<T> sourceG, ReadOnlySpan<T> sourceB, Span<T> targetR, Span<T> targetG, Span<T> targetB)
            where T : unmanaged
        {
            sourceR.CopyTo(targetR);
            sourceG.CopyTo(targetG);
            sourceB.CopyTo(targetB);
        }

        private static void CopyAlpha(float[]? source, IPicture<byte> target)
        {
            if (source is null)
            {
                target.a = null;
                target.HasAlphaChannel = false;
                return;
            }

            target.a = GC.AllocateUninitializedArray<float>(target.Pixels);
            Array.Copy(source, target.a, target.Pixels);
            target.HasAlphaChannel = true;
        }

        private static void CopyAlpha(float[]? source, IPicture<ushort> target)
        {
            if (source is null)
            {
                target.a = null;
                target.HasAlphaChannel = false;
                return;
            }

            target.a = GC.AllocateUninitializedArray<float>(target.Pixels);
            Array.Copy(source, target.a, target.Pixels);
            target.HasAlphaChannel = true;
        }

        private static void CopyAlpha(float[]? source, MonoChromePicture8bpp target)
        {
            if (source is null)
            {
                target.a = null;
                target.HasAlphaChannel = false;
                return;
            }

            target.a = GC.AllocateUninitializedArray<float>(target.Pixels);
            Array.Copy(source, target.a, target.Pixels);
            target.HasAlphaChannel = true;
        }

        private static void CopyAlphaFromSource(IPicture source, IPicture<byte> target)
        {
            if (source is IPicture<byte> source8 && source8.a is not null)
            {
                CopyAlpha(source8.a, target);
                return;
            }

            if (source is IPicture<ushort> source16 && source16.a is not null)
            {
                CopyAlpha(source16.a, target);
                return;
            }

            if (source is MonoChromePicture8bpp sourceMono && sourceMono.a is not null)
            {
                CopyAlpha(sourceMono.a, target);
                return;
            }

            target.a = null;
            target.HasAlphaChannel = false;
        }

        private static void CopyAlphaFromSource(IPicture source, IPicture<ushort> target)
        {
            if (source is IPicture<ushort> source16 && source16.a is not null)
            {
                CopyAlpha(source16.a, target);
                return;
            }

            if (source is IPicture<byte> source8 && source8.a is not null)
            {
                CopyAlpha(source8.a, target);
                return;
            }

            if (source is MonoChromePicture8bpp sourceMono && sourceMono.a is not null)
            {
                CopyAlpha(sourceMono.a, target);
                return;
            }

            target.a = null;
            target.HasAlphaChannel = false;
        }

        private static void CopyAlphaFromSource(IPicture source, MonoChromePicture8bpp target)
        {
            if (source is IPicture<byte> source8 && source8.a is not null)
            {
                CopyAlpha(source8.a, target);
                return;
            }

            if (source is IPicture<ushort> source16 && source16.a is not null)
            {
                CopyAlpha(source16.a, target);
                return;
            }

            if (source is MonoChromePicture8bpp sourceMono && sourceMono.a is not null)
            {
                CopyAlpha(sourceMono.a, target);
                return;
            }

            target.a = null;
            target.HasAlphaChannel = false;
        }

        private static void CopyMaskToRgb(ReadOnlySpan<bool> sourceR, ReadOnlySpan<bool> sourceG, ReadOnlySpan<bool> sourceB, Span<byte> targetR, Span<byte> targetG, Span<byte> targetB)
        {
            for (int i = 0; i < sourceR.Length; i++)
            {
                targetR[i] = sourceR[i] ? byte.MaxValue : byte.MinValue;
                targetG[i] = sourceG[i] ? byte.MaxValue : byte.MinValue;
                targetB[i] = sourceB[i] ? byte.MaxValue : byte.MinValue;
            }
        }

        private static void CopyMaskToUShort(ReadOnlySpan<bool> sourceR, ReadOnlySpan<bool> sourceG, ReadOnlySpan<bool> sourceB, Span<ushort> targetR, Span<ushort> targetG, Span<ushort> targetB)
        {
            for (int i = 0; i < sourceR.Length; i++)
            {
                targetR[i] = sourceR[i] ? ushort.MaxValue : ushort.MinValue;
                targetG[i] = sourceG[i] ? ushort.MaxValue : ushort.MinValue;
                targetB[i] = sourceB[i] ? ushort.MaxValue : ushort.MinValue;
            }
        }

        private static void CopyMaskToGray(ReadOnlySpan<bool> sourceR, ReadOnlySpan<bool> sourceG, ReadOnlySpan<bool> sourceB, Span<byte> target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = (sourceR[i] || sourceG[i] || sourceB[i]) ? byte.MaxValue : byte.MinValue;
            }
        }

        private static void CopyMonoToRgb(ReadOnlySpan<byte> source, Span<byte> targetR, Span<byte> targetG, Span<byte> targetB)
        {
            for (int i = 0; i < source.Length; i++)
            {
                byte gray = source[i];
                targetR[i] = gray;
                targetG[i] = gray;
                targetB[i] = gray;
            }
        }

        private static void CopyRgbToGray(ReadOnlySpan<byte> sourceR, ReadOnlySpan<byte> sourceG, ReadOnlySpan<byte> sourceB, Span<byte> target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = ToGrayByte(sourceR[i], sourceG[i], sourceB[i]);
            }
        }

        private static void CopyRgbToGray(ReadOnlySpan<ushort> sourceR, ReadOnlySpan<ushort> sourceG, ReadOnlySpan<ushort> sourceB, Span<byte> target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = ToGrayByte(sourceR[i], sourceG[i], sourceB[i]);
            }
        }

        private static void FillBrightness(Span<float> brightness, float value)
        {
            brightness.Fill(value);
        }

        private static byte ToGrayByte(byte r, byte g, byte b)
        {
            return (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
        }

        private static byte ToGrayByte(ushort r, ushort g, ushort b)
        {
            int gray16 = (int)((r * 299L + g * 587L + b * 114L + 500L) / 1000L);
            return (byte)(gray16 / 257);
        }

        private static void PopulateFromFallback(IPicture source, Picture8bpp target)
        {
            var red = source.GetSpecificChannel(IPicture.ChannelId.Red) as byte[];
            var green = source.GetSpecificChannel(IPicture.ChannelId.Green) as byte[];
            var blue = source.GetSpecificChannel(IPicture.ChannelId.Blue) as byte[];
            if (red is not null && green is not null && blue is not null)
            {
                CopyChannels(red, green, blue, target.r, target.g, target.b);
                CopyAlphaFromSource(source, target);
                return;
            }

            var red16 = source.GetSpecificChannel(IPicture.ChannelId.Red) as ushort[];
            var green16 = source.GetSpecificChannel(IPicture.ChannelId.Green) as ushort[];
            var blue16 = source.GetSpecificChannel(IPicture.ChannelId.Blue) as ushort[];
            if (red16 is not null && green16 is not null && blue16 is not null)
            {
                PictureBufferUtilities.ConvertUShortToByte(red16, target.r);
                PictureBufferUtilities.ConvertUShortToByte(green16, target.g);
                PictureBufferUtilities.ConvertUShortToByte(blue16, target.b);
                CopyAlphaFromSource(source, target);
                return;
            }

            throw new InvalidDataException($"Unable to load picture '{source.Tag}' as 8bpp.");
        }

        private static void PopulateFromFallback(IPicture source, Picture16bpp target)
        {
            var red = source.GetSpecificChannel(IPicture.ChannelId.Red) as ushort[];
            var green = source.GetSpecificChannel(IPicture.ChannelId.Green) as ushort[];
            var blue = source.GetSpecificChannel(IPicture.ChannelId.Blue) as ushort[];
            if (red is not null && green is not null && blue is not null)
            {
                CopyChannels(red, green, blue, target.r, target.g, target.b);
                CopyAlphaFromSource(source, target);
                return;
            }

            var red8 = source.GetSpecificChannel(IPicture.ChannelId.Red) as byte[];
            var green8 = source.GetSpecificChannel(IPicture.ChannelId.Green) as byte[];
            var blue8 = source.GetSpecificChannel(IPicture.ChannelId.Blue) as byte[];
            if (red8 is not null && green8 is not null && blue8 is not null)
            {
                PictureBufferUtilities.ConvertByteToUShort(red8, target.r);
                PictureBufferUtilities.ConvertByteToUShort(green8, target.g);
                PictureBufferUtilities.ConvertByteToUShort(blue8, target.b);
                if (source.HasAlphaChannel)
                {
                    CopyAlphaFromSource(source, target);
                }
                return;
            }

            throw new InvalidDataException($"Unable to load picture '{source.Tag}' as 16bpp.");
        }

        private static void PopulateFromFallback(IPicture source, MonoChromePicture8bpp target)
        {
            var gray = source.GetSpecificChannel(IPicture.ChannelId.MonoChrome) as byte[];
            if (gray is not null)
            {
                Array.Copy(gray, target.gray, target.Pixels);
                if (source.HasAlphaChannel)
                {
                    CopyAlphaFromSource(source, target);
                }
                return;
            }

            throw new InvalidDataException($"Unable to load picture '{source.Tag}' as monochrome.");
        }

        private static void PopulateFromFallback(IPicture source, HDRPicture16bpp target)
        {
            PopulateFromFallback(source, (Picture16bpp)target);
            if (source is IHDRPicture<ushort> hdr)
            {
                Array.Copy(hdr.Brightness, target.Brightness, target.Pixels);
                target.MaximumBrightness = hdr.MaximumBrightness;
            }
            else
            {
                FillBrightness(target.Brightness, 1f);
                target.MaximumBrightness = 1000f;
            }
        }

        private enum PictureFileKind
        {
            Unknown,
            Png,
            Vfd
        }
    }
}
