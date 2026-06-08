using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Composing;
using DrawingBlendMode = projectFrameCut.Drawing.Processing.Composing.BlendMode;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal static class BlendGenerator
{
    public static ImageSource GenerateBlendDemo(int cellSize = 120)
    {
        var basePic = DemoHelper.GenerateTestPattern(cellSize, cellSize);

        // Create a white circle on a transparent overlay
        var overlay = new Picture8bpp(cellSize, cellSize)
        {
            HasAlphaChannel = true,
            a = new float[cellSize * cellSize]
        };
        int cx = cellSize / 2, cy = cellSize / 2, r = cellSize / 3;
        int rr = r * r;
        for (int y = 0; y < cellSize; y++)
        {
            for (int x = 0; x < cellSize; x++)
            {
                int i = y * cellSize + x;
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= rr)
                {
                    overlay.r[i] = 255;
                    overlay.g[i] = 255;
                    overlay.b[i] = 255;
                    overlay.a[i] = 0.8f;
                }
                // else: a remains 0 (default), pixel is skipped in compose
            }
        }

        var composer = new CPUBlendPictureComposer();
        var modes = new (string Name, DrawingBlendMode Mode)[]
        {
            ("Overlay", DrawingBlendMode.Overlay),
            ("Add", DrawingBlendMode.Add),
            ("Multiply", DrawingBlendMode.Multiply),
            ("Screen", DrawingBlendMode.Screen),
        };

        int stripWidth = cellSize * modes.Length;
        var strip = DemoHelper.CreateWhiteCanvas(stripWidth, cellSize);

        for (int m = 0; m < modes.Length; m++)
        {
            using var result = (Picture8bpp)composer.Compose(basePic, overlay, modes[m].Mode);
            int dstX = m * cellSize;
            for (int y = 0; y < cellSize; y++)
            {
                for (int x = 0; x < cellSize; x++)
                {
                    int si = y * cellSize + x;
                    int di = y * stripWidth + (dstX + x);
                    strip.r[di] = result.r[si];
                    strip.g[di] = result.g[si];
                    strip.b[di] = result.b[si];
                }
            }
        }

        basePic.Dispose();
        overlay.Dispose();
        return strip.ToImageSource();
    }
}
