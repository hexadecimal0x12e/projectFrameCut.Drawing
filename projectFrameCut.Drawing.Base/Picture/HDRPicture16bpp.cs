using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace projectFrameCut.Drawing.Base.Picture
{


    public class HDRPicture16bpp : Picture16bpp, IHDRPicture<ushort>
    {
        private const float DefaultHdrMaximumBrightness = 1000f;

        public HDRPicture16bpp(string imagePath) : base(imagePath)
        {
        }

        public HDRPicture16bpp(IPicture<ushort> picture, bool copyData = false) : base(picture, copyData)
        {
        }

        public HDRPicture16bpp(int width, int height) : base(width, height)
        {
            Brightness = new float[Pixels];
        }

        /// <summary>
        /// Subclass constructor that can skip base-class array allocation to avoid double-allocation
        /// when arrays will be set immediately after construction.
        /// </summary>
        internal HDRPicture16bpp(int width, int height, bool allocateArrays) : base(width, height, allocateArrays)
        {
        }

        public float[] Brightness { get; set; } = Array.Empty<float>();
        public float MaximumBrightness { get; set; } = DefaultHdrMaximumBrightness;

        public HDRPicture16bpp SetBrightnessOffset(double offset)
        {
            float[] brightness = GC.AllocateUninitializedArray<float>(Brightness.Length);
            PictureBufferUtilities.ClampBrightness(Brightness, brightness, offset);
            Brightness = brightness;
            return this;
        }


        public static HDRPicture16bpp GenerateSolidColor(int width, int height, ushort r, ushort g, ushort b, float? a, float brightness = 1f, float maximumBrightness = DefaultHdrMaximumBrightness)
        {
            int pixels = checked(width * height);
            float validBrightness = float.IsFinite(brightness) ? Math.Clamp(brightness, 0f, 1f) : 0f;
            float validMaximumBrightness = (maximumBrightness > 0f && float.IsFinite(maximumBrightness))
                ? maximumBrightness
                : DefaultHdrMaximumBrightness;

            var pic = new HDRPicture16bpp(width, height, allocateArrays: false)
            {
                ProcessStack = new List<PictureProcessStack>
                {
                    new PictureProcessStack
                    {
                        OperationDisplayName = "GenerateSolidColor (HDR)",
                        Operator = typeof(HDRPicture16bpp),
                        ProcessingFuncStackTrace = new StackTrace(true),
                        Properties = new Dictionary<string, object>
                        {
                            { "Width", width },
                            { "Height", height },
                            { "R", r },
                            { "G", g },
                            { "B", b },
                            { "A", a ?? -1f },
                            { "Brightness", validBrightness },
                            { "MaximumBrightness", validMaximumBrightness },
                        },
                    }
                },
                MaximumBrightness = validMaximumBrightness,
                Brightness = PictureBufferUtilities.AllocateFilledArray(pixels, validBrightness),
            };

            pic.r = PictureBufferUtilities.AllocateFilledArray(pixels, r);
            pic.g = PictureBufferUtilities.AllocateFilledArray(pixels, g);
            pic.b = PictureBufferUtilities.AllocateFilledArray(pixels, b);

            if (a != null)
            {
                pic.a = PictureBufferUtilities.AllocateFilledArray(pixels, Math.Clamp(a.Value, 0f, 1f));
                pic.HasAlphaChannel = true;
            }
            else
            {
                pic.a = null;
                pic.HasAlphaChannel = false;
            }

            return pic;
        }

        public new string GetDiagnosticsInfo() => $"HDR image, Size: {Width}*{Height}, avg R:{r.Average(Convert.ToDecimal)} G:{g.Average(Convert.ToDecimal)} B:{b.Average(Convert.ToDecimal)} A:(has:{HasAlphaChannel}){a?.Average(Convert.ToDecimal) ?? -1} L:{Brightness.Average()}(0..1), {Brightness.Average() * MaximumBrightness}nit";

        public new ulong GetUniqueID()
        {
            var hash = new XxHash64();
            hash.Append(MemoryMarshal.AsBytes<ushort>(r.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<ushort>(g.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<ushort>(b.AsSpan()));
            if (a is not null)
                hash.Append(MemoryMarshal.AsBytes<float>(a.AsSpan()));
            if (Brightness is not null)
                hash.Append(MemoryMarshal.AsBytes<float>(Brightness.AsSpan()));
            return hash.GetCurrentHashAsUInt64();
        }

    }
}
