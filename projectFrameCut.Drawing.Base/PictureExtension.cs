using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Base.ReadWriteConvert;

namespace projectFrameCut.Drawing.Base
{
    public static class PictureExtensions
    {
        public static readonly VfdPictureEncoder SharedVfdPictureEncoder = new VfdPictureEncoder();
        public static readonly PngPictureEncoder SharedPngPictureEncoder = new PngPictureEncoder();
        public static readonly VfdPictureDecoder SharedVfdPictureDecoder = new VfdPictureDecoder();
        public static readonly PngPictureDecoder SharedPngPictureDecoder = new PngPictureDecoder();

        extension(IPicture source)
        {
            /// <summary>
            /// Clone the specific image instance with a deep copy of its pixel data buffers. 
            /// Metadata and process stack are also copied, with an additional step indicating the clone operation.
            /// </summary>
            /// <returns>A same IPicture without sharing pixel data buffers.</returns>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public IPicture Clone()
            {
                if (source is null) throw new ArgumentNullException(nameof(source));
                if (source.Disposed) throw new ObjectDisposedException(nameof(source));
                var sw = Stopwatch.StartNew();
                lock (source)
                {
                    int width = source.Width;
                    int height = source.Height;
                    int pixels = source.Pixels;

                    if (source.BitPerPixel == 16)
                    {
                        // Prefer typed interface if available
                        if (source is IPicture<ushort> s16)
                        {
                            if (s16.r == null || s16.g == null || s16.b == null)
                                throw new InvalidOperationException("Source 16bpp picture has null channel buffers.");

                            var dst = new Picture16bpp(width, height)
                            {
                                Tag = s16.Tag,
                                HasAlphaChannel = s16.HasAlphaChannel
                            };

                            // ensure destination arrays exist
                            dst.r = new ushort[pixels];
                            dst.g = new ushort[pixels];
                            dst.b = new ushort[pixels];
                            Array.Copy(s16.r, dst.r, pixels);
                            Array.Copy(s16.g, dst.g, pixels);
                            Array.Copy(s16.b, dst.b, pixels);

                            if (s16.HasAlphaChannel && s16.a != null)
                            {
                                dst.a = new float[pixels];
                                Array.Copy(s16.a, dst.a, pixels);
                                dst.HasAlphaChannel = true;
                            }
                            else
                            {
                                dst.a = null;
                                dst.HasAlphaChannel = false;
                            }
                            dst.ProcessStack = s16.ProcessStack.Append(new PictureProcessStack
                            {
                                OperationDisplayName = "Deep copied",
                                Operator = typeof(PictureExtensions),
                                ProcessingFuncStackTrace = new StackTrace(true),
                                Elapsed = sw.Elapsed
                            }).ToList();
                            return dst;
                        }
                        else
                        {
                            // Fallback using GetSpecificChannel
                            var rr = source.GetSpecificChannel(IPicture.ChannelId.Red) as ushort[] ?? throw new InvalidOperationException("Red channel missing for 16bpp picture.");
                            var gg = source.GetSpecificChannel(IPicture.ChannelId.Green) as ushort[] ?? throw new InvalidOperationException("Green channel missing for 16bpp picture.");
                            var bb = source.GetSpecificChannel(IPicture.ChannelId.Blue) as ushort[] ?? throw new InvalidOperationException("Blue channel missing for 16bpp picture.");
                            var aa = source.HasAlphaChannel ? source.GetSpecificChannel(IPicture.ChannelId.Alpha) as float[] : null;

                            if (rr.Length != pixels || gg.Length != pixels || bb.Length != pixels || (aa != null && aa.Length != pixels))
                                throw new InvalidOperationException("Source channel buffer lengths do not match picture pixel count.");

                            var dst = new Picture16bpp(width, height)
                            {
                                Tag = source.Tag,
                                HasAlphaChannel = source.HasAlphaChannel
                            };

                            dst.r = new ushort[pixels];
                            dst.g = new ushort[pixels];
                            dst.b = new ushort[pixels];
                            Array.Copy(rr, dst.r, pixels);
                            Array.Copy(gg, dst.g, pixels);
                            Array.Copy(bb, dst.b, pixels);

                            if (aa != null)
                            {
                                dst.a = new float[pixels];
                                Array.Copy(aa, dst.a, pixels);
                                dst.HasAlphaChannel = true;
                            }
                            else
                            {
                                dst.a = null;
                                dst.HasAlphaChannel = false;
                            }
                            dst.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
                            {
                                OperationDisplayName = "Deep copied",
                                Operator = typeof(PictureExtensions),
                                ProcessingFuncStackTrace = new StackTrace(true),
                                Elapsed = sw.Elapsed
                            }).ToList();
                            return dst;
                        }
                    }
                    else if (source.BitPerPixel == 8)
                    {
                        if (source is IPicture<byte> s8)
                        {
                            if (s8.r == null || s8.g == null || s8.b == null)
                                throw new InvalidOperationException("Source 8bpp picture has null channel buffers.");

                            var dst = new Picture8bpp(width, height)
                            {
                                Tag = s8.Tag,
                                ProcessStack = s8.ProcessStack.Append(new PictureProcessStack
                                {
                                    OperationDisplayName = "Deep copied",
                                    Operator = typeof(PictureExtensions),
                                    ProcessingFuncStackTrace = new StackTrace(true),
                                }).ToList(),
                                HasAlphaChannel = s8.HasAlphaChannel
                            };

                            dst.r = new byte[pixels];
                            dst.g = new byte[pixels];
                            dst.b = new byte[pixels];
                            Array.Copy(s8.r, dst.r, pixels);
                            Array.Copy(s8.g, dst.g, pixels);
                            Array.Copy(s8.b, dst.b, pixels);

                            if (s8.HasAlphaChannel && s8.a != null)
                            {
                                dst.a = new float[pixels];
                                Array.Copy(s8.a, dst.a, pixels);
                                dst.HasAlphaChannel = true;
                            }
                            else
                            {
                                dst.a = null;
                                dst.HasAlphaChannel = false;
                            }
                            dst.ProcessStack = s8.ProcessStack.Append(new PictureProcessStack
                            {
                                OperationDisplayName = "Deep copied",
                                Operator = typeof(PictureExtensions),
                                ProcessingFuncStackTrace = new StackTrace(true),
                                Elapsed = sw.Elapsed
                            }).ToList();
                            return dst;
                        }
                        else
                        {
                            var rr = source.GetSpecificChannel(IPicture.ChannelId.Red) as byte[] ?? throw new InvalidOperationException("Red channel missing for 8bpp picture.");
                            var gg = source.GetSpecificChannel(IPicture.ChannelId.Green) as byte[] ?? throw new InvalidOperationException("Green channel missing for 8bpp picture.");
                            var bb = source.GetSpecificChannel(IPicture.ChannelId.Blue) as byte[] ?? throw new InvalidOperationException("Blue channel missing for 8bpp picture.");
                            var aa = source.HasAlphaChannel ? source.GetSpecificChannel(IPicture.ChannelId.Alpha) as float[] : null;

                            if (rr.Length != pixels || gg.Length != pixels || bb.Length != pixels || (aa != null && aa.Length != pixels))
                                throw new InvalidOperationException("Source channel buffer lengths do not match picture pixel count.");

                            var dst = new Picture8bpp(width, height)
                            {
                                Tag = source.Tag,
                                ProcessStack = source.ProcessStack.Append(new PictureProcessStack
                                {
                                    OperationDisplayName = "Deep copied",
                                    Operator = typeof(PictureExtensions),
                                    ProcessingFuncStackTrace = new StackTrace(true),
                                }).ToList(),
                                HasAlphaChannel = source.HasAlphaChannel
                            };

                            dst.r = new byte[pixels];
                            dst.g = new byte[pixels];
                            dst.b = new byte[pixels];
                            Array.Copy(rr, dst.r, pixels);
                            Array.Copy(gg, dst.g, pixels);
                            Array.Copy(bb, dst.b, pixels);

                            if (aa != null)
                            {
                                dst.a = new float[pixels];
                                Array.Copy(aa, dst.a, pixels);
                                dst.HasAlphaChannel = true;
                            }
                            else
                            {
                                dst.a = null;
                                dst.HasAlphaChannel = false;
                            }
                            dst.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
                            {
                                OperationDisplayName = "Deep copied",
                                Operator = typeof(PictureExtensions),
                                ProcessingFuncStackTrace = new StackTrace(true),
                                Elapsed = sw.Elapsed
                            }).ToList();
                            return dst;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Only 8bpp and 16bpp images are supported for deep copy.");
                    }
                }
            }

            /// <summary>
            /// Save the picture to a stream using the provided encoder. The encoder is responsible for handling the specific picture type and format.
            /// </summary>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void Save(Stream stream, IPictureEncoder encoder)
            {
                ArgumentNullException.ThrowIfNull(stream);
                ArgumentNullException.ThrowIfNull(encoder);
                ObjectDisposedException.ThrowIf(source.Disposed, nameof(source));
                encoder.SaveIPicture(source, stream);
            }

            /// <summary>
            /// Save the picture to a file path using the provided encoder. The encoder is responsible for handling the specific picture type and format.
            /// </summary>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void SaveToDisk(string path, IPictureEncoder encoder)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);
                ArgumentNullException.ThrowIfNull(encoder);
                ObjectDisposedException.ThrowIf(source.Disposed, nameof(source));
                using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                encoder.SaveIPicture(source, fs);
                fs.Flush(true);
            }

            /// <summary>
            /// Save the picture to a file path in png format. This is a convenient method that uses a shared instance of <see cref="PngPictureEncoder"/> to save the picture as a PNG file. The encoder will handle the specific picture type and format, and the file will be created or overwritten at the specified path.
            /// </summary>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void SaveToPng(string path)
                => SaveToDisk(source, path, SharedPngPictureEncoder);

            /// <summary>
            /// Save the picture to a file path in png format. This is a convenient method that uses a shared instance of <see cref="PngPictureEncoder"/> to save the picture as a PNG file. The encoder will handle the specific picture type and format, and the file will be created or overwritten at the specified path.
            /// </summary>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void SaveToPng(Stream output)
                => Save(source, output, SharedPngPictureEncoder);

            /// <summary>
            /// Get the dimensions of the picture as a tuple of (width, height). This is a convenient method to retrieve both dimensions together without needing to access the properties separately.
            /// </summary>
            [DebuggerStepThrough()]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (int Width, int Height) GetDimensions() => (source.Width, source.Height);

            public IPicture AppendProcessStack(string operationDisplayName, Type operatorType, Dictionary<string, object>? properties = null, TimeSpan? elapsed = null, string? tag = null)
            {
                source.ProcessStack = source.ProcessStack.Append(new PictureProcessStack
                {
                    OperationDisplayName = operationDisplayName,
                    Operator = operatorType,
                    ProcessingFuncStackTrace = new StackTrace(1, true),
                    Properties = properties ?? new(),
                    Elapsed = elapsed ?? null,
                    Tag = tag
                }).ToList();

                return source;
            }
        }

        public static (int Width, int Height) GetDimensions(string picPath) => new Picture8bpp(picPath).GetDimensions();

        public static bool TryFromXYToArrayIndex(this IPicture reference, int x, int y, out int index)
            => TryFromXYToArrayIndex(x, y, reference.Width, reference.Height, out index);

        public static bool TryFromXYToArrayIndex(int x, int y, int width, int height, out int index)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                index = -1;
                return false;
            }
            index = y * width + x;
            return true;
        }

        public static Pixel<T> GetPixel<T>(this IPicture<T> source, int x, int y)
        {
            if (!TryFromXYToArrayIndex(x, y, source.Width, source.Height, out int idx))
            {
                if (x < 0 || x >= source.Width)
                    throw new ArgumentOutOfRangeException(nameof(x), "x is out of bounds.");
                if (y < 0 || y >= source.Height)
                    throw new ArgumentOutOfRangeException(nameof(y), "y is out of bounds.");
                throw new ArgumentOutOfRangeException("x or y", "x or y is out of bounds.");
            }
            return new Pixel<T>
            {
                r = source.r[idx],
                g = source.g[idx],
                b = source.b[idx],
                a = (source.a != null) ? source.a[idx] : 1f
            };
        }

        public struct Pixel<T>
        {
            public T r;
            public T g;
            public T b;
            public float a;
        }
    }

    internal static class PictureBufferUtilities
    {
        private static readonly Vector128<byte> Rgba32RMask = Vector128.Create((byte)0, 4, 8, 12, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba32GMask = Vector128.Create((byte)1, 5, 9, 13, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba32BMask = Vector128.Create((byte)2, 6, 10, 14, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba32AMask = Vector128.Create((byte)3, 7, 11, 15, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba64RMask = Vector128.Create((byte)0, 1, 8, 9, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba64GMask = Vector128.Create((byte)2, 3, 10, 11, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
        private static readonly Vector128<byte> Rgba64BMask = Vector128.Create((byte)4, 5, 12, 13, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] AllocateFilledArray<T>(int length, T value)
        {
            T[] array = GC.AllocateUninitializedArray<T>(length);
            array.AsSpan().Fill(value);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertUShortToByte(ReadOnlySpan<ushort> source, Span<byte> destination)
        {
            if (source.Length != destination.Length)
            {
                throw new ArgumentException("Source and destination lengths must match.");
            }

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = (byte)(source[i] / 257);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertByteToUShort(ReadOnlySpan<byte> source, Span<ushort> destination)
        {
            if (source.Length != destination.Length)
            {
                throw new ArgumentException("Source and destination lengths must match.");
            }

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = (ushort)(source[i] * 257);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertBoolMaskToBytes(ReadOnlySpan<bool> source, Span<byte> destination)
        {
            if (source.Length != destination.Length)
            {
                throw new ArgumentException("Source and destination lengths must match.");
            }

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = source[i] ? byte.MaxValue : byte.MinValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClampBrightness(ReadOnlySpan<float> source, Span<float> destination, double offset)
        {
            if (source.Length != destination.Length)
            {
                throw new ArgumentException("Source and destination lengths must match.");
            }

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = (float)Math.Clamp(source[i] + offset, 0.0, 1.0);
            }
        }

    }

    /// <summary>
    /// Read-only lifecycle snapshot of one picture instance.
    /// </summary>
    public readonly record struct PictureLifecycleSnapshot(
        long Id,
        string TypeName,
        int Width,
        int Height,
        DateTime CreatedAtUtc,
        DateTime? DisposedAtUtc,
        DateTime? CollectedAtUtc,
        bool IsDisposed,
        bool IsCollected,
        TimeSpan? LifetimeToDispose,
        TimeSpan? LifetimeToCollect,
        StackTrace CreateStack,
        StackTrace? DisposeStack,
        List<PictureProcessStack>? FinalProcessStack);

    /// <summary>
    /// Centralized lifecycle tracker for <see cref="IPicture"/> objects.
    /// </summary>
    public static class PictureLifecycleTracker
    {
        private sealed record PictureIdentity(long Id);

        private sealed record PictureLifecycleState(long Id, string TypeName, int Width, int Height, DateTime CreatedAtUtc, StackTrace CreateStack)
        {
            private long _disposedAtTicks;
            private long _collectedAtTicks;
            private StackTrace? DisposedStack;
            private List<PictureProcessStack>? FinalStack;

            public PictureLifecycleState(long id, IPicture picture) : this(id, picture.GetType().FullName ?? picture.GetType().Name, picture.Width, picture.Height, DateTime.UtcNow, new StackTrace(true))
            {
            }

            public void MarkDisposed(List<PictureProcessStack>? stack)
            {
                Interlocked.CompareExchange(ref _disposedAtTicks, DateTime.UtcNow.Ticks, 0);
                Interlocked.Exchange(ref DisposedStack, new StackTrace(true));
                Interlocked.Exchange(ref FinalStack, stack);
            }

            public void MarkCollected(List<PictureProcessStack>? stack)
            {
                Interlocked.CompareExchange(ref _collectedAtTicks, DateTime.UtcNow.Ticks, 0);
                if (stack != null)
                {
                    Interlocked.Exchange(ref FinalStack, stack);
                }
            }

            public PictureLifecycleSnapshot ToSnapshot()
            {
                long disposedTicks = Volatile.Read(ref _disposedAtTicks);
                long collectedTicks = Volatile.Read(ref _collectedAtTicks);
                DateTime? disposedAt = disposedTicks > 0 ? new DateTime(disposedTicks, DateTimeKind.Utc) : null;
                DateTime? collectedAt = collectedTicks > 0 ? new DateTime(collectedTicks, DateTimeKind.Utc) : null;

                return new PictureLifecycleSnapshot(
                    Id,
                    TypeName,
                    Width,
                    Height,
                    CreatedAtUtc,
                    disposedAt,
                    collectedAt,
                    disposedAt.HasValue,
                    collectedAt.HasValue,
                    disposedAt?.Subtract(CreatedAtUtc),
                    collectedAt?.Subtract(CreatedAtUtc),
                    CreateStack,
                    DisposedStack,
                    FinalStack);
            }
        }

        private sealed class FinalizationSentinel
        {
            private readonly long _id;
            private readonly WeakReference<IPicture> _picture;

            public FinalizationSentinel(long id, IPicture picture)
            {
                _id = id;
                _picture = new WeakReference<IPicture>(picture);
            }

            ~FinalizationSentinel()
            {
                if (_picture.TryGetTarget(out IPicture? picture))
                {
                    PictureLifecycleTracker.MarkCollected(_id, picture.ProcessStack);
                    return;
                }

                PictureLifecycleTracker.MarkCollected(_id, null);
            }
        }

        private static long _nextId;
        private static readonly ConcurrentDictionary<long, PictureLifecycleState> States = new();
        private static readonly ConditionalWeakTable<IPicture, PictureIdentity> Identities = new();
        private static readonly ConditionalWeakTable<IPicture, FinalizationSentinel> Sentinels = new();

        /// <summary>
        /// Enables tracking globally. Keep false in production unless diagnostics are needed.
        /// </summary>
        public static bool Enabled
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>
        /// Track GC collection time using an extra finalizer sentinel per picture.
        /// Keep disabled when only creation/dispose duration is needed.
        /// </summary>
        public static bool TrackCollection { get; set; } = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterCreated(IPicture picture)
        {
            if (!Enabled) return;

            PictureIdentity identity = Identities.GetValue(picture, _ => new PictureIdentity(Interlocked.Increment(ref _nextId)));
            States.TryAdd(identity.Id, new PictureLifecycleState(identity.Id, picture));

            if (TrackCollection)
            {
                Sentinels.GetValue(picture, _ => new FinalizationSentinel(identity.Id, picture));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkDisposed(IPicture picture)
        {
            if (!Enabled) return;
            if (!Identities.TryGetValue(picture, out PictureIdentity? identity)) return;
            if (States.TryGetValue(identity.Id, out PictureLifecycleState? state))
            {
                state.MarkDisposed(picture.ProcessStack);
            }
        }

        public static IReadOnlyList<PictureLifecycleSnapshot> GetSnapshots(bool includeDisposed = true)
        {
            var snapshots = States.Values
                .Select(state => state.ToSnapshot())
                .Where(snapshot => includeDisposed || !snapshot.IsDisposed)
                .OrderBy(snapshot => snapshot.Id)
                .ToArray();
            return snapshots;
        }

        public static void Clear()
        {
            States.Clear();
        }

        private static void MarkCollected(long id, List<PictureProcessStack>? stack)
        {
            if (States.TryGetValue(id, out PictureLifecycleState? state))
            {
                state.MarkCollected(stack);
            }
        }

        public static async Task ExportPictureLifecycleTrackerSnapshots(string outputPath)
        {
            try
            {
                if (!PictureLifecycleTracker.Enabled)
                {
                    //Logger.Log("PictureLifecycleTracker is disabled. Skipped lifecycle snapshot export.");
                    return;
                }

                var snapshots = PictureLifecycleTracker.GetSnapshots(includeDisposed: true);
                await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                await writer.WriteLineAsync(string.Join(',',
                [
                    "Id",
                    "TypeName",
                    "Width",
                    "Height",
                    "CreatedAtUtc",
                    "DisposedAtUtc",
                    "CollectedAtUtc",
                    "IsDisposed",
                    "IsCollected",
                    "LifetimeToDisposeMs",
                    "LifetimeToCollectMs",
                    "CreateStackTrace",
                    "DisposeStackTrace",
                    "FinalProcessStack"
                ]));

                foreach (var snapshot in snapshots)
                {
                    await writer.WriteLineAsync(string.Join(',',
                    [
                        EscapeCsv(snapshot.Id.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.TypeName),
                        EscapeCsv(snapshot.Width.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.Height.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.DisposedAtUtc?.ToString("O", CultureInfo.InvariantCulture)?? "N/A"),
                        EscapeCsv(snapshot.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture)?? "N/A"),
                        EscapeCsv(snapshot.IsDisposed ? "true" : "false"),
                        EscapeCsv(snapshot.IsCollected ? "true" : "false"),
                        EscapeCsv(snapshot.LifetimeToDispose?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.LifetimeToCollect?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(snapshot.CreateStack.ToString()),
                        EscapeCsv(snapshot.DisposeStack?.ToString() ?? "N/A"),
                        EscapeCsv(snapshot.FinalProcessStack is List<PictureProcessStack> p ? PictureProcessStack.FormatProcessStackForLog(p, 12): "N/A"),

                    ]));
                }

                await writer.FlushAsync();
                await stream.FlushAsync();
                await writer.DisposeAsync();
                await stream.DisposeAsync();
                //Logger.Log($"Exported PictureLifecycleTracker snapshots: {snapshots.Count} records, {outputPath}");
            }
            catch (Exception)
            {
                //Logger.Log(ex, "export PictureLifecycleTracker snapshots");
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
