using System.Globalization;
using System.Text.RegularExpressions;
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

[TestClass]
public sealed class TextOrientationTests
{
    private const string FontPath = @"C:\Windows\Fonts\arial.ttf";

    [TestMethod]
    public void Glyph_IsRightSideUp_AfterUnifomScaleFix()
    {
        // Regression test for the double-Y-flip bug:
        // Both the CFF and glyf parsers already negate font Y so that
        // GlyphPoint.Y is in canvas Y-down coordinates. FlattenContour must
        // NOT negate Y again, or glyphs render upside down.
        if (!File.Exists(FontPath))
            Assert.Inconclusive("Arial not present; skipping orientation test.");

        const int targetW = 800, targetH = 200;
        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "1",
            FontName = font.FamilyName,
            FontSize = 0.6f,
            X = 0.5f, Y = 0.5f,
            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        var pic = new CPUVectorPictureRasterizer().Convert(canvas, targetW, targetH, transparentBackground: true);

        var bbox = ScanOpaqueBoundingBox(pic);
        Assert.IsTrue(bbox.HasValue, "No text pixels were rendered.");
        int top = bbox.Value.y0, bottom = bbox.Value.y1;
        int glyphMid = (top + bottom) / 2;
        int baseline = 100;        // entry.Y = 0.5 * 200
        int canvasMid = targetH / 2;

        // The "1" character is mostly above the baseline. For a
        // right-side-up render, the top of the glyph must be above the
        // baseline (top < baseline), and the body of the character must
        // sit in the upper half of the canvas (top < canvasMid).
        Assert.IsLessThan(baseline, top,
            $"The top of the '1' character (Y={top}) is not above the baseline (Y={baseline}). " +
            "This indicates the glyph is rendered upside down — the Y axis is being flipped twice.");

        // And the bbox midpoint should be in the upper half of the canvas.
        Assert.IsLessThan(canvasMid, glyphMid,
            $"Glyph midpoint is at Y={glyphMid} (canvas midpoint Y={canvasMid}). " +
            "Expected the '1' to sit in the upper half of the canvas.");
    }

    [TestMethod]
    public void Glyph_SVG_IsRightSideUp()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive("Arial not present; skipping orientation test.");

        const int targetW = 800, targetH = 200;
        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "1",
            FontName = font.FamilyName,
            FontSize = 0.6f,
            X = 0.5f, Y = 0.5f,
            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        var svg = SVGToVectorElement.ExportToSvg(canvas, targetW, targetH);

        // Parse all Y values out of the polygon "points" attribute(s) and
        // verify their midpoint is above the baseline (Y < 100 in a 200-tall
        // viewBox = upper half).
        int baseline = targetH / 2;
        var onlyYs = new List<float>();
        foreach (Match m in Regex.Matches(svg, @"points=""([^""]+)"""))
        {
            var nums = m.Groups[1].Value.Split(
                new[] { ' ', '\n', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < nums.Length; i += 2)
                if (float.TryParse(nums[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    onlyYs.Add(v);
        }

        Assert.IsNotEmpty(onlyYs, "SVG had no Y coordinates.");
        float avgY = onlyYs.Average();
        Assert.IsLessThan(baseline, avgY,
            $"SVG '1' has average Y={avgY:F1}, expected < {baseline} (above the baseline). " +
            "Glyphs are flipped upside down in the SVG export as well.");
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static (int x0, int y0, int x1, int y1)? ScanOpaqueBoundingBox(IPicture pic)
    {
        var alpha = pic.GetSpecificChannel(IPicture.ChannelId.Alpha) as float[]
                    ?? throw new InvalidOperationException("Picture has no alpha channel.");
        int w = pic.Width, h = pic.Height;
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        bool any = false;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (alpha[y * w + x] > 0.05f)
            {
                any = true;
                if (x < x0) x0 = x;
                if (y < y0) y0 = y;
                if (x > x1) x1 = x;
                if (y > y1) y1 = y;
            }
        }
        return any ? (x0, y0, x1, y1) : null;
    }
}
