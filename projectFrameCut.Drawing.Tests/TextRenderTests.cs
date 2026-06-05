using projectFrameCut.Drawing.Text;
using projectFrameCut.Drawing.Text.FontHelper.Table;
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
}
