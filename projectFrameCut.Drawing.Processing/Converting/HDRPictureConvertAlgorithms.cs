using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace projectFrameCut.Drawing.Processing.Converting
{
    public sealed class HDRPictureFormatConverter : IHDRPictureConverter
    {
        public Picture16bpp ToSDR(IHDRPicture<ushort> source, HDRImageDegradeToSDRMode? degradeMode = null)
            => HDRPictureConvertAlgorithms.ConvertHdrToSdr(source, degradeMode, typeof(HDRPictureFormatConverter), "ConvertToSDR");

        public IHDRPicture<ushort> ToHDR(IPicture<ushort> source, float maximumBrightness = 1000f, float defaultBrightness = 1f)
            => HDRPictureConvertAlgorithms.ConvertSdrToHdr(source, defaultBrightness, maximumBrightness, typeof(HDRPictureFormatConverter), "ConvertToHDR");
    }

    internal static class HDRPictureConvertAlgorithms
    {
        private const float DefaultHdrMaximumBrightness = 1000f;
        private const float SdrReferenceNits = 100f;
        private const float ToneMapKnee = 1.5f;
        private const float OutputGamma = 2.2f;
        private const float LumaEpsilon = 1e-6f;

        public static Picture16bpp ConvertHdrToSdr(
            IHDRPicture<ushort> source,
            HDRImageDegradeToSDRMode? degradeMode,
            Type operatorType,
            string operationDisplayName = "DegradeToSDR")
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(operatorType);

            var mode = degradeMode ?? HDRPictureConverter.DefaultHDRImageDegradeToSDRMode;
            if (mode == HDRImageDegradeToSDRMode.DisallowDowngrade)
            {
                throw new InvalidOperationException($"HDR to SDR degrade is disabled. Mode: {mode}.");
            }

            var sw = Stopwatch.StartNew();
            var result = new Picture16bpp(source.Width, source.Height, allocateArrays: false)
            {
                Tag = source.Tag,
                r = GC.AllocateUninitializedArray<ushort>(source.Pixels),
                g = GC.AllocateUninitializedArray<ushort>(source.Pixels),
                b = GC.AllocateUninitializedArray<ushort>(source.Pixels),
                ProcessStack = new List<PictureProcessStack>(source.ProcessStack),
            };

            if (source.HasAlphaChannel && source.a is not null && source.a.Length == source.Pixels)
            {
                result.a = GC.AllocateUninitializedArray<float>(source.Pixels);
                Array.Copy(source.a, result.a, source.Pixels);
                result.HasAlphaChannel = true;
            }
            else
            {
                result.a = null;
                result.HasAlphaChannel = false;
            }

            float[]? brightness = source.Brightness;
            bool hasBrightness = brightness is not null && brightness.Length == source.Pixels;
            float validMaximumBrightness = source.MaximumBrightness > 0f && float.IsFinite(source.MaximumBrightness)
                ? source.MaximumBrightness
                : SdrReferenceNits;

            for (int i = 0; i < source.Pixels; i++)
            {
                if (!hasBrightness || mode == HDRImageDegradeToSDRMode.DiscardBrightnessChannel)
                {
                    result.r[i] = source.r[i];
                    result.g[i] = source.g[i];
                    result.b[i] = source.b[i];
                    continue;
                }

                float pixelBrightness = brightness![i];
                switch (mode)
                {
                    case HDRImageDegradeToSDRMode.NormalizeBrightnessToRGB:
                        MapHdrToSdr(
                            source.r[i], source.g[i], source.b[i], pixelBrightness, validMaximumBrightness,
                            out var mappedR, out var mappedG, out var mappedB);
                        result.r[i] = mappedR;
                        result.g[i] = mappedG;
                        result.b[i] = mappedB;
                        break;

                    case HDRImageDegradeToSDRMode.OverlayMaskFromBrightness:
                        float mask = Math.Clamp(pixelBrightness, 0f, 1f);
                        result.r[i] = (ushort)Math.Clamp((int)Math.Round(source.r[i] * mask), 0, 65535);
                        result.g[i] = (ushort)Math.Clamp((int)Math.Round(source.g[i] * mask), 0, 65535);
                        result.b[i] = (ushort)Math.Clamp((int)Math.Round(source.b[i] * mask), 0, 65535);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown HDR degrade mode.");
                }
            }

            result.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = operationDisplayName,
                Operator = operatorType,
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "Mode", mode.ToString() },
                    { "MaximumBrightness", source.MaximumBrightness },
                },
                Elapsed = sw.Elapsed,
            });

            return result;
        }

        public static HDRPicture16bpp ConvertSdrToHdr(
            IPicture<ushort> source,
            float defaultBrightness = 1f,
            float maximumBrightness = DefaultHdrMaximumBrightness,
            Type? operatorType = null,
            string operationDisplayName = "ConvertToHDR")
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source is HDRPicture16bpp hdrSource)
            {
                return hdrSource;
            }

            int pixels = source.Pixels;
            float validBrightness = float.IsFinite(defaultBrightness) ? Math.Clamp(defaultBrightness, 0f, 1f) : 1f;
            float validMaximumBrightness = maximumBrightness > 0f && float.IsFinite(maximumBrightness)
                ? maximumBrightness
                : DefaultHdrMaximumBrightness;

            var result = new HDRPicture16bpp(source.Width, source.Height, allocateArrays: false)
            {
                r = GC.AllocateUninitializedArray<ushort>(pixels),
                g = GC.AllocateUninitializedArray<ushort>(pixels),
                b = GC.AllocateUninitializedArray<ushort>(pixels),
                Brightness = new float[pixels],
                MaximumBrightness = validMaximumBrightness,
                HasAlphaChannel = source.HasAlphaChannel && source.a is not null && source.a.Length == pixels,
                a = null,
                Tag = source.Tag,
                ProcessStack = new List<PictureProcessStack>(source.ProcessStack),
            };

            Array.Fill(result.Brightness, validBrightness);
            Array.Copy(source.r, result.r, pixels);
            Array.Copy(source.g, result.g, pixels);
            Array.Copy(source.b, result.b, pixels);

            if (result.HasAlphaChannel && source.a is not null)
            {
                result.a = GC.AllocateUninitializedArray<float>(pixels);
                Array.Copy(source.a, result.a, pixels);
            }

            result.ProcessStack.Add(new PictureProcessStack
            {
                OperationDisplayName = operationDisplayName,
                Operator = operatorType ?? typeof(HDRPictureConvertAlgorithms),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "MaximumBrightness", validMaximumBrightness },
                    { "DefaultBrightness", validBrightness },
                },
            });

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MapHdrToSdr(
            ushort sourceR, ushort sourceG, ushort sourceB,
            float brightness, float maximumBrightness,
            out ushort mappedR, out ushort mappedG, out ushort mappedB)
        {
            if (!float.IsFinite(brightness))
            {
                mappedR = sourceR;
                mappedG = sourceG;
                mappedB = sourceB;
                return;
            }

            float r = sourceR / 65535f;
            float g = sourceG / 65535f;
            float b = sourceB / 65535f;
            float sourceSignalLuma = Math.Clamp(0.2627f * r + 0.6780f * g + 0.0593f * b, 0f, 1f);
            if (sourceSignalLuma <= LumaEpsilon)
            {
                mappedR = sourceR;
                mappedG = sourceG;
                mappedB = sourceB;
                return;
            }

            float relativeToSdrWhite = Math.Max(0f, brightness) * (maximumBrightness / SdrReferenceNits);
            float toneMappedLinearLuma = (relativeToSdrWhite * ToneMapKnee) / (1f + relativeToSdrWhite * ToneMapKnee);
            toneMappedLinearLuma = Math.Clamp(toneMappedLinearLuma, 0f, 1f);
            float targetSignalLuma = MathF.Pow(toneMappedLinearLuma, 1f / OutputGamma);
            float gain = targetSignalLuma / sourceSignalLuma;

            r = Math.Clamp(r * gain, 0f, 1f);
            g = Math.Clamp(g * gain, 0f, 1f);
            b = Math.Clamp(b * gain, 0f, 1f);

            mappedR = (ushort)Math.Clamp((int)Math.Round(r * 65535f), 0, 65535);
            mappedG = (ushort)Math.Clamp((int)Math.Round(g * 65535f), 0, 65535);
            mappedB = (ushort)Math.Clamp((int)Math.Round(b * 65535f), 0, 65535);
        }
    }
}
