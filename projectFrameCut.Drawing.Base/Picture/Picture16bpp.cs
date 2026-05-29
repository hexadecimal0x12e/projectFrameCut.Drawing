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
    /// The 16-bit(ushort) Picture structure.
    /// </summary>
    [DebuggerDisplay("{GetDiagnosticsInfo()}")]
    public class Picture16bpp : IPicture<ushort>
    {
        [JsonIgnore()]
        public ushort[] r { get; set; } = Array.Empty<ushort>();
        [JsonIgnore()]
        public ushort[] g { get; set; } = Array.Empty<ushort>();
        [JsonIgnore()]
        public ushort[] b { get; set; } = Array.Empty<ushort>();
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

        public PicturePixelMode BitPerPixel => 16;
        /// <summary>
        /// Initializes a new instance of the Picture class by copying the properties of an existing Picture.
        /// </summary>
        /// <remarks>The new Picture instance shares the same pixel data reference as the source Picture.
        /// Changes to the pixel data in one instance will affect the other.</remarks>
        /// <param name="picture">The Picture instance to copy the width, height, and pixel data from. Cannot be null.</param>
        public Picture16bpp(IPicture<ushort> picture, bool copyData = false)
        {
            Width = picture.Width;
            Height = picture.Height;
            Pixels = picture.Pixels;
            if (copyData)
            {
                // Ensure pixel buffers reference the source buffers if present, otherwise allocate
                r = (picture.r != null && picture.r.Length == Pixels) ? picture.r : new ushort[Pixels];
                g = (picture.g != null && picture.g.Length == Pixels) ? picture.g : new ushort[Pixels];
                b = (picture.b != null && picture.b.Length == Pixels) ? picture.b : new ushort[Pixels];

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


            ProcessStack = [new PictureProcessStack
            {
                OperationDisplayName = "Create from another",
                Operator = this.GetType(),
                ProcessingFuncStackTrace = new StackTrace(true),
                Properties = new Dictionary<string, object>
                {
                    { "SourceProcessStack", picture?.ProcessStack!  },
                    { "CopyData", copyData }
                },
            }];

            PictureLifecycleTracker.RegisterCreated(this);
        }

        /// <summary>
        /// Initializes a new instance of the Picture class with the specified width and height.
        /// </summary>
        /// <param name="width">The width of the picture, in pixels. Must be a non-negative integer.</param>
        /// <param name="height">The height of the picture, in pixels. Must be a non-negative integer.</param>
        public Picture16bpp(int width, int height) : this(width, height, allocateArrays: true) { }

        /// <summary>
        /// Initializes a new instance with optional array allocation. When <paramref name="allocateArrays"/> is false,
        /// r/g/b/a are left as the field-initializer defaults so subclasses can avoid double-allocation.
        /// </summary>
        protected internal Picture16bpp(int width, int height, bool allocateArrays)
        {
            Width = width;
            Height = height;
            Pixels = checked(width * height);

            if (allocateArrays)
            {
                r = new ushort[Pixels];
                g = new ushort[Pixels];
                b = new ushort[Pixels];
            }
            a = null;
            ProcessStack = [new PictureProcessStack
            {
                OperationDisplayName = "Create from scratch",
                Operator = this.GetType(),
                ProcessingFuncStackTrace = new StackTrace(true),
            }];

            PictureLifecycleTracker.RegisterCreated(this);
        }


        /// <summary>
        /// Initializes a new instance of the Picture class by loading image data from the specified file path.
        /// </summary>
        /// <remarks>The image is loaded from the specified file and its pixel data is extracted for use
        /// by the Picture instance. The constructor supports images compatible with the underlying image processing
        /// library. If the file does not exist or is not a valid image, an exception may be thrown by the image loading
        /// process.</remarks>
        /// <param name="imagePath">The file path to the image to load. The path must refer to a valid image file and cannot be null, empty, or
        /// consist only of white-space characters.</param>
        /// <exception cref="ArgumentException">Thrown if imagePath is null, empty, or consists only of white-space characters.</exception>
        [DebuggerNonUserCode()]
        public Picture16bpp(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("imagePath is null or empty", nameof(imagePath));
            PictureFileLoader.LoadInto(this, imagePath);
            PictureLifecycleTracker.RegisterCreated(this);
            ProcessStack ??= new();
        }

        public Picture16bpp SetAlpha(bool haveAlpha)
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

        public static Picture16bpp GenerateSolidColor(int width, int height, ushort r, ushort g, ushort b, float? a)
        {
            var pic = new Picture16bpp(width, height, allocateArrays: false)
            {
                ProcessStack = new List<PictureProcessStack>
                {
                    new PictureProcessStack
                    {
                        OperationDisplayName = "GenerateSolidColor",
                        Operator = typeof(Picture16bpp),
                        ProcessingFuncStackTrace = new StackTrace(true),
                        Properties = new Dictionary<string, object>
                        {
                            { "Width", width },
                            { "Height", height },
                            { "R", r },
                            { "G", g },
                            { "B", b },
                            { "A", a ?? -1f },
                        },
                    }
                }
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
                            Operator = typeof(Picture16bpp),
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
            if (bitPerPixel == PicturePixelMode.UShortPicture)
            {
                return this;
            }
            else if (bitPerPixel == PicturePixelMode.BytePicture)
            {
                if (!AllowPixelModeDowngrade) throw new InvalidOperationException($"AllowPixelModeDowngrade is false, so you can't convert a Picture16bpp to Picture8bpp.") { Data = { { "ProcessStack", ProcessStack } } };
                var sw = Stopwatch.StartNew();
                var pic = new Picture8bpp(Width, Height, allocateArrays: false)
                {
                    Tag = this.Tag,
                    HasAlphaChannel = this.HasAlphaChannel,
                    ProcessStack = this.ProcessStack,
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

                pic.r = GC.AllocateUninitializedArray<byte>(Pixels);
                pic.g = GC.AllocateUninitializedArray<byte>(Pixels);
                pic.b = GC.AllocateUninitializedArray<byte>(Pixels);
                PictureBufferUtilities.ConvertUShortToByte(r, pic.r);
                PictureBufferUtilities.ConvertUShortToByte(g, pic.g);
                PictureBufferUtilities.ConvertUShortToByte(b, pic.b);
                pic.ProcessStack.Add(new PictureProcessStack
                {
                    OperationDisplayName = "Converted from 16bpp to 8bpp",
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

        IPicture<ushort> IPicture<ushort>.SetAlpha(bool haveAlpha)
        {
            return SetAlpha(haveAlpha);
        }

        public ulong GetUniqueID()
        {
            var hash = new XxHash64();
            hash.Append(MemoryMarshal.AsBytes<ushort>(r.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<ushort>(g.AsSpan()));
            hash.Append(MemoryMarshal.AsBytes<ushort>(b.AsSpan()));
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

        public string GetDiagnosticsInfo() => $"16BitPerPixel image, Tag: '{Tag}', Size: {Width}*{Height}, avg R:{r.Average(Convert.ToDecimal)} G:{g.Average(Convert.ToDecimal)} B:{b.Average(Convert.ToDecimal)} A:(has:{HasAlphaChannel}){a?.Average(Convert.ToDecimal) ?? -1}";
    }
}
