using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Benchmarks;

internal static class BenchmarkImageFactory
{
    private static readonly Random Rng = new(42);

    internal static Picture8bpp CreateRandomBytePicture(int width, int height, bool withAlpha = true)
    {
        int pixels = width * height;
        var pic = new Picture8bpp(width, height)
        {
            r = RandomFill(new byte[pixels]),
            g = RandomFill(new byte[pixels]),
            b = RandomFill(new byte[pixels]),
        };

        if (withAlpha)
        {
            pic.SetAlpha(true);
            RandomFill(pic.a!);
        }

        return pic;
    }

    internal static Picture16bpp CreateRandomUShortPicture(int width, int height, bool withAlpha = true)
    {
        int pixels = width * height;
        var pic = new Picture16bpp(width, height)
        {
            r = RandomFill(new ushort[pixels]),
            g = RandomFill(new ushort[pixels]),
            b = RandomFill(new ushort[pixels]),
        };

        if (withAlpha)
        {
            pic.SetAlpha(true);
            RandomFill(pic.a!);
        }

        return pic;
    }

    internal static HDRPicture16bpp CreateRandomHDRPicture(int width, int height, bool withAlpha = true)
    {
        int pixels = width * height;
        var pic = new HDRPicture16bpp(width, height)
        {
            r = RandomFill(new ushort[pixels]),
            g = RandomFill(new ushort[pixels]),
            b = RandomFill(new ushort[pixels]),
            Brightness = RandomFill(new float[pixels]),
            MaximumBrightness = 1000f,
        };

        if (withAlpha)
        {
            pic.SetAlpha(true);
            RandomFill(pic.a!);
        }

        return pic;
    }

    internal static Picture8bpp CreateGradientBytePicture(int width, int height)
    {
        int pixels = width * height;
        var pic = new Picture8bpp(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                pic.r[i] = (byte)(x * 255 / width);
                pic.g[i] = (byte)(y * 255 / height);
                pic.b[i] = (byte)((x + y) * 255 / (width + height));
            }
        }

        return pic;
    }

    internal static Picture8bpp CreateSolidBytePicture(int width, int height, byte red, byte green, byte blue)
    {
        return Picture8bpp.GenerateSolidColor(width, height, red, green, blue, null);
    }

    private static byte[] RandomFill(byte[] array)
    {
        Rng.NextBytes(array);
        return array;
    }

    private static ushort[] RandomFill(ushort[] array)
    {
        var buffer = new byte[array.Length * sizeof(ushort)];
        Rng.NextBytes(buffer);
        Buffer.BlockCopy(buffer, 0, array, 0, buffer.Length);
        return array;
    }

    private static float[] RandomFill(float[] array)
    {
        for (int i = 0; i < array.Length; i++)
            array[i] = (float)Rng.NextDouble();
        return array;
    }
}
