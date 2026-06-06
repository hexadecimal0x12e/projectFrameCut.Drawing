using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Text;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Tests;

/// <summary>
/// Verifies that <see cref="GlyphCanvasElement"/> preserves its intrinsic
/// aspect ratio when rasterised onto non-square canvases.  Without uniform
/// scaling, a "1" character on a 800x200 canvas would appear horizontally
/// stretched 4:1 because the X dimension used the canvas width and the Y
/// dimension used the canvas height independently.
/// </summary>
[TestClass]
public sealed class TextNonSquareAspectTests
{
    private const string FontPath = @"C:\Windows\Fonts\arial.ttf";

    // ──────────────────────────────────────────────
    //  GlyphCanvasElement.UseUniformScale flag
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphCanvasElement_DefaultsUseUniformScaleToTrue()
    {
        // A glyph is intrinsically isotropic; it must request uniform scaling
        // or the renderer will distort it on non-square canvases.
        var glyph = CreateEmptyGlyph();
        var element = new GlyphCanvasElement(glyph, 1000);
        Assert.IsTrue(element.UseUniformScale);
    }

    [TestMethod]
    public void VectorCanvasElement_DefaultsUseUniformScaleToFalse()
    {
        // Non-glyph elements (rectangles, ellipses, lines) should keep the
        // legacy X=width / Y=height scaling.  Make sure we didn't accidentally
        // flip the default for the base class.
        var element = new ProbeElement();
        Assert.IsFalse(element.UseUniformScale);
    }

    // ──────────────────────────────────────────────
    //  Rasterised bounding box on non-square canvases
    // ──────────────────────────────────────────────

    [TestMethod]
    public void VectorToIPicture_TextRendersAtUniformAspectOnWideCanvas()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        const int targetW = 800;
        const int targetH = 200;        // 4:1 wide
        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "1",
            FontName = font.FamilyName,
            FontSize = 0.6f,
            X = 0.5f,
            Y = 0.5f,
            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        Assert.IsNotEmpty(canvas.Elements, "No glyph elements were produced.");
        foreach (var el in canvas.Elements)
            Assert.IsTrue(el.UseUniformScale, "Glyph elements must request uniform scaling.");

        var pic = VectorToIPicture.Convert(canvas, targetW, targetH, transparentBackground: true);
        SaveDiagnosticPng(pic, "wide");

        // Scan for the bounding box of opaque (text) pixels and verify the
        // aspect ratio matches the natural glyph aspect ratio (~1:2 for "1",
        // height being roughly twice the width).
        var bbox = ScanOpaqueBoundingBox(pic);
        Assert.IsTrue(bbox.HasValue, "No text pixels were rendered.");

        float w = bbox.Value.x1 - bbox.Value.x0 + 1;
        float h = bbox.Value.y1 - bbox.Value.y0 + 1;
        float aspect = h / w;

        // The natural aspect of "1" is roughly 2:1 (h:w). With the bug it
        // would be 2 * (W/H) = 2 * 4 = 8. With the fix it stays close to 2.
        // Allow a generous tolerance to account for AA edge pixels and the
        // fact that "1" is slightly narrower than its bounding box suggests.
        Assert.IsTrue(aspect < 4.5f,
            $"Text aspect ratio {aspect:F2} looks horizontally stretched " +
            $"(bbox {w}x{h} on a {targetW}x{targetH} canvas).");
    }

    [TestMethod]
    public void VectorToIPicture_TextRendersAtUniformAspectOnTallCanvas()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        const int targetW = 200;
        const int targetH = 800;        // 1:4 tall
        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "1",
            FontName = font.FamilyName,
            FontSize = 0.6f,
            X = 0.5f,
            Y = 0.5f,
            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        var pic = VectorToIPicture.Convert(canvas, targetW, targetH, transparentBackground: true);
        SaveDiagnosticPng(pic, "tall");

        var bbox = ScanOpaqueBoundingBox(pic);
        Assert.IsTrue(bbox.HasValue, "No text pixels were rendered.");

        float w = bbox.Value.x1 - bbox.Value.x0 + 1;
        float h = bbox.Value.y1 - bbox.Value.y0 + 1;
        float aspect = w / h;

        // "1" natural width/height is ~0.5; with the bug on a 200x800 canvas
        // (W/H = 0.25) it would be 0.5 * 0.25 = 0.125. With the fix the
        // aspect ratio should be roughly preserved.
        Assert.IsTrue(aspect > 0.25f,
            $"Text aspect ratio {aspect:F2} looks vertically squished " +
            $"(bbox {w}x{h} on a {targetW}x{targetH} canvas).");
    }

    [TestMethod]
    public void VectorToIPicture_TextRendersAtUniformAspectOnSquareCanvas()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        const int targetW = 400;
        const int targetH = 400;        // 1:1
        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "1",
            FontName = font.FamilyName,
            FontSize = 0.6f,
            X = 0.5f,
            Y = 0.5f,
            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        var pic = VectorToIPicture.Convert(canvas, targetW, targetH, transparentBackground: true);
        SaveDiagnosticPng(pic, "square");

        var bbox = ScanOpaqueBoundingBox(pic);
        Assert.IsTrue(bbox.HasValue, "No text pixels were rendered.");
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static (int x0, int y0, int x1, int y1)? ScanOpaqueBoundingBox(IPicture pic)
    {
        int w = pic.Width, h = pic.Height;

        // The picture is rendered transparent.  Opaque pixels belong to the
        // text; use any non-transparent pixel as a proxy for "text body".
        // Prefer the alpha channel (when present); otherwise check the green
        // channel for black-on-transparent text.
        float[]? alpha = null;
        ushort[]? green = null;
        if (pic.HasAlphaChannel)
        {
            alpha = pic.GetSpecificChannel<float>(IPicture.ChannelId.Alpha);
        }
        green = pic.GetSpecificChannel<ushort>(IPicture.ChannelId.Green);

        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        bool any = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isOpaque;
                if (alpha is not null)
                {
                    isOpaque = alpha[y * w + x] > 0.05f;
                }
                else
                {
                    // Black text on a transparent background: green < threshold
                    // means the pixel belongs to the text body. (If the
                    // background is opaque white this would be inverted, but
                    // the Convert call above uses transparentBackground=true.)
                    isOpaque = green![y * w + x] < 6553;
                }

                if (isOpaque)
                {
                    any = true;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        }
        return any ? (x0, y0, x1, y1) : null;
    }

    private static void SaveDiagnosticPng(IPicture pic, string tag)
    {
        try
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(dir, $"text-aspect-{tag}-{DateTime.Now:HHmmssfff}.png");
            pic.SaveToDisk(path, PictureExtensions.SharedPngPictureEncoder);
        }
        catch
        {
            // Diagnostic output is best-effort; do not fail the test if writing fails.
        }
    }

    private sealed class ProbeElement : VectorCanvasElement
    {
        public override VectorSegment[] Draw() => [];
    }

    private static Glyph CreateEmptyGlyph()
    {
        var ctor = typeof(Glyph).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];

        return (Glyph)ctor.Invoke([
            -1,                          // outlineType (empty)
            Array.Empty<GlyphPoint[]>(),  // contours
            (short)0, (short)0,          // xMin, yMin
            (short)0, (short)0,          // xMax, yMax
        ]);
    }
}
