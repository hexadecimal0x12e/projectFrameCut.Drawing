using projectFrameCut.Drawing.Text;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class TextEntryTests
{
    // ──────────────────────────────────────────────
    //  TextEntry construction
    // ──────────────────────────────────────────────

    [TestMethod]
    public void TextEntry_Create_WithRequiredProperties()
    {
        var entry = new TextEntry
        {
            Text = "Hello",
            FontName = "Arial",
        };
        Assert.AreEqual("Hello", entry.Text);
        Assert.AreEqual("Arial", entry.FontName);
    }

    [TestMethod]
    public void TextEntry_DefaultValues()
    {
        var entry = new TextEntry
        {
            Text = "Hi",
            FontName = "Arial",
        };
        Assert.AreEqual("Regular", entry.FontStyle);
        Assert.IsEmpty(entry.FallbackFonts);
        Assert.AreEqual(0.1f, entry.FontSize);
        Assert.AreEqual(0f, entry.X);
        Assert.AreEqual(0f, entry.Y);
        Assert.AreEqual(0, entry.LayerIndex);
        Assert.AreEqual(0f, entry.Rotation);
        Assert.AreEqual((ushort)0, entry.FillR);
        Assert.AreEqual((ushort)0, entry.FillG);
        Assert.AreEqual((ushort)0, entry.FillB);
        Assert.AreEqual(1f, entry.FillA);
        Assert.AreEqual((ushort)0, entry.StrokeR);
        Assert.AreEqual((ushort)0, entry.StrokeG);
        Assert.AreEqual((ushort)0, entry.StrokeB);
        Assert.AreEqual(0f, entry.StrokeA);
        Assert.AreEqual(0f, entry.StrokeThickness);
        Assert.AreEqual(0f, entry.CharacterSpacing);
        Assert.AreEqual(0f, entry.WordSpacing);
        Assert.AreEqual(0.3f, entry.LineSpacing);
        Assert.AreEqual(TextAlignment.Left, entry.Alignment);
        Assert.AreEqual(TextDecoration.None, entry.Decoration);
    }

    [TestMethod]
    public void TextEntry_PropertyRoundTrip()
    {
        var entry = new TextEntry
        {
            Text = "Test",
            FontName = "Times New Roman",
            FontStyle = "Bold",
            FontSize = 0.05f,
            X = 0.2f,
            Y = 0.3f,
            LayerIndex = 2,
            Rotation = 45f,
            FillR = 1000,
            FillG = 2000,
            FillB = 3000,
            FillA = 0.8f,
            StrokeR = 4000,
            StrokeG = 5000,
            StrokeB = 6000,
            StrokeA = 0.5f,
            StrokeThickness = 2.5f,
            CharacterSpacing = 0.01f,
            WordSpacing = 0.02f,
            LineSpacing = 0.4f,
            Alignment = TextAlignment.Center,
            Decoration = TextDecoration.Underline | TextDecoration.Strikethrough,
        };

        Assert.AreEqual("Test", entry.Text);
        Assert.AreEqual("Times New Roman", entry.FontName);
        Assert.AreEqual("Bold", entry.FontStyle);
        Assert.AreEqual(0.05f, entry.FontSize);
        Assert.AreEqual(0.2f, entry.X);
        Assert.AreEqual(0.3f, entry.Y);
        Assert.AreEqual(2, entry.LayerIndex);
        Assert.AreEqual(45f, entry.Rotation);
        Assert.AreEqual((ushort)1000, entry.FillR);
        Assert.AreEqual((ushort)2000, entry.FillG);
        Assert.AreEqual((ushort)3000, entry.FillB);
        Assert.AreEqual(0.8f, entry.FillA);
        Assert.AreEqual((ushort)4000, entry.StrokeR);
        Assert.AreEqual((ushort)5000, entry.StrokeG);
        Assert.AreEqual((ushort)6000, entry.StrokeB);
        Assert.AreEqual(0.5f, entry.StrokeA);
        Assert.AreEqual(2.5f, entry.StrokeThickness);
        Assert.AreEqual(0.01f, entry.CharacterSpacing);
        Assert.AreEqual(0.02f, entry.WordSpacing);
        Assert.AreEqual(0.4f, entry.LineSpacing);
        Assert.AreEqual(TextAlignment.Center, entry.Alignment);
        Assert.AreEqual(TextDecoration.Underline | TextDecoration.Strikethrough, entry.Decoration);
    }

    // ──────────────────────────────────────────────
    //  HasStroke
    // ──────────────────────────────────────────────

    [TestMethod]
    public void HasStroke_NoStroke_ReturnsFalse()
    {
        var entry = new TextEntry { Text = "A", FontName = "Arial" };
        Assert.IsFalse(entry.HasStroke);
    }

    [TestMethod]
    public void HasStroke_ThicknessAndAlpha_ReturnsTrue()
    {
        var entry = new TextEntry
        {
            Text = "A",
            FontName = "Arial",
            StrokeThickness = 1f,
            StrokeA = 1f,
        };
        Assert.IsTrue(entry.HasStroke);
    }

    [TestMethod]
    public void HasStroke_ZeroThickness_ReturnsFalse()
    {
        var entry = new TextEntry
        {
            Text = "A",
            FontName = "Arial",
            StrokeThickness = 0f,
            StrokeA = 1f,
        };
        Assert.IsFalse(entry.HasStroke);
    }

    [TestMethod]
    public void HasStroke_ZeroAlpha_ReturnsFalse()
    {
        var entry = new TextEntry
        {
            Text = "A",
            FontName = "Arial",
            StrokeThickness = 1f,
            StrokeA = 0f,
        };
        Assert.IsFalse(entry.HasStroke);
    }

    [TestMethod]
    public void HasStroke_NegativeThickness_ReturnsFalse()
    {
        var entry = new TextEntry
        {
            Text = "A",
            FontName = "Arial",
            StrokeThickness = -1f,
            StrokeA = 1f,
        };
        Assert.IsFalse(entry.HasStroke);
    }

    // ──────────────────────────────────────────────
    //  Record equality
    // ──────────────────────────────────────────────

    [TestMethod]
    public void TextEntry_ValueSemantics()
    {
        var entry = new TextEntry { Text = "Hello", FontName = "Arial", FontSize = 0.2f };

        Assert.AreEqual("Hello", entry.Text);
        Assert.AreEqual("Arial", entry.FontName);
        Assert.AreEqual(0.2f, entry.FontSize);
        Assert.AreEqual(TextAlignment.Left, entry.Alignment);
        Assert.AreEqual(TextDecoration.None, entry.Decoration);
    }

    [TestMethod]
    public void TextEntry_FallbackFonts_DefaultIsEmpty()
    {
        var a = new TextEntry { Text = "Hi", FontName = "A" };
        Assert.IsEmpty(a.FallbackFonts);
    }

    // ──────────────────────────────────────────────
    //  TextAlignment enum
    // ──────────────────────────────────────────────

    [TestMethod]
    public void TextAlignment_HasThreeDistinctValues()
    {
        var values = Enum.GetValues<TextAlignment>();
        Assert.HasCount(3, values);
        Assert.Contains(TextAlignment.Left, [.. values]);
        Assert.Contains(TextAlignment.Center, [.. values]);
        Assert.Contains(TextAlignment.Right, [.. values]);
    }

    // ──────────────────────────────────────────────
    //  TextDecoration flags enum
    // ──────────────────────────────────────────────

    [TestMethod]
    public void TextDecoration_HasExpectedValues()
    {
        Assert.AreEqual("None", Enum.GetName(TextDecoration.None));
        Assert.AreEqual("Underline", Enum.GetName(TextDecoration.Underline));
        Assert.AreEqual("Strikethrough", Enum.GetName(TextDecoration.Strikethrough));
    }

    [TestMethod]
    public void TextDecoration_FlagsCombination()
    {
        var combined = TextDecoration.Underline | TextDecoration.Strikethrough;
        Assert.AreEqual(3, (int)combined);
        Assert.AreEqual(combined, Enum.Parse<TextDecoration>("Underline, Strikethrough"));
    }
}
