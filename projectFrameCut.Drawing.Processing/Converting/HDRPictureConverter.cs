using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Processing.Converting
{
    public interface IHDRPictureConverter
    {
        Picture16bpp ToSDR(IHDRPicture<ushort> source, HDRImageDegradeToSDRMode? degradeMode = null);
        IHDRPicture<ushort> ToHDR(IPicture<ushort> source, float maximumBrightness = 1000f, float defaultBrightness = 1f);
    }


    public static class HDRPictureConverter
    {
        public static HDRImageDegradeToSDRMode DefaultHDRImageDegradeToSDRMode { get; internal set; }

        public static IHDRPictureConverter Default { get; set; } = new HDRPictureFormatConverter();

        extension(IHDRPicture<ushort> picture)
        {
            public Picture16bpp DegradeToSDR(HDRImageDegradeToSDRMode? degradeMode = null)
                => Default.ToSDR(picture, degradeMode);
        }

        extension(IPicture<ushort> picture)
        {
            public IHDRPicture<ushort> ToHDR(float maximumBrightness = 1000f, float defaultBrightness = 1f)
                => Default.ToHDR(picture, maximumBrightness, defaultBrightness);
        }
    }

    /// <summary>
    /// Determines the method to degrade HDR image to SDR when the renderer or display does not support HDR.
    /// </summary>
    public enum HDRImageDegradeToSDRMode
    {
        /// <summary>
        /// Normalize the pixels from the brightness channel to the range of RGB channels.
        /// </summary>
        NormalizeBrightnessToRGB,
        /// <summary>
        /// Overlay a black mask which has alpha channel from brightness to the RGB(A) channels.
        /// </summary>
        OverlayMaskFromBrightness,
        /// <summary>
        /// Discard the brightness channel away, and cast the remaining channels to an <see cref="Picture16bpp"/>.
        /// </summary>
        DiscardBrightnessChannel,
        /// <summary>
        /// Throw an <see cref="InvalidOperationException"/> when degrade operation occurs.
        /// </summary>
        DisallowDowngrade
    }
}
