using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Processing.Cropping
{
    public class CPUPictureCropper : IPictureCropper
    {
        public IPicture<ushort> Crop(IPicture<ushort> source, int startX, int startY, int width, int height)
        {
            var sw = Stopwatch.StartNew();
            var safeRect = BuildSafeCropRect(startX, startY, width, height, source.Width, source.Height);
            if (safeRect.X == 0 && safeRect.Y == 0 && safeRect.Width == source.Width && safeRect.Height == source.Height)
                return source;

            var result = new Picture16bpp(safeRect.Width, safeRect.Height);
            CopyChannels(source, result, safeRect);

            result.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
            {
                OperationDisplayName = "Crop (IPicture)",
                Operator = typeof(CPUPictureCropper),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = BuildProperties(startX, startY, width, height, safeRect),
                Elapsed = sw.Elapsed,
            }).ToList();
            return result;
        }

        public IPicture<byte> Crop(IPicture<byte> source, int startX, int startY, int width, int height)
        {
            var sw = Stopwatch.StartNew();
            var safeRect = BuildSafeCropRect(startX, startY, width, height, source.Width, source.Height);
            if (safeRect.X == 0 && safeRect.Y == 0 && safeRect.Width == source.Width && safeRect.Height == source.Height)
                return source;

            var result = new Picture8bpp(safeRect.Width, safeRect.Height);
            CopyChannels(source, result, safeRect);

            result.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
            {
                OperationDisplayName = "Crop (IPicture)",
                Operator = typeof(CPUPictureCropper),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = BuildProperties(startX, startY, width, height, safeRect),
                Elapsed = sw.Elapsed,
            }).ToList();
            return result;
        }

        public IHDRPicture<ushort> Crop(IHDRPicture<ushort> source, int startX, int startY, int width, int height)
        {
            var sw = Stopwatch.StartNew();
            var safeRect = BuildSafeCropRect(startX, startY, width, height, source.Width, source.Height);
            if (safeRect.X == 0 && safeRect.Y == 0 && safeRect.Width == source.Width && safeRect.Height == source.Height)
                return source;

            var result = new HDRPicture16bpp(safeRect.Width, safeRect.Height, allocateArrays: false)
            {
                r = new ushort[safeRect.Width * safeRect.Height],
                g = new ushort[safeRect.Width * safeRect.Height],
                b = new ushort[safeRect.Width * safeRect.Height],
                MaximumBrightness = (source.MaximumBrightness > 0f && float.IsFinite(source.MaximumBrightness))
                    ? source.MaximumBrightness
                    : 1000f,
            };
            CopyChannels(source, result, safeRect);

            if (source.Brightness != null && source.Brightness.Length == source.Pixels)
            {
                result.Brightness = new float[safeRect.Width * safeRect.Height];
                for (int y = 0; y < safeRect.Height; y++)
                {
                    int sourceOffset = (safeRect.Y + y) * source.Width + safeRect.X;
                    int destinationOffset = y * safeRect.Width;
                    Array.Copy(source.Brightness, sourceOffset, result.Brightness, destinationOffset, safeRect.Width);
                }
            }
            else
            {
                result.Brightness = new float[result.Pixels];
            }

            result.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
            {
                OperationDisplayName = "Crop (HDR IPicture)",
                Operator = typeof(CPUPictureCropper),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = BuildProperties(startX, startY, width, height, safeRect),
                Elapsed = sw.Elapsed,
            }).ToList();
            return result;
        }

        private static void CopyChannels(IPicture<ushort> source, IPicture<ushort> destination, CropRect safeRect)
        {
            for (int y = 0; y < safeRect.Height; y++)
            {
                int sourceOffset = (safeRect.Y + y) * source.Width + safeRect.X;
                int destinationOffset = y * safeRect.Width;
                Array.Copy(source.r, sourceOffset, destination.r, destinationOffset, safeRect.Width);
                Array.Copy(source.g, sourceOffset, destination.g, destinationOffset, safeRect.Width);
                Array.Copy(source.b, sourceOffset, destination.b, destinationOffset, safeRect.Width);
            }

            bool keepAlpha = source.HasAlphaChannel && source.a != null && source.a.Length == source.Pixels;
            destination.HasAlphaChannel = keepAlpha;
            destination.a = keepAlpha ? new float[destination.Pixels] : null;
            if (keepAlpha && destination.a != null && source.a != null)
            {
                for (int y = 0; y < safeRect.Height; y++)
                {
                    int sourceOffset = (safeRect.Y + y) * source.Width + safeRect.X;
                    int destinationOffset = y * safeRect.Width;
                    Array.Copy(source.a, sourceOffset, destination.a, destinationOffset, safeRect.Width);
                }
            }
        }

        private static void CopyChannels(IPicture<byte> source, IPicture<byte> destination, CropRect safeRect)
        {
            for (int y = 0; y < safeRect.Height; y++)
            {
                int sourceOffset = (safeRect.Y + y) * source.Width + safeRect.X;
                int destinationOffset = y * safeRect.Width;
                Array.Copy(source.r, sourceOffset, destination.r, destinationOffset, safeRect.Width);
                Array.Copy(source.g, sourceOffset, destination.g, destinationOffset, safeRect.Width);
                Array.Copy(source.b, sourceOffset, destination.b, destinationOffset, safeRect.Width);
            }

            bool keepAlpha = source.HasAlphaChannel && source.a != null && source.a.Length == source.Pixels;
            destination.HasAlphaChannel = keepAlpha;
            destination.a = keepAlpha ? new float[destination.Pixels] : null;
            if (keepAlpha && destination.a != null && source.a != null)
            {
                for (int y = 0; y < safeRect.Height; y++)
                {
                    int sourceOffset = (safeRect.Y + y) * source.Width + safeRect.X;
                    int destinationOffset = y * safeRect.Width;
                    Array.Copy(source.a, sourceOffset, destination.a, destinationOffset, safeRect.Width);
                }
            }
        }

        private static CropRect BuildSafeCropRect(int startX, int startY, int width, int height, int sourceWidth, int sourceHeight)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and Height must be positive");

            int x0 = Math.Max(0, startX);
            int y0 = Math.Max(0, startY);
            int x1 = Math.Min(sourceWidth, startX + width);
            int y1 = Math.Min(sourceHeight, startY + height);
            int safeWidth = x1 - x0;
            int safeHeight = y1 - y0;

            if (safeWidth <= 0 || safeHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(startX), "Crop rectangle must overlap source bounds.");

            return new CropRect(x0, y0, safeWidth, safeHeight);
        }

        private static Dictionary<string, object> BuildProperties(int requestStartX, int requestStartY, int requestWidth, int requestHeight, CropRect safeRect)
        {
            return new Dictionary<string, object>
            {
                { "RequestStartX", requestStartX },
                { "RequestStartY", requestStartY },
                { "RequestWidth", requestWidth },
                { "RequestHeight", requestHeight },
                { "StartX", safeRect.X },
                { "StartY", safeRect.Y },
                { "Width", safeRect.Width },
                { "Height", safeRect.Height },
            };
        }

        private readonly record struct CropRect(int X, int Y, int Width, int Height);
    }
}
