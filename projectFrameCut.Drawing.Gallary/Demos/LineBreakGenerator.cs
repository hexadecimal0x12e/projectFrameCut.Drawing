using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.ReadWriteConvert;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Gallary.Demos;

internal static class LineBreakGenerator
{
    private static readonly PngPictureEncoder PngEncoder = new();

    public static (string broken, ImageSource? preview, byte[]? png) RenderBrokenText(
        FontFace font, TextEntry entry, float targetWidth,
        int imageWidth, int imageHeight, bool transparentBackground = false, bool useDashWhenWordAcrossLine = false, bool allowPunctuationOverflowMaxWidthInCJK = false)
    {
        try
        {
            if (font == null) return ("(font is null)", null, null);

            entry = entry with { FontName = font.FamilyName };

            var broken = LineBreakHandler.BreakLine(entry, font, targetWidth, "\n", useDashWhenWordAcrossLine, allowPunctuationOverflowMaxWidthInCJK);

            var brokenEntry = entry with { Text = broken };
            var engine = new NormalTypesettingEngine { DebugMode = false, ShowEMBox = true };
            var vectorCanvas = engine.Layout(brokenEntry, font);

            // Add a vertical guide line at the target width boundary.
            // Use UniformScale so the line aligns with the glyph coordinate
            // system (relative advances are in uniform space, not canvas space).
            float lineWidth = 0.003f;
            float lineHeight = 1.5f;
            var guideLine = ShapeCanvasElement.DrawRectangle(lineWidth, lineHeight)
                .WithFill(200, 60, 60, 0.45f)
                .WithPosition(targetWidth - lineWidth * 0.5f, -0.25f)
                .WithLayer(int.MaxValue);
            guideLine.UseUniformScale = true;
            guideLine.BaseX = entry.X;
            vectorCanvas.Elements.Add(guideLine);

            var picture = new CPUVectorPictureRasterizer().Convert(
                vectorCanvas, imageWidth, imageHeight, transparentBackground, AntiAliasMode.SSAA4x);

            using var ms = new MemoryStream();
            picture.Save(ms, PngEncoder);
            var pngBytes = ms.ToArray();

            return (broken, picture.ToImageSource(), pngBytes);
        }
        catch (Exception ex)
        {
            throw;
            return ($"(error: {ex.Message})", null, null);
        }
    }
}
