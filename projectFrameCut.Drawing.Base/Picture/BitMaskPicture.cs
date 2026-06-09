using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Hashing;
using System.Text.Json.Serialization;
using static projectFrameCut.Drawing.Base.IPicture;

namespace projectFrameCut.Drawing.Base.Picture
{
    /// <summary>A 1-bit per pixel picture using boolean channels, typically used as a mask.</summary>
    [DebuggerDisplay("{GetDiagnosticsInfo()}")]
    public class BitMaskPicture : INoAlphaPicture<bool>
    {
        [JsonIgnore()]
        public bool[] r { get; set; } = Array.Empty<bool>();
        [JsonIgnore()]
        public bool[] g { get; set; } = Array.Empty<bool>();
        [JsonIgnore()]
        public bool[] b { get; set; } = Array.Empty<bool>();

        public PicturePixelMode BitPerPixel => 1;

        public int Width { get; set; }
        public int Height { get; set; }
        public int Pixels { get; set; }
        public string Tag { get; set; } = "Unset tag";

        public List<PictureProcessStack> ProcessStack { get; set; }
        public bool HasAlphaChannel { get; set; }
        public bool Disposed { get; set; } = false;
        public bool CanBeDisposed { get; set; } = true;

        public BitMaskPicture()
        {
            ProcessStack = new List<PictureProcessStack>();
            PictureLifecycleTracker.RegisterCreated(this);
        }

        public string GetDiagnosticsInfo() => $"BitMaskPicture image, Tag: '{Tag}', Size: {Width}*{Height}, avg R:{r.Average(v => v ? 1 : 0)} G:{g.Average(v => v ? 1 : 0)} B:{b.Average(v => v ? 1 : 0)}";


        public ulong GetUniqueID()
        {
            var hash = new XxHash64();
            hash.Append(MemoryMarshal.AsBytes<bool>(r.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<bool>(g.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<bool>(b.AsSpan()));
            return hash.GetCurrentHashAsUInt64();
        }

        public object? GetSpecificChannel(IPicture.ChannelId channelId)
        {
            return channelId switch
            {
                IPicture.ChannelId.Red => r,
                IPicture.ChannelId.Green => g,
                IPicture.ChannelId.Blue => b,
                _ => throw new ArgumentOutOfRangeException(nameof(channelId), "Invalid channel ID."),
            };
        }

        public IPicture<bool> Resize(int targetWidth, int targetHeight, bool preserveAspect = true)
        {
            throw new NotImplementedException();
        }

        public IPicture<bool> SetAlpha(bool haveAlpha)
        {
            throw new NotSupportedException($"Setting alpha channel is not supported for BitMaskPicture.");
        }

        public IPicture ToBitPerPixel(PicturePixelMode bitPerPixel)
        {
            if (bitPerPixel.Value == 1) return this;
            else return ToNormalPicture().ToBitPerPixel(bitPerPixel);
        }

        public Picture8bpp ToNormalPicture()
        {
            var result = new Picture8bpp(Width, Height, allocateArrays: false)
            {
                a = null,
                HasAlphaChannel = false,
                Width = Width,
                Height = Height,
                Pixels = Pixels,
                ProcessStack = ProcessStack.Append(new PictureProcessStack
                {
                    OperationDisplayName = "Converted from BitGrayscalePicture",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                }).ToList()

            };
            result.r = GC.AllocateUninitializedArray<byte>(Pixels);
            result.g = GC.AllocateUninitializedArray<byte>(Pixels);
            result.b = GC.AllocateUninitializedArray<byte>(Pixels);
            PictureBufferUtilities.ConvertBoolMaskToBytes(r, result.r);
            PictureBufferUtilities.ConvertBoolMaskToBytes(g, result.g);
            PictureBufferUtilities.ConvertBoolMaskToBytes(b, result.b);
            return result;
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing, bool force = false, bool appendDisposedProcessStack = true)
        {
            if (!force && (disposedValue || !CanBeDisposed)) return;
            lock (this)
            {
                if (!disposedValue)
                {
                    if (appendDisposedProcessStack)
                    {
                        ProcessStack.Add(new PictureProcessStack
                        {
                            OperationDisplayName = "Disposed",
                            Operator = typeof(BitMaskPicture),
                            ProcessingFuncStackTrace = new StackTrace(true),
                            Properties = new Dictionary<string, object> { { "Force", force } }
                        });
                    }
                    if (disposing)
                    {
                        r = null!;
                        g = null!;
                        b = null!;
                    }

                    disposedValue = true;
                    PictureLifecycleTracker.MarkDisposed(this);
                }
                Disposed = disposedValue;

            }
        }

        public void Dispose() => Dispose(force: false);

        public void Dispose(bool force = false, bool appendDisposedProcessStack = true)
        {
            Dispose(disposing: true, force, appendDisposedProcessStack);
            GC.SuppressFinalize(this);
        }
    }
}
