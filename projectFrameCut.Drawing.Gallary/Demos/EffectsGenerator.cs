using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal static class EffectsGenerator
{
    public static ImageSource GenerateEffectStrip(int cellSize = 120)
    {
        var pattern = DemoHelper.GenerateTestPattern(cellSize, cellSize);

        var effects = new (string Name, Func<Picture8bpp, IPicture<byte>> Apply)[]
        {
            ("Original", src => new Picture8bpp(src, copyData: false)),
            ("Invert", src => InvertEffect.Process(src)),
            ("Grayscale", src => GrayscaleEffect.Process(src)),
            ("Blur σ=3", src => BlurEffect.Process(src, 3f)),
            ("Vignette", src => VignetteEffect.Process(src, 0.5f, 0.5f)),
            ("Flip H", src => FlipEffect.Process(src, true, false)),
        };

        int stripWidth = cellSize * effects.Length;
        int stripHeight = cellSize;
        var strip = DemoHelper.CreateWhiteCanvas(stripWidth, stripHeight);

        for (int e = 0; e < effects.Length; e++)
        {
            var result = effects[e].Apply(pattern);
            CopyPixels(result, e * cellSize, 0, cellSize, cellSize, stripWidth, strip);
            if (!ReferenceEquals(result, pattern))
                result.Dispose();
        }

        pattern.Dispose();
        return strip.ToImageSource();
    }

    private static void CopyPixels(IPicture<byte> src, int dstX, int dstY, int width, int height, int dstStride, Picture8bpp dst)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int si = y * src.Width + x;
                int di = (dstY + y) * dstStride + (dstX + x);
                dst.r[di] = src.r[si];
                dst.g[di] = src.g[si];
                dst.b[di] = src.b[si];
            }
        }
    }
}
