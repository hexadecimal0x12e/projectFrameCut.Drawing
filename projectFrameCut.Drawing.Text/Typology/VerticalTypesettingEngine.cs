using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Vector;
using System.Text;

namespace projectFrameCut.Drawing.Text.Typology;

/// <summary>
/// Lays out text as a vertical column, one CJK character per row.
/// A newline character starts a new column; the <c>lineSpacing</c> parameter
/// controls the gap between adjacent columns.
/// Non‑CJK characters are either kept horizontal (<paramref name="keepNonCjkHorizontal"/>)
/// or rotated 90° CW to stand upright in the column.
/// </summary>
public class VerticalTypesettingEngine : ITypesettingEngine
{
    /// <summary>
    /// Additional fonts tried when the primary font maps a character to
    /// <c>.notdef</c> (index 0). Consulted in order.
    /// </summary>
    public IList<FontFace> FallbackFonts { get; set; } = new List<FontFace>();

    public VectorPicture Layout(
        string text, FontFace primaryFont, float normalizedFontSize, float x, float y,
        float lineSpacing = 1f, bool keepNonCjkHorizontal = false,
        ushort fillR = 0, ushort fillG = 0, ushort fillB = 0, float fillA = 1f,
        ushort strokeR = 0, ushort strokeG = 0, ushort strokeB = 0, float strokeThickness = 0f,
        TextFlowDirection flowDirection = TextFlowDirection.LeftToRight)
    {
        var entry = new TextEntry { Text = text, FontName = primaryFont.FamilyName,
            FontSize = normalizedFontSize, X = x, Y = y,
            LineSpacing = lineSpacing, FillR = fillR, FillG = fillG, FillB = fillB, FillA = fillA,
            StrokeR = strokeR, StrokeG = strokeG, StrokeB = strokeB, StrokeThickness = strokeThickness,
            FlowDirection = flowDirection };
        entry.ExtraData["keepNonCjkHorizontal"] = keepNonCjkHorizontal;
        return LayoutUnicode(entry, primaryFont);
    }

    /// <summary>
    /// Lay out <paramref name="text"/> as a vertical column of glyphs.
    /// Newline characters advance to the next column (right for LTR, left for RTL).
    /// All coordinates are in normalized 0…1 canvas space.
    /// </summary>
    private VectorPicture LayoutLegacy(
        string text,
        FontFace primaryFont,
        float normalizedFontSize,
        float x,
        float y,
        float lineSpacing = 1f,
        bool keepNonCjkHorizontal = false,
        ushort fillR = 0, ushort fillG = 0, ushort fillB = 0, float fillA = 1f,
        ushort strokeR = 0, ushort strokeG = 0, ushort strokeB = 0, float strokeThickness = 0f,
        TextFlowDirection flowDirection = TextFlowDirection.LeftToRight)
    {
        var result = new VectorPicture();
        if (string.IsNullOrEmpty(text) || primaryFont is null)
            return result;

        // Vertical advance per character within a column (no extra spacing).
        float charAdvance = normalizedFontSize;
        // Horizontal distance between columns: lineSpacing controls the gap.
        float columnWidth = normalizedFontSize * (1f + lineSpacing);
        float cursorY = 0f;
        // Column offset in uniform space so it scales with min(canvasW,canvasH),
        // matching the glyph body scale from GlyphCanvasElement.UseUniformScale.
        float columnOffset = 0f;

        foreach (char c in text)
        {
            if (c == '\n' || c == '\r')
            {
                cursorY = 0f;
                columnOffset += flowDirection == TextFlowDirection.RightToLeft
                    ? -columnWidth
                    : columnWidth;
                continue;
            }

            bool isCjk = IsCjkCharacter(c);
            bool needsRotation = !isCjk && !keepNonCjkHorizontal;

            var (resolvedFont, glyphIndex) = ResolveChar(c, primaryFont);
            if (glyphIndex == 0)
            {
                cursorY += charAdvance;
                continue;
            }

            Glyph? glyph = resolvedFont.GetVariedGlyph(glyphIndex);
            if (glyph is null || glyph.IsEmpty)
            {
                cursorY += charAdvance;
                continue;
            }

            var element = new GlyphCanvasElement(glyph, resolvedFont.UnitsPerEm)
            {
                FontSize = normalizedFontSize,
                BaseX = x,
                BaseY = y,
                RelativeX = columnOffset,
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

    /// <inheritdoc/>
    public VectorPicture Layout(TextEntry entry, FontFace font)
    {
        return LayoutUnicode(entry, font);
    }

    /// <inheritdoc/>
    public (float width, float height) Measure(TextEntry entry, FontFace font)
    {
        if (string.IsNullOrEmpty(entry.Text))
            return (0f, 0f);

        float charAdvance = entry.FontSize;
        float columnWidth = entry.FontSize * (1f + entry.LineSpacing);

        var clusters = UnicodeTextPipeline.Resolve(entry.Text, font, FallbackFonts, entry.FillR, entry.FillG, entry.FillB);
        var lines = new List<int> { 0 };
        foreach (var c in clusters) { if (c.IsNewLine) lines.Add(0); else lines[^1]++; }
        float maxHeight = 0f;

        foreach (int count in lines)
        {
            float columnHeight = count * charAdvance;
            if (columnHeight > maxHeight)
                maxHeight = columnHeight;
        }

        float totalWidth = lines.Count * columnWidth;
        return (totalWidth, maxHeight);
    }

    private VectorPicture LayoutUnicode(TextEntry entry, FontFace primaryFont)
    {
        var result = new VectorPicture();
        if (string.IsNullOrEmpty(entry.Text)) return result;
        bool keep = entry.ExtraData.TryGetValue("keepNonCjkHorizontal", out var v) && v is true;
        var clusters = UnicodeTextPipeline.Resolve(entry.Text, primaryFont, FallbackFonts, entry.FillR, entry.FillG, entry.FillB);
        float y = 0, column = 0, columnWidth = entry.FontSize * (1 + entry.LineSpacing);
        foreach (var c in clusters)
        {
            if (c.IsNewLine) { y = 0; column += entry.FlowDirection == TextFlowDirection.RightToLeft ? -columnWidth : columnWidth; continue; }
            if (c.Font is null) { y += entry.FontSize; continue; }
            VectorCanvasElement? element = null;
            if (c.IsColorEmoji)
                element = new ColorGlyphCanvasElement(c.Font, c.ColorLayers!, entry.FontSize, entry.FillA);
            else
            {
                var glyph = c.Font.GetVariedGlyph(c.GlyphIndex);
                if (glyph is not null && !glyph.IsEmpty)
                {
                    var mono = new GlyphCanvasElement(glyph, c.Font.UnitsPerEm) { FontSize = entry.FontSize };
                    mono.WithFill(entry.FillR, entry.FillG, entry.FillB, entry.FillA);
                    if (entry.StrokeThickness > 0) mono.WithStroke(entry.StrokeR, entry.StrokeG, entry.StrokeB, entry.StrokeA, entry.StrokeThickness);
                    if (!keep && c.Rune is Rune rune && !IsCjkCharacter(rune)) mono.Rotation = MathF.PI * .5f;
                    element = mono;
                }
            }
            if (element is not null)
            {
                element.BaseX = entry.X; element.BaseY = entry.Y; element.RelativeX = column;
                element.RelativeY = y; element.LayerIndex = entry.LayerIndex; element.Rotation += entry.Rotation;
                result.Elements.Add(element);
            }
            y += entry.FontSize;
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

    private static bool IsCjkCharacter(Rune rune) => rune.IsBmp && IsCjkCharacter((char)rune.Value);
}
