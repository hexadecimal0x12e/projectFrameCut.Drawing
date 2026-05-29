using projectFrameCut.Drawing.Base.Picture;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace projectFrameCut.Drawing.Base
{
    /// <summary>
    /// This class is for placing Picture information and as a base of the actual picture (<see cref="IPicture{T}"/>).
    /// </summary>
    public interface IPicture : IDisposable
    {
        /// <summary>
        /// Allow convert a IPicture to a lower <see cref="BitPerPixel"/>.
        /// </summary>
        /// <remarks>
        /// When this is false, an <see cref="InvalidOperationException"/> will be thrown when attempting to convert to a lower pixel mode, either in <see cref="ToBitPerPixel(int)"/> or in VideoWriter.
        /// </remarks>
        public static bool AllowPixelModeDowngrade = true;

        /// <summary>
        /// Get how much bits in one pixel.
        /// Please refer to <see cref="PicturePixelMode"/> for more information.
        /// </summary>
        public PicturePixelMode BitPerPixel { get; }
        /// <summary>
        /// The width of this picture
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// The height of this picture
        /// </summary>
        public int Height { get; set; }
        /// <summary>
        /// Total pixels of this picture
        /// </summary>
        public int Pixels { get; set; }

        /// <summary>
        /// A string for marking this picture. 
        /// It can be used for debugging or other purposes. 
        /// It has no effect on the processing of the picture.
        /// </summary>
        public string Tag { get; set; }


        /// <summary>
        /// Records each step of the image processed.
        /// </summary>
        /// <remarks>
        /// Please append your processing step information to this property if you're manipulating the picture manually.
        /// If you're using <see cref="IPictureProcessStep"/>, override <see cref="IPictureProcessStep.GetProcessStack"/> to provide the information.
        /// </remarks>
        public List<PictureProcessStack> ProcessStack { get; set; }

        /// <summary>
        /// Indicates whether this picture has an alpha channel.
        /// </summary>
        public bool HasAlphaChannel { get; set; }
        /// <summary>
        /// Get whether this picture has been disposed.
        /// </summary>
        public bool Disposed { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the object can be disposed.
        /// </summary>
        /// <remarks>
        /// when this is true, <see cref="Dispose(bool)"/> will have no effect, except the force param is True.
        /// </remarks>
        public bool CanBeDisposed { get; set; }

        /// <summary>
        /// Convert this picture to the specified bits per pixel.
        /// </summary>
        IPicture ToBitPerPixel(PicturePixelMode bitPerPixel);

        /// <summary>
        /// Convert this picture to the specified bits per pixel.
        /// </summary>
        public sealed IPicture ToBitPerPixel(int bpp) => ToBitPerPixel(new PicturePixelMode(bpp));

        /// <summary>
        /// Get a specific channel's data.
        /// </summary>
        /// <param name="channelId">The channel want to get</param>
        /// <returns>the data. Must in a array</returns>
        object? GetSpecificChannel(ChannelId channelId);

        /// <summary>
        /// Get a specific channel's data.
        /// </summary>
        /// <param name="channelId">The channel want to get</param>
        /// <returns>the data.</returns>
        T[]? GetSpecificChannel<T>(ChannelId channelId) => GetSpecificChannel(channelId) is T[] ret ? ret : throw new InvalidCastException("The channel data type does not match the requested type.");

        /// <summary>
        /// Get the diagnostics information of this picture.
        /// </summary>
        /// <returns>The Diagnostics info</returns>
        string GetDiagnosticsInfo();

        /// <summary>
        /// Dispose the Picture when <see cref="CanBeDisposed"/> is true or <paramref name="force"/> is true.
        /// </summary>
        /// <param name="force">Ignore <see cref="CanBeDisposed"/> and dispose it anyway.</param>
        /// <param name="appendDisposedProcessStack">Whether to append a "Disposed" entry to <see cref="ProcessStack"/> before disposal.</param>
        public void Dispose(bool force = false, bool appendDisposedProcessStack = true);

        /// <summary>
        /// Compute a content-based hash of the pixel data for unique identification.
        /// </summary>
        public ulong GetUniqueID();

        public enum ChannelId
        {
            Red = 0,
            Green = 1,
            Blue = 2,
            Alpha = 3,
            MonoChrome = 4
        }

        public readonly record struct PicturePixelMode(int Value)
        {
            public static implicit operator int(PicturePixelMode bpp) => bpp.Value;
            public static implicit operator PicturePixelMode(int value) => new(value);
            public override int GetHashCode() => Value;
            public override string ToString() => Value.ToString();

            public bool Equals(PicturePixelMode mode)
            {
                return Value == mode.Value;
            }
            /// <summary>
            /// Represents a picture of 8 bits per pixel, aka <see cref="Picture8bpp"/>.
            /// </summary>
            public static PicturePixelMode BytePicture => new PicturePixelMode(8);
            /// <summary>
            /// Represents a picture of 16 bits per pixel, aka <see cref="Picture16bpp"/>.
            /// </summary>
            public static PicturePixelMode UShortPicture => new PicturePixelMode(16);
        }

    }


}
