using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Tests;

internal static class TestPictureFactory
{
    public static Picture8bpp CreatePicture8(
        int width,
        int height,
        byte[] red,
        byte[]? green = null,
        byte[]? blue = null,
        float[]? alpha = null)
    {
        int pixels = checked(width * height);
        Assert.HasCount(pixels, red);

        var picture = new Picture8bpp(width, height)
        {
            r = (byte[])red.Clone(),
            g = (byte[])(green ?? red).Clone(),
            b = (byte[])(blue ?? red).Clone(),
            a = alpha is null ? null : (float[])alpha.Clone(),
            HasAlphaChannel = alpha is not null
        };

        return picture;
    }

    public static Picture16bpp CreatePicture16(
        int width,
        int height,
        ushort[] red,
        ushort[]? green = null,
        ushort[]? blue = null,
        float[]? alpha = null)
    {
        int pixels = checked(width * height);
        Assert.HasCount(pixels, red);

        var picture = new Picture16bpp(width, height)
        {
            r = (ushort[])red.Clone(),
            g = (ushort[])(green ?? red).Clone(),
            b = (ushort[])(blue ?? red).Clone(),
            a = alpha is null ? null : (float[])alpha.Clone(),
            HasAlphaChannel = alpha is not null
        };

        return picture;
    }

    public static HDRPicture16bpp CreateHdrPicture16(
        int width,
        int height,
        ushort[] red,
        ushort[]? green = null,
        ushort[]? blue = null,
        float[]? alpha = null,
        float[]? brightness = null,
        float maximumBrightness = 1000f)
    {
        int pixels = checked(width * height);
        Assert.HasCount(pixels, red);

        var picture = new HDRPicture16bpp(width, height)
        {
            r = (ushort[])red.Clone(),
            g = (ushort[])(green ?? red).Clone(),
            b = (ushort[])(blue ?? red).Clone(),
            a = alpha is null ? null : (float[])alpha.Clone(),
            HasAlphaChannel = alpha is not null,
            Brightness = brightness is null ? new float[pixels] : (float[])brightness.Clone(),
            MaximumBrightness = maximumBrightness
        };

        return picture;
    }
}
