using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using static projectFrameCut.Drawing.Base.IPicture;

namespace projectFrameCut.Drawing.Base.Picture
{
    /// <summary>
    /// The 8-bit monochrome picture structure with optional alpha.
    /// </summary>
    [DebuggerDisplay("{GetDiagnosticsInfo()}")]
    public class MonoChromePicture8bpp : IMonoChromePicture<byte>
    {
        [JsonIgnore()]
        public byte[] gray { get; set; } = Array.Empty<byte>();
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

        public MonoChromePicture8bpp(IMonoChromePicture<byte> picture, bool copyData = false)
        {
            Width = picture.Width;
            Height = picture.Height;
            Pixels = picture.Pixels;

            if (copyData)
            {
                gray = (picture.gray != null && picture.gray.Length == Pixels) ? picture.gray : new byte[Pixels];
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
                    OperationDisplayName = "Create monochrome from another",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Properties = new Dictionary<string, object>
                    {
                        { "SourceProcessStack", picture.ProcessStack },
                        { "CopyData", copyData }
                    },
                }
            };

            PictureLifecycleTracker.RegisterCreated(this);
        }

        public MonoChromePicture8bpp(int width, int height)
            : this(width, height, allocateArrays: true)
        {
        }

        internal MonoChromePicture8bpp(int width, int height, bool allocateArrays)
        {
            Width = width;
            Height = height;
            Pixels = checked(width * height);

            if (allocateArrays)
            {
                gray = new byte[Pixels];
            }

            a = null;
            ProcessStack = new List<PictureProcessStack>
            {
                new PictureProcessStack
                {
                    OperationDisplayName = "Create monochrome from scratch",
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

        [DebuggerNonUserCode()]
        public MonoChromePicture8bpp(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("imagePath is null or empty", nameof(imagePath));
            PictureFileLoader.LoadInto(this, imagePath);
            PictureLifecycleTracker.RegisterCreated(this); 
            ProcessStack ??= new();
        }

        public MonoChromePicture8bpp SetAlpha(bool haveAlpha)
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

        public static MonoChromePicture8bpp GenerateSolidColor(int width, int height, byte gray, float? a)
        {
            var pic = new MonoChromePicture8bpp(width, height, allocateArrays: false)
            {
                ProcessStack = new List<PictureProcessStack>
                {
                    new PictureProcessStack
                    {
                        OperationDisplayName = "Generate monochrome solid color",
                        Operator = typeof(MonoChromePicture8bpp),
                        ProcessingFuncStackTrace = new StackTrace(true),
                        Properties = new Dictionary<string, object>
                        {
                            { "Width", width },
                            { "Height", height },
                            { "Gray", gray },
                            { "A", a ?? 1.0f }
                        },
                    }
                },
            };
            pic.gray = PictureBufferUtilities.AllocateFilledArray(pic.Pixels, gray);
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
                PictureBufferUtilities.ConvertByteToUShort(gray, pic.r);
                PictureBufferUtilities.ConvertByteToUShort(gray, pic.g);
                PictureBufferUtilities.ConvertByteToUShort(gray, pic.b);
                pic.ProcessStack.Add(new PictureProcessStack
                {
                    OperationDisplayName = "Converted from monochrome 8bpp to 16bpp",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Elapsed = sw.Elapsed
                });
                return pic;
            }
            else if (bitPerPixel.Value == 1)
            {
                var sw = Stopwatch.StartNew();
                var pic = new BitMaskPicture
                {
                    Tag = this.Tag,
                    Width = Width,
                    Height = Height,
                    Pixels = Pixels,
                    ProcessStack = this.ProcessStack
                };

                pic.r = GC.AllocateUninitializedArray<bool>(Pixels);
                pic.g = GC.AllocateUninitializedArray<bool>(Pixels);
                pic.b = GC.AllocateUninitializedArray<bool>(Pixels);
                for (int i = 0; i < Pixels; i++)
                {
                    bool on = gray[i] >= 128;
                    pic.r[i] = on;
                    pic.g[i] = on;
                    pic.b[i] = on;
                }

                pic.ProcessStack.Add(new PictureProcessStack
                {
                    OperationDisplayName = "Converted from monochrome 8bpp to bitmask",
                    Operator = this.GetType(),
                    ProcessingFuncStackTrace = new StackTrace(true),
                    Elapsed = sw.Elapsed
                });

                return pic;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(bitPerPixel), "仅支持 1、8 或 16 bpp。");
            }
        }

        public object? GetSpecificChannel(IPicture.ChannelId channelId)
        {
            return channelId switch
            {
                IPicture.ChannelId.MonoChrome => gray,
                IPicture.ChannelId.Red => gray,
                IPicture.ChannelId.Green => gray,
                IPicture.ChannelId.Blue => gray,
                IPicture.ChannelId.Alpha => a!,
                _ => throw new ArgumentOutOfRangeException(nameof(channelId), "Invalid channel ID."),
            };
        }

        public ulong GetUniqueID()
        {
            var hash = new XxHash64();
            hash.Append(gray.AsSpan());
            if (a is not null)
                hash.Append(MemoryMarshal.AsBytes<float>(a.AsSpan()));
            return hash.GetCurrentHashAsUInt64();
        }

        public string GetDiagnosticsInfo()
            => $"8BitMonoChrome image, Tag: '{Tag}', Size: {Width}*{Height}, avg Gray:{gray.Average(Convert.ToDecimal)} A:(has:{HasAlphaChannel}){a?.Average(Convert.ToDecimal) ?? -1}";

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
                            Operator = typeof(MonoChromePicture8bpp),
                            ProcessingFuncStackTrace = new StackTrace(true),
                            Properties = new Dictionary<string, object> { { "Force", force } }
                        });
                    }
                    if (disposing)
                    {
                        gray = null!;
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
    }
}
