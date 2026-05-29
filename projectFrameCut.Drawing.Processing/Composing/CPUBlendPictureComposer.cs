using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Processing.Composing
{
    public class CPUBlendPictureComposer : IPictureComposer
    {
        private const float Epsilon = 1e-6f;
        private const float DefaultHdrMaximumBrightness = 1000f;

        public IPicture<ushort> Compose(IPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode)
            => Compose(basePicture, topPicture, blendMode, 0, 0, basePicture.Width, basePicture.Height);

        public IPicture<byte> Compose(IPicture<byte> basePicture, IPicture<byte> topPicture, BlendMode blendMode)
            => Compose(basePicture, topPicture, blendMode, 0, 0, basePicture.Width, basePicture.Height);

        public IHDRPicture<ushort> Compose(IHDRPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode)
            => Compose(basePicture, topPicture, blendMode, 0, 0, basePicture.Width, basePicture.Height);

        public IPicture<ushort> Compose(IPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight)
        {
            ArgumentNullException.ThrowIfNull(basePicture);
            ArgumentNullException.ThrowIfNull(topPicture);
            ValidateTarget(targetWidth, targetHeight);

            var sw = Stopwatch.StartNew();
            int targetPixels = checked(targetWidth * targetHeight);
            var outR = new ushort[targetPixels];
            var outG = new ushort[targetPixels];
            var outB = new ushort[targetPixels];
            var outA = new float[targetPixels];

            InitializeBaseLayer(basePicture, targetWidth, targetHeight, outR, outG, outB, outA);
            ComposeOverlap(basePicture, topPicture, blendMode, topStartX, topStartY, targetWidth, targetHeight, 65535f, outR, outG, outB, outA, null, null, null);

            bool outputHasAlpha = basePicture.HasAlphaChannel || topPicture.HasAlphaChannel || basePicture.Width != targetWidth || basePicture.Height != targetHeight;
            var result = new Picture16bpp(targetWidth, targetHeight)
            {
                r = outR,
                g = outG,
                b = outB,
                a = outputHasAlpha ? outA : null,
                HasAlphaChannel = outputHasAlpha,
                ProcessStack = new List<PictureProcessStack>(basePicture.ProcessStack)
            };

            result.ProcessStack.Add(BuildProcessStack(blendMode, topStartX, topStartY, targetWidth, targetHeight, sw.Elapsed));
            return result;
        }

        public IPicture<byte> Compose(IPicture<byte> basePicture, IPicture<byte> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight)
        {
            ArgumentNullException.ThrowIfNull(basePicture);
            ArgumentNullException.ThrowIfNull(topPicture);
            ValidateTarget(targetWidth, targetHeight);

            var sw = Stopwatch.StartNew();
            int targetPixels = checked(targetWidth * targetHeight);
            var outR = new byte[targetPixels];
            var outG = new byte[targetPixels];
            var outB = new byte[targetPixels];
            var outA = new float[targetPixels];

            InitializeBaseLayer(basePicture, targetWidth, targetHeight, outR, outG, outB, outA);
            ComposeOverlap(basePicture, topPicture, blendMode, topStartX, topStartY, targetWidth, targetHeight, 255f, outR, outG, outB, outA, null, null, null);

            bool outputHasAlpha = basePicture.HasAlphaChannel || topPicture.HasAlphaChannel || basePicture.Width != targetWidth || basePicture.Height != targetHeight;
            var result = new Picture8bpp(targetWidth, targetHeight)
            {
                r = outR,
                g = outG,
                b = outB,
                a = outputHasAlpha ? outA : null,
                HasAlphaChannel = outputHasAlpha,
                ProcessStack = new List<PictureProcessStack>(basePicture.ProcessStack)
            };

            result.ProcessStack.Add(BuildProcessStack(blendMode, topStartX, topStartY, targetWidth, targetHeight, sw.Elapsed));
            return result;
        }

        public IHDRPicture<ushort> Compose(IHDRPicture<ushort> basePicture, IPicture<ushort> topPicture, BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight)
        {
            ArgumentNullException.ThrowIfNull(basePicture);
            ArgumentNullException.ThrowIfNull(topPicture);
            ValidateTarget(targetWidth, targetHeight);

            var sw = Stopwatch.StartNew();
            int targetPixels = checked(targetWidth * targetHeight);
            var outR = new ushort[targetPixels];
            var outG = new ushort[targetPixels];
            var outB = new ushort[targetPixels];
            var outA = new float[targetPixels];
            var outBrightness = new float[targetPixels];

            InitializeBaseLayer(basePicture, targetWidth, targetHeight, outR, outG, outB, outA);
            InitializeBaseBrightness(basePicture, targetWidth, targetHeight, outBrightness);

            float[]? topBrightness = (topPicture is IHDRPicture<ushort> topHdr && topHdr.Brightness != null && topHdr.Brightness.Length == topPicture.Pixels)
                ? topHdr.Brightness
                : null;

            ComposeOverlap(basePicture, topPicture, blendMode, topStartX, topStartY, targetWidth, targetHeight, 65535f, outR, outG, outB, outA, outBrightness, basePicture.Brightness, topBrightness);

            bool outputHasAlpha = basePicture.HasAlphaChannel || topPicture.HasAlphaChannel || basePicture.Width != targetWidth || basePicture.Height != targetHeight;
            float outputMaximumBrightness = DefaultHdrMaximumBrightness;
            if (basePicture.MaximumBrightness > 0f && float.IsFinite(basePicture.MaximumBrightness))
                outputMaximumBrightness = basePicture.MaximumBrightness;
            if (topPicture is IHDRPicture<ushort> topHdrPicture && topHdrPicture.MaximumBrightness > 0f && float.IsFinite(topHdrPicture.MaximumBrightness))
                outputMaximumBrightness = Math.Max(outputMaximumBrightness, topHdrPicture.MaximumBrightness);

            var result = new HDRPicture16bpp(targetWidth, targetHeight, allocateArrays: false)
            {
                r = outR,
                g = outG,
                b = outB,
                a = outputHasAlpha ? outA : null,
                HasAlphaChannel = outputHasAlpha,
                Brightness = outBrightness,
                MaximumBrightness = outputMaximumBrightness,
                ProcessStack = new List<PictureProcessStack>(basePicture.ProcessStack)
            };

            result.ProcessStack.Add(BuildProcessStack(blendMode, topStartX, topStartY, targetWidth, targetHeight, sw.Elapsed));
            return result;
        }

        private static void ComposeOverlap<T>(
            IPicture<T> basePicture,
            IPicture<T> topPicture,
            BlendMode blendMode,
            int topStartX,
            int topStartY,
            int targetWidth,
            int targetHeight,
            float maxChannel,
            T[] outR,
            T[] outG,
            T[] outB,
            float[] outA,
            float[]? outBrightness,
            float[]? baseBrightness,
            float[]? topBrightness) where T : unmanaged
        {
            int overlapLeft = Math.Max(0, topStartX);
            int overlapTop = Math.Max(0, topStartY);
            int overlapRight = Math.Min(targetWidth, topStartX + topPicture.Width);
            int overlapBottom = Math.Min(targetHeight, topStartY + topPicture.Height);

            bool hasTopAlpha = topPicture.HasAlphaChannel && topPicture.a != null && topPicture.a.Length == topPicture.Pixels;
            bool outputBrightness = outBrightness != null;

            for (int y = overlapTop; y < overlapBottom; y++)
            {
                int topY = y - topStartY;
                int topRow = topY * topPicture.Width;
                int dstRow = y * targetWidth;

                for (int x = overlapLeft; x < overlapRight; x++)
                {
                    int topX = x - topStartX;
                    int topIdx = topRow + topX;
                    int dstIdx = dstRow + x;
                    float topAlpha = hasTopAlpha ? Clamp01(topPicture.a![topIdx]) : 1f;
                    if (topAlpha <= 0f)
                        continue;

                    float baseAlpha = outA[dstIdx];
                    if (topAlpha >= 0.999f)
                    {
                        outR[dstIdx] = topPicture.r[topIdx];
                        outG[dstIdx] = topPicture.g[topIdx];
                        outB[dstIdx] = topPicture.b[topIdx];
                        outA[dstIdx] = 1f;
                        if (outputBrightness)
                        {
                            float topBrightnessValue = topBrightness != null ? Clamp01(topBrightness[topIdx]) : EstimateBrightness(ToFloat(topPicture.r[topIdx]), ToFloat(topPicture.g[topIdx]), ToFloat(topPicture.b[topIdx]), maxChannel);
                            outBrightness![dstIdx] = topBrightnessValue;
                        }
                        continue;
                    }

                    float topR = ToFloat(topPicture.r[topIdx]);
                    float topG = ToFloat(topPicture.g[topIdx]);
                    float topB = ToFloat(topPicture.b[topIdx]);
                    float baseR = ToFloat(outR[dstIdx]);
                    float baseG = ToFloat(outG[dstIdx]);
                    float baseB = ToFloat(outB[dstIdx]);

                    ComposeChannel(blendMode, topR, baseR, topAlpha, baseAlpha, maxChannel, out float composedR, out float composedAlpha);
                    ComposeChannel(blendMode, topG, baseG, topAlpha, baseAlpha, maxChannel, out float composedG, out _);
                    ComposeChannel(blendMode, topB, baseB, topAlpha, baseAlpha, maxChannel, out float composedB, out _);

                    outR[dstIdx] = FromFloat<T>(composedR, maxChannel);
                    outG[dstIdx] = FromFloat<T>(composedG, maxChannel);
                    outB[dstIdx] = FromFloat<T>(composedB, maxChannel);
                    outA[dstIdx] = composedAlpha;

                    if (outputBrightness)
                    {
                        float topBrightnessValue = topBrightness != null ? Clamp01(topBrightness[topIdx]) : EstimateBrightness(topR, topG, topB, maxChannel);
                        float baseBrightnessValue = baseBrightness != null && dstIdx < baseBrightness.Length
                            ? Clamp01(baseBrightness[dstIdx])
                            : outBrightness![dstIdx];
                        ComposeChannel(blendMode, topBrightnessValue, baseBrightnessValue, topAlpha, baseAlpha, 1f, out float composedBrightness, out _);
                        outBrightness![dstIdx] = Clamp01(composedBrightness);
                    }
                }
            }
        }

        private static void InitializeBaseLayer(IPicture<ushort> source, int targetWidth, int targetHeight, ushort[] outR, ushort[] outG, ushort[] outB, float[] outA)
        {
            bool hasAlpha = source.HasAlphaChannel && source.a != null && source.a.Length == source.Pixels;
            for (int y = 0; y < targetHeight; y++)
            {
                int dstRow = y * targetWidth;
                if (y < 0 || y >= source.Height)
                {
                    ClearRow(outR, outG, outB, outA, dstRow, targetWidth);
                    continue;
                }

                for (int x = 0; x < targetWidth; x++)
                {
                    int dstIdx = dstRow + x;
                    if (x >= source.Width)
                    {
                        outR[dstIdx] = 0;
                        outG[dstIdx] = 0;
                        outB[dstIdx] = 0;
                        outA[dstIdx] = 0f;
                        continue;
                    }

                    int srcIdx = y * source.Width + x;
                    outR[dstIdx] = source.r[srcIdx];
                    outG[dstIdx] = source.g[srcIdx];
                    outB[dstIdx] = source.b[srcIdx];
                    outA[dstIdx] = hasAlpha ? Clamp01(source.a![srcIdx]) : 1f;
                }
            }
        }

        private static void InitializeBaseLayer(IPicture<byte> source, int targetWidth, int targetHeight, byte[] outR, byte[] outG, byte[] outB, float[] outA)
        {
            bool hasAlpha = source.HasAlphaChannel && source.a != null && source.a.Length == source.Pixels;
            for (int y = 0; y < targetHeight; y++)
            {
                int dstRow = y * targetWidth;
                if (y < 0 || y >= source.Height)
                {
                    ClearRow(outR, outG, outB, outA, dstRow, targetWidth);
                    continue;
                }

                for (int x = 0; x < targetWidth; x++)
                {
                    int dstIdx = dstRow + x;
                    if (x >= source.Width)
                    {
                        outR[dstIdx] = 0;
                        outG[dstIdx] = 0;
                        outB[dstIdx] = 0;
                        outA[dstIdx] = 0f;
                        continue;
                    }

                    int srcIdx = y * source.Width + x;
                    outR[dstIdx] = source.r[srcIdx];
                    outG[dstIdx] = source.g[srcIdx];
                    outB[dstIdx] = source.b[srcIdx];
                    outA[dstIdx] = hasAlpha ? Clamp01(source.a![srcIdx]) : 1f;
                }
            }
        }

        private static void InitializeBaseBrightness(IHDRPicture<ushort> source, int targetWidth, int targetHeight, float[] outBrightness)
        {
            bool hasBrightness = source.Brightness != null && source.Brightness.Length == source.Pixels;
            for (int y = 0; y < targetHeight; y++)
            {
                int dstRow = y * targetWidth;
                for (int x = 0; x < targetWidth; x++)
                {
                    int dstIdx = dstRow + x;
                    if (y >= source.Height || x >= source.Width)
                    {
                        outBrightness[dstIdx] = 0f;
                        continue;
                    }

                    int srcIdx = y * source.Width + x;
                    outBrightness[dstIdx] = hasBrightness
                        ? Clamp01(source.Brightness![srcIdx])
                        : EstimateBrightness(source.r[srcIdx], source.g[srcIdx], source.b[srcIdx], 65535f);
                }
            }
        }

        private static void ComposeChannel(BlendMode blendMode, float top, float @base, float topAlpha, float baseAlpha, float maxChannel, out float outColor, out float outAlpha)
        {
            outAlpha = topAlpha + baseAlpha * (1f - topAlpha);
            if (outAlpha < Epsilon)
            {
                outColor = 0f;
                outAlpha = 0f;
                return;
            }

            if (blendMode == BlendMode.Overlay)
            {
                float topContribution = top * topAlpha / outAlpha;
                float baseContribution = @base * baseAlpha * (1f - topAlpha) / outAlpha;
                outColor = Math.Clamp(topContribution + baseContribution, 0f, maxChannel);
                return;
            }

            float blended = blendMode switch
            {
                BlendMode.Add => Math.Min(top + @base, maxChannel),
                BlendMode.Subtract => Math.Max(@base - top, 0f),
                BlendMode.Multiply => top * @base / maxChannel,
                BlendMode.Screen => maxChannel - (maxChannel - top) * (maxChannel - @base) / maxChannel,
                BlendMode.OverlayBlend => @base < (maxChannel * 0.5f)
                    ? 2f * top * @base / maxChannel
                    : maxChannel - 2f * (maxChannel - top) * (maxChannel - @base) / maxChannel,
                BlendMode.Darken => Math.Min(top, @base),
                BlendMode.Lighten => Math.Max(top, @base),
                BlendMode.Difference => Math.Abs(top - @base),
                _ => throw new ArgumentOutOfRangeException(nameof(blendMode), blendMode, "Unsupported blend mode.")
            };

            float result = (blended * topAlpha + @base * baseAlpha * (1f - topAlpha)) / outAlpha;
            outColor = Math.Clamp(result, 0f, maxChannel);
        }

        private static PictureProcessStack BuildProcessStack(BlendMode blendMode, int topStartX, int topStartY, int targetWidth, int targetHeight, TimeSpan elapsed)
        {
            return new PictureProcessStack
            {
                OperationDisplayName = "Compose (CPU Blend)",
                Operator = typeof(CPUBlendPictureComposer),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "BlendMode", blendMode.ToString() },
                    { "TopStartX", topStartX },
                    { "TopStartY", topStartY },
                    { "TargetWidth", targetWidth },
                    { "TargetHeight", targetHeight },
                },
                Elapsed = elapsed
            };
        }

        private static void ValidateTarget(int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
                throw new ArgumentException("targetWidth and targetHeight must be positive.");
        }

        private static float Clamp01(float value)
        {
            if (!float.IsFinite(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float EstimateBrightness(float r, float g, float b, float maxChannel)
            => Clamp01((0.2627f * r + 0.6780f * g + 0.0593f * b) / maxChannel);

        private static float ToFloat<T>(T value) where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
                return (byte)(object)value;
            if (typeof(T) == typeof(ushort))
                return (ushort)(object)value;
            throw new NotSupportedException($"Unsupported channel type: {typeof(T).FullName}");
        }

        private static T FromFloat<T>(float value, float max) where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
                return (T)(object)(byte)Math.Clamp(value, 0f, max);
            if (typeof(T) == typeof(ushort))
                return (T)(object)(ushort)Math.Clamp(value, 0f, max);
            throw new NotSupportedException($"Unsupported channel type: {typeof(T).FullName}");
        }

        private static void ClearRow(byte[] outR, byte[] outG, byte[] outB, float[] outA, int rowOffset, int count)
        {
            Array.Clear(outR, rowOffset, count);
            Array.Clear(outG, rowOffset, count);
            Array.Clear(outB, rowOffset, count);
            Array.Clear(outA, rowOffset, count);
        }

        private static void ClearRow(ushort[] outR, ushort[] outG, ushort[] outB, float[] outA, int rowOffset, int count)
        {
            Array.Clear(outR, rowOffset, count);
            Array.Clear(outG, rowOffset, count);
            Array.Clear(outB, rowOffset, count);
            Array.Clear(outA, rowOffset, count);
        }
    }
}
