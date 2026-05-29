using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.IO.Hashing;
using System.Text.Json.Serialization;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using static projectFrameCut.Drawing.Base.IPicture;

namespace projectFrameCut.Drawing.Base.Picture
{
    /// <summary>
    /// The 8-bit(byte) Picture structure.
    /// </summary>
    [DebuggerDisplay("{GetDiagnosticsInfo()}")]
    public class Picture8bpp : IPicture<byte>
    {
        [JsonIgnore()]
        public byte[] r { get; set; } = Array.Empty<byte>();
        [JsonIgnore()]
        public byte[] g { get; set; } = Array.Empty<byte>();
        [JsonIgnore()]
        public byte[] b { get; set; } = Array.Empty<byte>();
        [JsonIgnore()]
        [NotNull()]
        public float[]? a { get; set; } = null;
        public int Width { get; set; }
        public int Height { get; set; }
        public int Pixels { get; set; }

        public string Tag { get; set; } = "Unset tag";

        public List<PictureProcessStack> ProcessStack { get; set; }
        public bool Disposed { get; set; } = false;
        public bool CanBeDisposed { get; set; } = true;

        public bool HasAlphaChannel { get; set; } = false;

        public PicturePixelMode BitPerPixel => 8;



        /// <summary>
        /// Initializes a new instance of the Picture class by copying the properties of an existing Picture.
        /// </summary>
        /// <remarks>The new Picture instance shares the same pixel data reference as the source Picture.
        /// Changes to the pixel data in one instance will affect the other.</remarks>
        /// <param name="picture">The Picture instance to copy the width, height, and pixel data from. Cannot be null.</param>
        public Picture8bpp(IPicture<byte> picture, bool copyData = false)
        {
            Width = picture.Width;
            Height = picture.Height;
            Pixels = picture.Pixels;
            if (copyData)
            {
                // Ensure pixel buffers reference the source buffers if present, otherwise allocate
                r = (picture.r != null && picture.r.Length == Pixels) ? picture.r : new byte[Pixels];
                g = (picture.g != null && picture.g.Length == Pixels) ? picture.g : new byte[Pixels];
                b = (picture.b != null && picture.b.Length == Pixels) ? picture.b : new byte[Pixels];

                if (picture.a != null && picture.a.Length == Pixels)
                {
                    a = picture.a;
                    HasAlphaChannel = true;
                }
                else
                {
                    a = null;
                    HasAlphaChannel = false;
                }
            }


            ProcessStack = new List<PictureProcessStack>
            {
                new PictureProcessStack
                {
                    OperationDisplayName = "Create from another",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Properties = new Dictionary<string, object>
                    {
                        { "SourceProcessStack", picture?.ProcessStack! },
                        { "CopyData", copyData }
                    },
                }
            };

            PictureLifecycleTracker.RegisterCreated(this);

        }

        /// <summary>
        /// Initializes a new instance of the Picture8bpp class with the specified width and height.
        /// </summary>
        /// <param name="width">The width of the picture, in pixels. Must be a non-negative integer.</param>
        /// <param name="height">The height of the picture, in pixels. Must be a non-negative integer.</param>
        public Picture8bpp(int width, int height)
            : this(width, height, allocateArrays: true)
        {
        }

        internal Picture8bpp(int width, int height, bool allocateArrays)
        {
            Width = width;
            Height = height;
            Pixels = checked(width * height);

            if (allocateArrays)
            {
                r = new byte[Pixels];
                g = new byte[Pixels];
                b = new byte[Pixels];
            }
            a = null;
            ProcessStack = new List<PictureProcessStack>
            {
                new PictureProcessStack
                {
                    OperationDisplayName = "Created from scratch",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Properties = new Dictionary<string, object>
                    {
                        { "Width", Width },
                        { "Height", Height }
                    },
                }
            };

            PictureLifecycleTracker.RegisterCreated(this);

        }


        /// <summary>
        /// Initializes a new instance of the Picture8bpp class by loading image data from the specified file path.
        /// </summary>
        /// <param name="imagePath">The file path to the image to load.</param>
        [DebuggerNonUserCode()]
        public Picture8bpp(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("imagePath is null or empty", nameof(imagePath));
            PictureFileLoader.LoadInto(this, imagePath);
            PictureLifecycleTracker.RegisterCreated(this);
            ProcessStack ??= new();
        }

        public Picture8bpp SetAlpha(bool haveAlpha)
        {
            lock (this)
            {
                if (haveAlpha == HasAlphaChannel)
                {
                    return this;
                }
                HasAlphaChannel = haveAlpha;
                if (!haveAlpha)
                {
                    a = null;
                }
                else
                {
                    a = PictureBufferUtilities.AllocateFilledArray(Pixels, 1f);
                }
                return this;
            }
        }

        public void EnsureAlpha()
        {
            lock (this)
            {
                if (!HasAlphaChannel || a == null || a.Length != Pixels)
                {
                    a = PictureBufferUtilities.AllocateFilledArray(Pixels, 1f);
                    HasAlphaChannel = true;
                }
            }
        }


        public void EnsureNoAlpha()
        {
            if (HasAlphaChannel || a != null || a?.Length == Pixels)
            {
                a = null;
                HasAlphaChannel = false;
            }
        }

        public static Picture8bpp GenerateSolidColor(int width, int height, byte r, byte g, byte b, float? a)
        {
            var pic = new Picture8bpp(width, height, allocateArrays: false)
            {
                ProcessStack = new List<PictureProcessStack>
                {
                    new PictureProcessStack
                    {
                        OperationDisplayName = "Created solid color",
                        Operator = typeof(Picture8bpp),
                        ProcessingFuncStackTrace = new StackTrace(true),
                        Properties = new Dictionary<string, object>
                        {
                            { "Width", width },
                            { "Height", height },
                            { "R", r },
                            { "G", g },
                            { "B", b },
                            { "A", a ?? 1.0f }
                        },
                    }
                },
            };
            pic.r = PictureBufferUtilities.AllocateFilledArray(pic.Pixels, r);
            pic.g = PictureBufferUtilities.AllocateFilledArray(pic.Pixels, g);
            pic.b = PictureBufferUtilities.AllocateFilledArray(pic.Pixels, b);
            if (a != null)
            {
                pic.a = PictureBufferUtilities.AllocateFilledArray(pic.Pixels, a.Value);
                pic.HasAlphaChannel = true;
            }
            else
            {
                pic.a = null;
                pic.HasAlphaChannel = false;
            }
            return pic;
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
                            Operator = typeof(Picture8bpp),
                            ProcessingFuncStackTrace = new StackTrace(true),
                            Properties = new Dictionary<string, object> { { "Force", force } }
                        });
                    }
                    if (disposing)
                    {
                        r = null!;
                        g = null!;
                        b = null!;
                        a = null;
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

        public IPicture ToBitPerPixel(PicturePixelMode bitPerPixel)
        {
            if (bitPerPixel == PicturePixelMode.BytePicture)
            {
                return this;
            }
            else if (bitPerPixel == PicturePixelMode.UShortPicture)
            {
                var sw = Stopwatch.StartNew();
                var pic = new Picture16bpp(Width, Height, allocateArrays: false)
                {
                    Tag = this.Tag,
                    HasAlphaChannel = this.HasAlphaChannel,
                    ProcessStack = this.ProcessStack
                };


                if (HasAlphaChannel && a != null)
                {
                    pic.a = GC.AllocateUninitializedArray<float>(Pixels);
                    Array.Copy(a, pic.a, Pixels);
                }
                else
                {
                    pic.a = null;
                }

                pic.r = GC.AllocateUninitializedArray<ushort>(Pixels);
                pic.g = GC.AllocateUninitializedArray<ushort>(Pixels);
                pic.b = GC.AllocateUninitializedArray<ushort>(Pixels);
                PictureBufferUtilities.ConvertByteToUShort(r, pic.r);
                PictureBufferUtilities.ConvertByteToUShort(g, pic.g);
                PictureBufferUtilities.ConvertByteToUShort(b, pic.b);
                pic.ProcessStack.Add(new PictureProcessStack
                {
                    OperationDisplayName = "Converted from 8bpp to 16bpp",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Elapsed = sw.Elapsed
                });
                return pic;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(bitPerPixel), "仅支持 8 或 16 bpp。");
            }
        }

        IPicture<byte> IPicture<byte>.SetAlpha(bool haveAlpha)
        {
            return SetAlpha(haveAlpha);
        }

        public ulong GetUniqueID()
        {
            var hash = new XxHash64();
            hash.Append(r.AsSpan());
            hash.Append(g.AsSpan());
            hash.Append(b.AsSpan());
            if (a is not null)
                hash.Append(MemoryMarshal.AsBytes<float>(a.AsSpan()));
            return hash.GetCurrentHashAsUInt64();
        }

        public object? GetSpecificChannel(IPicture.ChannelId channelId)
        {
            return channelId switch
            {
                IPicture.ChannelId.Red => r,
                IPicture.ChannelId.Green => g,
                IPicture.ChannelId.Blue => b,
                IPicture.ChannelId.Alpha => a!,
                _ => throw new ArgumentOutOfRangeException(nameof(channelId), "Invalid channel ID."),
            };
        }

        public string GetDiagnosticsInfo() => $"8BitPerPixel image, Tag: '{Tag}', Size: {Width}*{Height}, avg R:{r.Average(Convert.ToDecimal)} G:{g.Average(Convert.ToDecimal)} B:{b.Average(Convert.ToDecimal)} A:(has:{HasAlphaChannel}){a?.Average(Convert.ToDecimal) ?? -1}";


    }
}
