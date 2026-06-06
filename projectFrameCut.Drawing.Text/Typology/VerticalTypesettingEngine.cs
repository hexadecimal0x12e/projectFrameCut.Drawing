using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.Typology;

/// <summary>
/// Lays out text as a vertical column, one CJK character per row.
/// Non‑CJK characters are either kept horizontal (<paramref name="keepNonCjkHorizontal"/>)
/// or rotated 90° CW to stand upright in the column.
/// </summary>
public class VerticalTypesettingEngine
{
    /// <summary>
    /// Additional fonts tried when the primary font maps a character to
    /// <c>.notdef</c> (index 0). Consulted in order.
    /// </summary>
    public IList<FontFace> FallbackFonts { get; set; } = new List<FontFace>();

    /// <summary>
    /// Lay out <paramref name="text"/> as a vertical column of glyphs.
    /// All coordinates are in normalized 0…1 canvas space.
    /// </summary>
    public VectorPicture Layout(
        string text,
        FontFace primaryFont,
        float normalizedFontSize,
        float x,
        float y,
        float lineSpacing = 1f,
        bool keepNonCjkHorizontal = false,
        ushort fillR = 0, ushort fillG = 0, ushort fillB = 0, float fillA = 1f,
        ushort strokeR = 0, ushort strokeG = 0, ushort strokeB = 0, float strokeThickness = 0f)
    {
        var result = new VectorPicture();
        if (string.IsNullOrEmpty(text) || primaryFont is null)
            return result;

        // Normalised advance per character row
        float charAdvance = normalizedFontSize * lineSpacing;
        float cursorY = y;

        foreach (char c in text)
        {
            if (c == '\n' || c == '\r')
                continue;

            bool isCjk = IsCjkCharacter(c);
            bool needsRotation = !isCjk && !keepNonCjkHorizontal;

            var (resolvedFont, glyphIndex) = ResolveChar(c, primaryFont);
            if (glyphIndex == 0)
            {
                cursorY += charAdvance;
                continue;
            }

            Glyph? glyph = resolvedFont.GetGlyph(glyphIndex);
            if (glyph is null || glyph.IsEmpty)
            {
                cursorY += charAdvance;
                continue;
            }

            var element = new GlyphCanvasElement(glyph, resolvedFont.UnitsPerEm)
            {
                FontSize = normalizedFontSize,
                RelativeX = x,
                RelativeY = cursorY,
            };

            if (fillA > 0f)
                element.WithFill(fillR, fillG, fillB, fillA);
            if (strokeThickness > 0f)
                element.WithStroke(strokeR, strokeG, strokeB, 1f, strokeThickness);

            if (needsRotation)
            {
                // Rotate 90° CW around the cell centre so the glyph stands upright.
                element.Rotation = MathF.PI * 0.5f;
                // Shift origin so the rotated glyph stays in column.
                element.RelativeX -= normalizedFontSize * 0.5f;
                element.RelativeY += normalizedFontSize * 0.25f;
            }

            result.Elements.Add(element);
            cursorY += charAdvance;
        }

        return result;
    }

    // ──────────────────────────────────────────────
    //  Font fallback
    // ──────────────────────────────────────────────

    private (FontFace font, ushort glyphIndex) ResolveChar(char c, FontFace primaryFont)
    {
        ushort idx = primaryFont.GetGlyphIndex(c);
        if (idx != 0)
            return (primaryFont, idx);

        foreach (var fb in FallbackFonts)
        {
            if (fb is null || ReferenceEquals(fb, primaryFont))
                continue;

            idx = fb.GetGlyphIndex(c);
            if (idx != 0)
                return (fb, idx);
        }

        return (primaryFont, primaryFont.GetGlyphIndex(c));
    }

    // ──────────────────────────────────────────────
    //  CJK detection (mirror of the original TextClip logic)
    // ──────────────────────────────────────────────

    private static bool IsCjkCharacter(char c)
    {
        return (c >= '⺀' && c <= '⻿') ||  // CJK Radicals Supplement
               (c >= '⼀' && c <= '⿟') ||  // Kangxi Radicals
               (c >= '　' && c <= '〿') ||  // CJK Symbols and Punctuation
               (c >= '぀' && c <= 'ゟ') ||  // Hiragana
               (c >= '゠' && c <= 'ヿ') ||  // Katakana
               (c >= '㄀' && c <= 'ㄯ') ||  // Bopomofo
               (c >= '㈀' && c <= '㋿') ||  // Enclosed CJK Letters and Months
               (c >= '㌀' && c <= '㏿') ||  // CJK Compatibility
               (c >= '㐀' && c <= '䶿') ||  // CJK Extension A
               (c >= '一' && c <= '鿿') ||  // CJK Unified Ideographs
               (c >= '가' && c <= '힯') ||  // Hangul Syllables
               (c >= '豈' && c <= '﫿') ||  // CJK Compatibility Ideographs
               (c >= '︰' && c <= '﹏') ||  // CJK Compatibility Forms
               (c >= '＀' && c <= '￯');    // Halfwidth and Fullwidth Forms
    }
}
