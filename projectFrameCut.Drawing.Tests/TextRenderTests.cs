using projectFrameCut.Drawing.Text;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Vector;
using System.Reflection;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class TextRenderTests
{
    // ──────────────────────────────────────────────
    //  GlyphCanvasElement constructor validation
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphCanvasElement_NullGlyph_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new GlyphCanvasElement(null!, 1000));
    }

    [TestMethod]
    public void GlyphCanvasElement_ZeroUnitsPerEm_ThrowsArgumentException()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new GlyphCanvasElement(CreateEmptyGlyph(), 0));

        Assert.AreEqual("unitsPerEm", ex.ParamName);
    }

    // ──────────────────────────────────────────────
    //  GlyphCanvasElement.Draw
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphCanvasElement_EmptyGlyph_Draw_ReturnsEmptyArray()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        element.WithFill(255, 0, 0);

        Assert.IsEmpty(element.Draw());
    }

    [TestMethod]
    public void GlyphCanvasElement_Draw_ClassifiesHoleByOppositeWinding()
    {
        // Standard font convention: hole contours are wound opposite to their outer.
        // Outer: CW in Y-down (positive area); hole: CCW in Y-down (negative area).
        var glyph = CreateGlyph(
        [
            [
                new GlyphPoint(0, 0, true),
                new GlyphPoint(100, 0, true),
                new GlyphPoint(100, 100, true),
                new GlyphPoint(0, 100, true),
            ],
            // CCW (reversed) inner square — a proper counter/hole.
            [
                new GlyphPoint(25, 25, true),
                new GlyphPoint(25, 75, true),
                new GlyphPoint(75, 75, true),
                new GlyphPoint(75, 25, true),
            ],
        ]);

        var element = new GlyphCanvasElement(glyph, 1000).WithFill(1, 2, 3, 1f);

        var fills = element.Draw().OfType<PolygonVectorSegment>().Where(s => s.FillA > 0f).ToArray();

        Assert.AreEqual(1, fills.Length);
        Assert.IsNotNull(fills[0].Holes);
        Assert.AreEqual(1, fills[0].Holes!.Length);
    }

    [TestMethod]
    public void GlyphCanvasElement_Draw_SameWindingInner_TreatedAsOuterFill_NotHole()
    {
        // Same-winding inner contours are overlapping filled strokes (e.g. CFF fonts like
        // Source Han Serif). They must NOT be punched as holes, which would create white gaps
        // at stroke intersections. Instead they are promoted to independent outer fills.
        var glyph = CreateGlyph(
        [
            [
                new GlyphPoint(0, 0, true),
                new GlyphPoint(100, 0, true),
                new GlyphPoint(100, 100, true),
                new GlyphPoint(0, 100, true),
            ],
            // CW inner square (same winding as outer) — overlapping stroke, not a hole.
            [
                new GlyphPoint(25, 25, true),
                new GlyphPoint(75, 25, true),
                new GlyphPoint(75, 75, true),
                new GlyphPoint(25, 75, true),
            ],
        ]);

        var element = new GlyphCanvasElement(glyph, 1000).WithFill(1, 2, 3, 1f);

        var fills = element.Draw().OfType<PolygonVectorSegment>().Where(s => s.FillA > 0f).ToArray();

        // Both contours should be filled independently — no holes punched.
        Assert.AreEqual(2, fills.Length);
        Assert.IsTrue(fills.All(f => f.Holes == null || f.Holes.Length == 0),
            "Same-winding inner contour must not be added as a hole.");
    }

    [TestMethod]
    public void GlyphCanvasElement_Draw_AssignsHoleToContainingOuter()
    {
        var glyph = CreateGlyph(
        [
            [
                new GlyphPoint(0, 0, true),
                new GlyphPoint(80, 0, true),
                new GlyphPoint(80, 80, true),
                new GlyphPoint(0, 80, true),
            ],
            // CCW inner — a proper hole for the first outer.
            [
                new GlyphPoint(20, 20, true),
                new GlyphPoint(20, 60, true),
                new GlyphPoint(60, 60, true),
                new GlyphPoint(60, 20, true),
            ],
            [
                new GlyphPoint(120, 0, true),
                new GlyphPoint(200, 0, true),
                new GlyphPoint(200, 80, true),
                new GlyphPoint(120, 80, true),
            ],
            // CCW inner — a proper hole for the second outer.
            [
                new GlyphPoint(140, 20, true),
                new GlyphPoint(140, 60, true),
                new GlyphPoint(180, 60, true),
                new GlyphPoint(180, 20, true),
            ],
        ]);

        var element = new GlyphCanvasElement(glyph, 1000).WithFill(1, 2, 3, 1f);

        var fills = element.Draw().OfType<PolygonVectorSegment>().Where(s => s.FillA > 0f).ToArray();

        Assert.AreEqual(2, fills.Length);
        CollectionAssert.AreEquivalent(new[] { 1, 1 }, fills.Select(f => f.Holes?.Length ?? 0).ToArray());
    }

    [TestMethod]
    public void GlyphCanvasElement_Draw_DoesNotTreatPartialOverlapAsHole()
    {
        var glyph = CreateGlyph(
        [
            [
                new GlyphPoint(0, 0, true),
                new GlyphPoint(100, 0, true),
                new GlyphPoint(100, 100, true),
                new GlyphPoint(0, 100, true),
            ],
            [
                new GlyphPoint(75, 25, true),
                new GlyphPoint(150, 25, true),
                new GlyphPoint(150, 100, true),
                new GlyphPoint(75, 100, true),
            ],
        ]);

        var element = new GlyphCanvasElement(glyph, 1000).WithFill(1, 2, 3, 1f);

        var fills = element.Draw().OfType<PolygonVectorSegment>().Where(s => s.FillA > 0f).ToArray();

        Assert.AreEqual(2, fills.Length);
        CollectionAssert.AreEquivalent(new[] { 0, 0 }, fills.Select(f => f.Holes?.Length ?? 0).ToArray());
    }

    // ──────────────────────────────────────────────
    //  Fluent API
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphCanvasElement_WithPosition_SetsCoordinates()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        var result = element.WithPosition(0.25f, 0.5f);

        Assert.AreEqual(0.25f, element.RelativeX);
        Assert.AreEqual(0.5f, element.RelativeY);
        Assert.AreSame(element, result);
    }

    [TestMethod]
    public void GlyphCanvasElement_WithLayer_SetsLayerIndex()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        var result = element.WithLayer(3);

        Assert.AreEqual(3, element.LayerIndex);
        Assert.AreSame(element, result);
    }

    [TestMethod]
    public void GlyphCanvasElement_WithFontSize_SetsFontSize()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        var result = element.WithFontSize(0.25f);

        Assert.AreEqual(0.25f, element.FontSize);
        Assert.AreSame(element, result);
    }

    [TestMethod]
    public void GlyphCanvasElement_WithFill_SetsFillColors()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        var result = element.WithFill(100, 200, 300, 0.5f);

        Assert.AreEqual((ushort)100, element.FillR);
        Assert.AreEqual((ushort)200, element.FillG);
        Assert.AreEqual((ushort)300, element.FillB);
        Assert.AreEqual(0.5f, element.FillA);
        Assert.AreSame(element, result);
    }

    [TestMethod]
    public void GlyphCanvasElement_WithStroke_SetsStrokeProperties()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);
        var result = element.WithStroke(10, 20, 30, 0.7f, 3f);

        Assert.AreEqual((ushort)10, element.StrokeR);
        Assert.AreEqual((ushort)20, element.StrokeG);
        Assert.AreEqual((ushort)30, element.StrokeB);
        Assert.AreEqual(0.7f, element.StrokeA);
        Assert.AreEqual(3f, element.StrokeThickness);
        Assert.AreSame(element, result);
    }

    [TestMethod]
    public void GlyphCanvasElement_DefaultProperties()
    {
        var element = new GlyphCanvasElement(CreateEmptyGlyph(), 1000);

        Assert.AreEqual(0.1f, element.FontSize);
        Assert.AreEqual((ushort)0, element.FillR);
        Assert.AreEqual((ushort)0, element.FillG);
        Assert.AreEqual((ushort)0, element.FillB);
        Assert.AreEqual(1f, element.FillA);
        Assert.AreEqual((ushort)0, element.StrokeR);
        Assert.AreEqual((ushort)0, element.StrokeG);
        Assert.AreEqual((ushort)0, element.StrokeB);
        Assert.AreEqual(1f, element.StrokeA);
        Assert.AreEqual(1f, element.StrokeThickness);
    }

    /// <summary>
    /// Create a Glyph instance via reflection to work around the internal constructor.
    /// Produces an empty glyph (OutlineType = -1, no contours).
    /// </summary>
    private static Glyph CreateEmptyGlyph()
    {
        var ctor = typeof(Glyph).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance)[0];

        return (Glyph)ctor.Invoke([
            -1,                          // outlineType (empty)
            Array.Empty<GlyphPoint[]>(),  // contours
            (short)0, (short)0,          // xMin, yMin
            (short)0, (short)0,          // xMax, yMax
        ]);
    }

    private static Glyph CreateGlyph(params GlyphPoint[][] contours)
    {
        var ctor = typeof(Glyph).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance)[0];

        short xMin = contours.SelectMany(c => c).Min(p => p.X);
        short yMin = contours.SelectMany(c => c).Min(p => p.Y);
        short xMax = contours.SelectMany(c => c).Max(p => p.X);
        short yMax = contours.SelectMany(c => c).Max(p => p.Y);

        return (Glyph)ctor.Invoke([
            0,
            contours,
            xMin,
            yMin,
            xMax,
            yMax,
        ]);
    }
}
