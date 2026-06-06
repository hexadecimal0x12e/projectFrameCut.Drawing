using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Tests;

/// <summary>
/// Regression tests for the per-character advance calculation in
/// <see cref="NormalTypesettingEngine"/>.  The previous implementation clamped the
/// advance to <c>charFontSize * 1.5f</c>, which was *relative* to the font size.
/// For a <c>charFontSize</c> close to <c>1.0</c> (a font nearly as tall as the
/// canvas) this allowed the per-character advance to approach or exceed the full
/// canvas width, pushing subsequent characters off the right edge and producing
/// the "text rendered incompletely" symptom reported by the user.
///
/// The fix has two parts:
///   1. <c>NormalTypesettingEngine.ComputeCharacterAdvance</c> no longer applies
///      a relative upper clamp at all. It just returns the proportionally-
///      converted font advance.
///   2. <c>TextClip</c> (the caller) caps the *normalised font size* itself at
///      <c>1.0</c> before passing it to the engine, so the per-character
///      advance is naturally bounded.
/// </summary>
[TestClass]
public sealed class TextAdvanceClampTests
{
    private const string FontPath = @"C:\Windows\Fonts\arial.ttf";

    [TestMethod]
    public void Layout_AdvanceStaysWithinCanvas_ForTypicalFontSize()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        using var font = FontFace.Load(FontPath);

        // 0.1 normalised ≈ 10% of canvas height — plenty of room.
        var entry = new TextEntry
        {
            Text = "Hello world",
            FontName = font.FamilyName,
            FontSize = 0.1f,
            X = 0f, Y = 0.5f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        Assert.IsNotEmpty(canvas.Elements);

        // No element should be positioned past the right edge of the canvas.
        // We approximate the right edge as RelativeX + FontSize (the glyph
        // body extends roughly that far to the right of the cursor).
        float maxRight = 0f;
        foreach (var el in canvas.Elements)
        {
            float right = el.RelativeX + entry.FontSize;
            if (right > maxRight) maxRight = right;
        }

        Assert.IsLessThanOrEqualTo(1.05f, maxRight,
            $"Layout produced an element whose right edge ({maxRight:F3}) extends " +
            "past the canvas (1.0) for a normal 0.1-fraction font size.");
    }

    [TestMethod]
    public void Layout_AdvanceIsProportionalToFontSize_NotArtificiallyCapped()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        using var font = FontFace.Load(FontPath);

        // Two text strings rendered with different font sizes must produce
        // glyphs whose x positions scale with the font size. After the unit-
        // conversion fix, a FontSize of 0.5 should put the second glyph at
        // roughly half the x of a FontSize of 1.0 — NOT at exactly the same
        // x (which is what an over-zealous upper clamp at 1.0 would do).
        var entry1 = new TextEntry
        {
            Text = "AB",
            FontName = font.FamilyName,
            FontSize = 1.0f,
            X = 0f, Y = 0.5f,
        };
        var entry05 = new TextEntry
        {
            Text = "AB",
            FontName = font.FamilyName,
            FontSize = 0.5f,
            X = 0f, Y = 0.5f,
        };

        var engine = new NormalTypesettingEngine();
        var canvas1 = engine.Layout(entry1, font);
        var canvas05 = engine.Layout(entry05, font);
        Assert.IsNotEmpty(canvas1.Elements);
        Assert.IsNotEmpty(canvas05.Elements);

        // The cursor of the second glyph in the 0.5-font-size canvas should
        // be roughly half of the 1.0-font-size canvas. If the engine were
        // clamping to 1.0, both second-glyph cursors would be ≈ 1.0 and the
        // ratio would be 1.0, not 0.5.
        float cursor1 = 0f;
        foreach (var el in canvas1.Elements)
            if (el.RelativeX > cursor1) cursor1 = el.RelativeX;

        float cursor05 = 0f;
        foreach (var el in canvas05.Elements)
            if (el.RelativeX > cursor05) cursor05 = el.RelativeX;

        float ratio = cursor05 / cursor1;
        Assert.IsTrue(ratio < 0.85f,
            $"Cursors did not scale with font size. 1.0-fontSize cursor={cursor1:F3}, " +
            $"0.5-fontSize cursor={cursor05:F3}, ratio={ratio:F3}. " +
            "Expected ≈ 0.5 (proportional), got ≥ 0.85 which means the engine is " +
            "still artificially capping the per-character advance at 1.0.");
    }

    [TestMethod]
    public void Measure_AndLayout_AgreeOnWidth_ForNormalText()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        using var font = FontFace.Load(FontPath);

        var entry = new TextEntry
        {
            Text = "WidthConsistency",
            FontName = font.FamilyName,
            FontSize = 0.08f,
            X = 0f, Y = 0.5f,
        };

        var engine = new NormalTypesettingEngine();
        var (measuredWidth, _) = engine.Measure(entry, font);
        var canvas = engine.Layout(entry, font);
        Assert.IsNotEmpty(canvas.Elements);

        // The last laid-out element's cursor position (RelativeX, which is
        // entry.X + accumulated advances) should match the measured width.
        float lastRelativeX = 0f;
        foreach (var el in canvas.Elements)
            if (el.RelativeX > lastRelativeX) lastRelativeX = el.RelativeX;

        // Allow a small drift equal to roughly one font size — the last
        // glyph's own advance was already counted by Measure, so the cursor
        // lands at exactly measuredWidth, while RelativeX is the baseline
        // anchor (not the right edge of the glyph body). A drift larger than
        // 1.5× font size means the two code paths disagree.
        float drift = System.MathF.Abs(lastRelativeX - measuredWidth);
        Assert.IsLessThanOrEqualTo(entry.FontSize * 1.5f, drift,
            $"Layout's last cursor position ({lastRelativeX:F4}) disagrees with " +
            $"the measured width ({measuredWidth:F4}) by {drift:F4}, which is " +
            "larger than 1.5× the font size. This usually means LayoutLine and " +
            "Measure are using different advance formulas — make sure both go " +
            "through NormalTypesettingEngine.ComputeCharacterAdvance.");
    }

    [TestMethod]
    public void Layout_NarrowGlyphs_DoNotStackAtSameX()
    {
        if (!File.Exists(FontPath))
            Assert.Inconclusive($"Arial not present at {FontPath}; skipping integration test.");

        using var font = FontFace.Load(FontPath);

        // Smoke test that the lower-bound branch (advance ≥ charFontSize * 0.1)
        // does not break the normal path. The four 'i' characters have small
        // advances in many fonts; the cursor must advance for every one.
        var entry = new TextEntry
        {
            Text = "iiii",
            FontName = font.FamilyName,
            FontSize = 0.1f,
            X = 0f, Y = 0.5f,
        };

        var canvas = new NormalTypesettingEngine().Layout(entry, font);
        Assert.IsNotEmpty(canvas.Elements);

        // All four glyphs should be at distinct cursor positions.
        var seenBuckets = new System.Collections.Generic.HashSet<int>();
        foreach (var el in canvas.Elements)
        {
            int bucket = (int)System.MathF.Round(el.RelativeX * 1000f);
            Assert.IsTrue(seenBuckets.Add(bucket),
                $"Two glyphs share the same cursor position (RelativeX ≈ " +
                $"{el.RelativeX:F4}). The cursor failed to advance — the " +
                "lower-bound clamp in ComputeCharacterAdvance is not working.");
        }
    }
}
