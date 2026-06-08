using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using DrawingIPicture = projectFrameCut.Drawing.Base.IPicture;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal static class DemoHelper
{
    private static readonly PngPictureEncoder PngEncoder = new();

    public static Picture8bpp GenerateTestPattern(int width, int height)
    {
        var pic = new Picture8bpp(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                pic.r[i] = (byte)(x * 255 / width);
                pic.g[i] = (byte)(y * 255 / height);
                pic.b[i] = (byte)(128 + 127 * Math.Sin(x * 0.05) * Math.Cos(y * 0.05));
            }
        }
        return pic;
    }

    public static ImageSource ToImageSource(this DrawingIPicture picture)
    {
        var ms = new MemoryStream();
        picture.Save(ms, PngEncoder);
        ms.Position = 0;
        return ImageSource.FromStream(() => ms);
    }

    public static Picture8bpp CreateWhiteCanvas(int width, int height)
    {
        var canvas = new Picture8bpp(width, height);
        Array.Fill(canvas.r, (byte)255);
        Array.Fill(canvas.g, (byte)255);
        Array.Fill(canvas.b, (byte)255);
        return canvas;
    }
}
