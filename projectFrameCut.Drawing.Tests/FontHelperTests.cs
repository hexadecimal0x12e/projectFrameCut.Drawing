using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using System.Globalization;
using System.Threading;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class FontHelperTests
{
    // ──────────────────────────────────────────────
    //  TargetLanguage
    // ──────────────────────────────────────────────

    [TestMethod]
    public void TargetLanguage_Constructor_SetsProperties()
    {
        var lang = new TargetLanguage(3, 0x0409);
        Assert.AreEqual((ushort)3, lang.PlatformId);
        Assert.AreEqual((ushort)0x0409, lang.LanguageId);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_English()
    {
        var lang = new TargetLanguage(3, 0x0409);
        // DisplayName uses CultureInfo.NativeName which varies by OS locale
        Assert.AreEqual(CultureInfo.GetCultureInfo(0x0409).NativeName, lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_ChineseSimplified()
    {
        var lang = new TargetLanguage(3, 0x0804);
        Assert.AreEqual(CultureInfo.GetCultureInfo(0x0804).NativeName, lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_Japanese()
    {
        var lang = new TargetLanguage(3, 0x0411);
        Assert.AreEqual(CultureInfo.GetCultureInfo(0x0411).NativeName, lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_MacEnglish()
    {
        var lang = new TargetLanguage(1, 0);
        Assert.AreEqual("English", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_MacJapanese()
    {
        var lang = new TargetLanguage(1, 11);
        Assert.AreEqual("Japanese", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_Unicode()
    {
        var lang = new TargetLanguage(0, 0);
        Assert.AreEqual("Unicode", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_UnknownPlatform()
    {
        var lang = new TargetLanguage(99, 0x1234);
        Assert.AreEqual("Platform=99, Language=0x1234", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_UnknownWindowsLcid_FallsBack()
    {
        var lang = new TargetLanguage(3, 0x9999);
        Assert.AreEqual("LCID 0x9999", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_DisplayName_UnknownMacLang_FallsBack()
    {
        var lang = new TargetLanguage(1, 0xFF);
        Assert.AreEqual("MacLang 255", lang.DisplayName);
    }

    [TestMethod]
    public void TargetLanguage_PlatformName_Windows()
    {
        var lang = new TargetLanguage(3, 0x0409);
        Assert.AreEqual("Windows", lang.PlatformName);
    }

    [TestMethod]
    public void TargetLanguage_PlatformName_Macintosh()
    {
        var lang = new TargetLanguage(1, 0);
        Assert.AreEqual("Macintosh", lang.PlatformName);
    }

    [TestMethod]
    public void TargetLanguage_PlatformName_Unicode()
    {
        var lang = new TargetLanguage(0, 0);
        Assert.AreEqual("Unicode", lang.PlatformName);
    }

    [TestMethod]
    public void TargetLanguage_PlatformName_Unknown()
    {
        var lang = new TargetLanguage(5, 0);
        Assert.AreEqual("Unknown platform 5", lang.PlatformName);
    }

    [TestMethod]
    public void TargetLanguage_ToString_ReturnsDisplayName()
    {
        var lang = new TargetLanguage(3, 0x0409);
        Assert.AreEqual(lang.DisplayName, lang.ToString());
    }

    [TestMethod]
    public void TargetLanguage_ValueEquality()
    {
        var a = new TargetLanguage(3, 0x0409);
        var b = new TargetLanguage(3, 0x0409);
        var c = new TargetLanguage(3, 0x040C);

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ──────────────────────────────────────────────
    //  InvalidFontFileException
    // ──────────────────────────────────────────────

    [TestMethod]
    public void InvalidFontFileException_Message()
    {
        var ex = new InvalidFontFileException("bad font");
        Assert.AreEqual("bad font", ex.Message);
    }

    [TestMethod]
    public void InvalidFontFileException_InnerException()
    {
        var inner = new Exception("inner");
        var ex = new InvalidFontFileException("bad font", inner);
        Assert.AreSame(inner, ex.InnerException);
    }

    [TestMethod]
    public void InvalidFontFileException_InheritsFromIOException()
    {
        var ex = new InvalidFontFileException("test");
        Assert.IsInstanceOfType(ex, typeof(IOException));
    }

    // ──────────────────────────────────────────────
    //  GlyphNotFoundException
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphNotFoundException_DefaultConstructor()
    {
        var ex = new GlyphNotFoundException();
        Assert.IsNull(ex.Glyph);
        Assert.IsNull(ex.Font);
    }

    [TestMethod]
    public void GlyphNotFoundException_Message()
    {
        var ex = new GlyphNotFoundException("not found");
        Assert.AreEqual("not found", ex.Message);
    }

    [TestMethod]
    public void GlyphNotFoundException_MessageAndInnerException()
    {
        var inner = new Exception("inner");
        var ex = new GlyphNotFoundException("not found", inner);
        Assert.AreSame(inner, ex.InnerException);
    }

    [TestMethod]
    public void GlyphNotFoundException_CharAndFont_SetsProperties()
    {
        var ex = new GlyphNotFoundException('A', "Arial");
        Assert.AreEqual('A', ex.Glyph);
        Assert.AreEqual("Arial", ex.Font);
    }

    [TestMethod]
    public void GlyphNotFoundException_CharAndFont_MessageFormat()
    {
        var ex = new GlyphNotFoundException('A', "Arial");
        Assert.AreEqual("The character 'A (0x000041)' was not found in the font 'Arial'.", ex.Message);
    }

    [TestMethod]
    public void GlyphNotFoundException_ControlChar_Message_ContainsControlHexAndFont()
    {
        var ex = new GlyphNotFoundException('\0', "Test");
        Assert.IsTrue(ex.Message.Contains("Control char 0x"));
        Assert.IsTrue(ex.Message.Contains("Test"));
    }

    [TestMethod]
    public void GlyphNotFoundException_NullFont_MessageFormat()
    {
        var ex = new GlyphNotFoundException('B', null);
        Assert.AreEqual("The character 'B (0x000042)' was not found in the font '<Unknown font>'.", ex.Message);
    }

    // ──────────────────────────────────────────────
    //  GlyphPoint
    // ──────────────────────────────────────────────

    [TestMethod]
    public void GlyphPoint_Constructor()
    {
        var pt = new GlyphPoint(10, 20, true);
        Assert.AreEqual((short)10, pt.X);
        Assert.AreEqual((short)20, pt.Y);
        Assert.IsTrue(pt.OnCurve);
    }

    [TestMethod]
    public void GlyphPoint_OffCurve()
    {
        var pt = new GlyphPoint(-5, 100, false);
        Assert.AreEqual((short)(-5), pt.X);
        Assert.AreEqual((short)100, pt.Y);
        Assert.IsFalse(pt.OnCurve);
    }

    [TestMethod]
    public void GlyphPoint_ValueEquality()
    {
        var a = new GlyphPoint(1, 2, true);
        var b = new GlyphPoint(1, 2, true);
        var c = new GlyphPoint(1, 2, false);

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ──────────────────────────────────────────────
    //  VariationAxis
    // ──────────────────────────────────────────────

    [TestMethod]
    public void VariationAxis_Constructor()
    {
        var axis = new VariationAxis("wght", 100f, 400f, 900f, 1);
        Assert.AreEqual("wght", axis.Tag);
        Assert.AreEqual(100f, axis.MinValue);
        Assert.AreEqual(400f, axis.DefaultValue);
        Assert.AreEqual(900f, axis.MaxValue);
        Assert.AreEqual(1, axis.AxisValueNameId);
    }

    [TestMethod]
    public void VariationAxis_Deconstruct()
    {
        var axis = new VariationAxis("wdth", 50f, 100f, 200f, 2);
        var (tag, minVal, defVal, maxVal, nameId) = axis;
        Assert.AreEqual("wdth", tag);
        Assert.AreEqual(50f, minVal);
        Assert.AreEqual(100f, defVal);
        Assert.AreEqual(200f, maxVal);
        Assert.AreEqual(2, nameId);
    }

    [TestMethod]
    public void VariationAxis_ValueEquality()
    {
        var a = new VariationAxis("wght", 100f, 400f, 900f, 1);
        var b = new VariationAxis("wght", 100f, 400f, 900f, 1);
        var c = new VariationAxis("wght", 100f, 500f, 900f, 1);

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }

    // ──────────────────────────────────────────────
    //  NamedInstance
    // ──────────────────────────────────────────────

    [TestMethod]
    public void NamedInstance_Constructor()
    {
        var coords = new[] { 400f, 100f };
        var instance = new NamedInstance("Regular", coords);
        Assert.AreEqual("Regular", instance.SubfamilyName);
        Assert.AreSame(coords, instance.Coordinates);
    }

    [TestMethod]
    public void NamedInstance_EmptyCoordinates()
    {
        var instance = new NamedInstance("Default", Array.Empty<float>());
        Assert.IsEmpty(instance.Coordinates);
    }

    [TestMethod]
    public void NamedInstance_ValueSemantics()
    {
        var coordsA = new[] { 400f };
        var coordsB = new[] { 400f };
        var a = new NamedInstance("Regular", coordsA);
        var b = new NamedInstance("Regular", coordsB);

        // Records with IReadOnlyList use reference equality for the list
        Assert.AreEqual("Regular", a.SubfamilyName);
        Assert.AreEqual("Regular", b.SubfamilyName);
        Assert.AreNotSame(coordsA, coordsB);
        CollectionAssert.AreEqual(coordsA, coordsB.ToArray());
    }

    // ──────────────────────────────────────────────
    //  FontCollection.Load (exception paths)
    // ──────────────────────────────────────────────

    [TestMethod]
    public void FontCollection_Load_EmptyData_Throws()
    {
        Assert.ThrowsExactly<InvalidFontFileException>(() =>
            FontCollection.Load(Array.Empty<byte>()));
    }

    [TestMethod]
    public void FontCollection_Load_WrongTag_Throws()
    {
        // "abcd" is not "ttcf"
        var data = "abcd"u8.ToArray();
        Assert.ThrowsExactly<InvalidFontFileException>(() =>
            FontCollection.Load(data));
    }

    // ──────────────────────────────────────────────
    //  FontFace.Load (exception paths)
    // ──────────────────────────────────────────────

    [TestMethod]
    public void FontFace_Load_EmptyData_Throws()
    {
        Assert.ThrowsExactly<InvalidFontFileException>(() =>
            FontFace.Load(Array.Empty<byte>()));
    }

    [TestMethod]
    public void FontFace_Load_InvalidSfVersion_Throws()
    {
        // "abcd" is not a valid SFNT version
        var data = new byte[] { 0x61, 0x62, 0x63, 0x64, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.ThrowsExactly<InvalidFontFileException>(() =>
            FontFace.Load(data));
    }

    [TestMethod]
    public void FontFace_Load_WoffData_Throws()
    {
        // "wOFF" tag should give a clear error
        var data = new byte[] { 0x77, 0x4F, 0x46, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        // Data too small, will throw from SfntReader constructor first
        Assert.ThrowsExactly<InvalidFontFileException>(() =>
            FontFace.Load(data));
    }

    [TestMethod]
    [DoNotParallelize]
    public void FontFace_AutoDispose_GlobalAndPerInstanceEnabled_UnloadsAndReloads()
    {
        const string fontPath = @"C:\Windows\Fonts\arial.ttf";
        if (!File.Exists(fontPath))
            Assert.Inconclusive($"Arial not present at {fontPath}; skipping integration test.");

        bool globalBackup = FontFace.GlobalAutoDisposeEnabled;
        TimeSpan timeoutBackup = FontFace.AutoDisposeIdleTimeout;

        try
        {
            using var font = FontFace.Load(fontPath);
            font.AutoDisposeEnabled = true;

            FontFace.GlobalAutoDisposeEnabled = true;
            FontFace.AutoDisposeIdleTimeout = TimeSpan.FromMilliseconds(1);

            Thread.Sleep(10);
            FontFace.RunAutoDisposeSweep();

            Assert.IsNull(GetSfntReaderField(font));

            _ = font.TryGetGlyphBounds(1, out _, out _, out _, out _);
            Assert.IsNotNull(GetSfntReaderField(font));
        }
        finally
        {
            FontFace.GlobalAutoDisposeEnabled = globalBackup;
            FontFace.AutoDisposeIdleTimeout = timeoutBackup;
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void FontFace_AutoDispose_PerInstanceDisabled_DoesNotUnload()
    {
        const string fontPath = @"C:\Windows\Fonts\arial.ttf";
        if (!File.Exists(fontPath))
            Assert.Inconclusive($"Arial not present at {fontPath}; skipping integration test.");

        bool globalBackup = FontFace.GlobalAutoDisposeEnabled;
        TimeSpan timeoutBackup = FontFace.AutoDisposeIdleTimeout;

        try
        {
            using var font = FontFace.Load(fontPath);
            font.AutoDisposeEnabled = false;

            FontFace.GlobalAutoDisposeEnabled = true;
            FontFace.AutoDisposeIdleTimeout = TimeSpan.FromMilliseconds(1);

            Thread.Sleep(10);
            FontFace.RunAutoDisposeSweep();

            Assert.IsNotNull(GetSfntReaderField(font));
        }
        finally
        {
            FontFace.GlobalAutoDisposeEnabled = globalBackup;
            FontFace.AutoDisposeIdleTimeout = timeoutBackup;
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void FontFace_AutoDispose_GlobalDisabled_DoesNotUnload()
    {
        const string fontPath = @"C:\Windows\Fonts\arial.ttf";
        if (!File.Exists(fontPath))
            Assert.Inconclusive($"Arial not present at {fontPath}; skipping integration test.");

        bool globalBackup = FontFace.GlobalAutoDisposeEnabled;
        TimeSpan timeoutBackup = FontFace.AutoDisposeIdleTimeout;

        try
        {
            using var font = FontFace.Load(fontPath);
            font.AutoDisposeEnabled = true;

            FontFace.GlobalAutoDisposeEnabled = false;
            FontFace.AutoDisposeIdleTimeout = TimeSpan.FromMilliseconds(1);

            Thread.Sleep(10);
            FontFace.RunAutoDisposeSweep();

            Assert.IsNotNull(GetSfntReaderField(font));
        }
        finally
        {
            FontFace.GlobalAutoDisposeEnabled = globalBackup;
            FontFace.AutoDisposeIdleTimeout = timeoutBackup;
        }
    }

    private static object? GetSfntReaderField(FontFace font)
    {
        var field = typeof(FontFace).GetField("_sfnt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(font);
    }
}
