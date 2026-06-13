using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal static class TextGenerator
{
    public static ImageSource? RenderText(FontFace font, TextEntry entry, int width, int height,
        bool transparentBackground = false, AntiAliasMode aaMode = AntiAliasMode.None)
    {
        try
        {
            if (font == null) return null;

            entry = entry with { FontName = font.FamilyName };
            var engine = new NormalTypesettingEngine
            {
                DebugMode = true
            };
            var vectorCanvas = engine.Layout(entry, font);
            var picture = new CPUVectorPictureRasterizer().Convert(vectorCanvas, width, height, transparentBackground, aaMode);
            return picture.ToImageSource();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RenderText error: {ex}");
            return null;
        }
    }

    private static FontFace? LoadFont(string path)
    {
        if (path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
        {
            var collection = FontCollection.Load(path);
            var info = collection.FirstOrDefault();
            return info?.Load();
        }
        return FontFace.Load(path);
    }
}
