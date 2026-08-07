using System.Text;
using projectFrameCut.Drawing.Text;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EmojiTextTests
{
    private const string EmojiPath = @"C:\Windows\Fonts\seguiemj.ttf";
    private const string TextPath = @"C:\Windows\Fonts\arial.ttf";

    [TestMethod]
    public void FontFace_RuneApi_MapsSupplementaryEmoji()
    {
        RequireFonts();
        using var emoji = FontFace.Load(EmojiPath);
        Assert.AreNotEqual((ushort)0, emoji.GetGlyphIndex(new Rune(0x1F600)));
        Assert.IsTrue(emoji.CanDisplayTheChar(new Rune(0x1F600)));
    }

    [TestMethod]
    public void HorizontalLayout_SupplementaryEmoji_IsOneColorElement()
    {
        RequireFonts();
        using var text = FontFace.Load(TextPath);
        using var emoji = FontFace.Load(EmojiPath);
        FontFace? previous = FontFace.EmojiFont;
        try
        {
            FontFace.EmojiFont = emoji;
            var entry = new TextEntry { Text = "A😀B", FontName = text.FamilyName, FontSize = .1f, FillA = 1f };
            var picture = new NormalTypesettingEngine().Layout(entry, text);
            Assert.AreEqual(3, picture.Elements.Count);
            Assert.AreEqual(1, picture.Elements.Count(e => e is ColorGlyphCanvasElement));
        }
        finally { FontFace.EmojiFont = previous; }
    }

    [TestMethod]
    public void VerticalLayout_ZwjSequence_OccupiesOneCellWhenFontSupportsIt()
    {
        RequireFonts();
        using var text = FontFace.Load(TextPath);
        using var emoji = FontFace.Load(EmojiPath);
        FontFace? previous = FontFace.EmojiFont;
        try
        {
            FontFace.EmojiFont = emoji;
            var entry = new TextEntry { Text = "👨‍👩‍👧‍👦", FontName = text.FamilyName, FontSize = .1f, FillA = 1f };
            var picture = new VerticalTypesettingEngine().Layout(entry, text);
            Assert.AreEqual(1, picture.Elements.Count);
            Assert.IsInstanceOfType<ColorGlyphCanvasElement>(picture.Elements[0]);
            Assert.AreEqual(0f, picture.Elements[0].RelativeY);
        }
        finally { FontFace.EmojiFont = previous; }
    }

    [TestMethod]
    public void LineBreak_DoesNotSplitEmojiGraphemeCluster()
    {
        RequireFonts();
        using var text = FontFace.Load(TextPath);
        var entry = new TextEntry { Text = "A👨‍👩‍👧‍👦B", FontName = text.FamilyName, FontSize = .1f };
        string broken = LineBreakHandler.BreakLine(entry, text, .11f);
        Assert.IsTrue(broken.Contains("👨‍👩‍👧‍👦"));
    }

    [TestMethod]
    public void MissingEmojiFont_DoesNotSplitSurrogatePairIntoTwoFallbackGlyphs()
    {
        RequireFonts();
        using var text = FontFace.Load(TextPath);
        FontFace? previous = FontFace.EmojiFont;
        try
        {
            FontFace.EmojiFont = null;
            var picture = new NormalTypesettingEngine().Layout(
                new TextEntry { Text = "😀", FontName = text.FamilyName, FontSize = .1f, FillA = 1f }, text);
            Assert.IsLessThanOrEqualTo(1, picture.Elements.Count);
        }
        finally { FontFace.EmojiFont = previous; }
    }

    private static void RequireFonts()
    {
        if (!File.Exists(EmojiPath) || !File.Exists(TextPath))
            Assert.Inconclusive("Segoe UI Emoji or Arial is unavailable on this system.");
    }
}
