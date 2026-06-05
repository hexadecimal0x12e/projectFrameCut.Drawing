using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.Typology;

/// <summary>
/// Lays out text from a <see cref="TextEntry"/> (or <see cref="RichTextEntry"/>)
/// using a primary <see cref="FontFace"/> and produces a <see cref="VectorPicture"/>
/// where each visible glyph is a positioned <see cref="GlyphCanvasElement"/>.
/// </summary>
public class NormalTypesettingEngine : ITypesettingEngine
{
    // ──────────────────────────────────────────────
    //  Font fallback
    // ──────────────────────────────────────────────

    /// <summary>
    /// Additional fonts tried when the primary font maps a character to
    /// the <c>.notdef</c> glyph (index 0). Fonts are consulted in order;
    /// the first one that maps the character to a real glyph wins.
    /// </summary>
    public IList<FontFace> FallbackFonts { get; set; } = new List<FontFace>();

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Lay out the text in <paramref name="entry"/> using the specified
    /// <paramref name="font"/> (and any <see cref="FallbackFonts"/>).
    /// All rendering parameters are read from the entry.
    /// </summary>
    public VectorPicture Layout(TextEntry entry, FontFace font)
    {
        var result = new VectorPicture();
        if (string.IsNullOrEmpty(entry.Text))
            return result;

        float lineHeight = entry.FontSize * (1f + entry.LineSpacing);

        ushort spaceGlyphIndex = font.GetGlyphIndex(' ');
        float spaceAdvanceWidth = font.GetAdvanceWidth(spaceGlyphIndex) *
                                  (entry.FontSize / font.UnitsPerEm);

        var lines = entry.Text.Split('\n');
        float baselineY = 0;

        foreach (var line in lines)
        {
            LayoutLine(line, font, spaceAdvanceWidth, baselineY, result, entry);
            baselineY += lineHeight;
        }

        foreach (var element in result.Elements)
        {
            element.RelativeX += entry.X;
            element.RelativeY += entry.Y;
            element.LayerIndex = entry.LayerIndex;
            element.Rotation = entry.Rotation;
        }

        return result;
    }

    /// <inheritdoc/>
    public (float width, float height) Measure(TextEntry entry, FontFace font)
    {
        if (string.IsNullOrEmpty(entry.Text))
            return (0f, 0f);

        float lineHeight = entry.FontSize * (1f + entry.LineSpacing);

        ushort spaceGlyphIndex = font.GetGlyphIndex(' ');
        float spaceAdvanceWidth = font.GetAdvanceWidth(spaceGlyphIndex) *
                                  (entry.FontSize / font.UnitsPerEm);

        var rich = entry as RichTextEntry;
        var lines = entry.Text.Split('\n');
        float maxWidth = 0f;

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            float lineWidth = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Per-character style overrides (RichTextEntry)
                float charFontSize = entry.FontSize;
                float charCharSpacing = entry.CharacterSpacing;
                float charWordSpacing = entry.WordSpacing;
                var charVariationAxes = entry.VariationAxes;

                if (rich is not null)
                {
                    foreach (var range in rich.GetRangesAt(i))
                    {
                        var s = range.Style;
                        if (s.FontSize.HasValue) charFontSize = s.FontSize.Value;
                        if (s.CharacterSpacing.HasValue) charCharSpacing = s.CharacterSpacing.Value;
                        if (s.WordSpacing.HasValue) charWordSpacing = s.WordSpacing.Value;
                        if (s.VariationAxes is not null) charVariationAxes = s.VariationAxes;
                    }
                }

                if (c == ' ')
                {
                    float spaceScale = charFontSize / entry.FontSize;
                    lineWidth += spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                }
                else
                {
                    var (rFont, rIdx) = ResolveChar(c, font);

                    if (rFont.IsVariableFont && charVariationAxes.Count > 0)
                        rFont.SetVariationAxes(charVariationAxes);

                    float charScale = charFontSize / rFont.UnitsPerEm;
                    float advance = rFont.GetVariedAdvanceWidth(rIdx) * charScale + charCharSpacing;
                    // 兜底：同 LayoutLine，防止字体返回 0 把 lineWidth 算成 0
                    if (advance < charFontSize * 0.1f)
                        advance = charFontSize;
                    lineWidth += advance;
                }
            }

            if (lineWidth > maxWidth)
                maxWidth = lineWidth;
        }

        float totalHeight = lines.Length * lineHeight;
        return (maxWidth, totalHeight);
    }

    // ──────────────────────────────────────────────
    //  Internal
    // ──────────────────────────────────────────────

    /// <summary>
    /// Walk the font chain (primary -> fallbacks) and return the first
    /// <see cref="FontFace"/> that maps <paramref name="c"/> to a glyph
    /// other than <c>.notdef</c> (index 0). If none match, the primary
    /// font is returned so its <c>.notdef</c> glyph renders as tofu.
    /// </summary>
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

    private void LayoutLine(
        string line, FontFace primaryFont,
        float spaceAdvanceWidth, float baselineY, VectorPicture result, TextEntry entry)
    {
        int n = line.Length;
        if (n == 0) return;

        var rich = entry as RichTextEntry;

        var charAdvances = new float[n];
        var resolvedFonts = new FontFace?[n];
        var resolvedIndices = new ushort[n];

        // ── First pass: measure every character ──
        for (int i = 0; i < n; i++)
        {
            char c = line[i];

            // Per-character style overrides from RichTextEntry
            float charFontSize = entry.FontSize;
            float charCharSpacing = entry.CharacterSpacing;
            float charWordSpacing = entry.WordSpacing;
            var charVariationAxes = entry.VariationAxes;

            if (rich is not null)
            {
                foreach (var range in rich.GetRangesAt(i))
                {
                    var s = range.Style;
                    if (s.FontSize.HasValue) charFontSize = s.FontSize.Value;
                    if (s.CharacterSpacing.HasValue) charCharSpacing = s.CharacterSpacing.Value;
                    if (s.WordSpacing.HasValue) charWordSpacing = s.WordSpacing.Value;
                    if (s.VariationAxes is not null) charVariationAxes = s.VariationAxes;
                }
            }

            if (c == ' ')
            {
                float spaceScale = charFontSize / entry.FontSize;
                charAdvances[i] = spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                continue;
            }

            var (rFont, rIdx) = ResolveChar(c, primaryFont);
            resolvedFonts[i] = rFont;
            resolvedIndices[i] = rIdx;

            if (rFont.IsVariableFont && charVariationAxes.Count > 0)
                rFont.SetVariationAxes(charVariationAxes);

            float charScale = charFontSize / rFont.UnitsPerEm;
            float advance = rFont.GetVariedAdvanceWidth(rIdx) * charScale + charCharSpacing;
            // 兜底：防止字体返回 0（或可变字体的 variation delta 把 advance 算成 0），
            // 此时光标原地不动会导致所有字形叠在同一点。用 charFontSize 兜一个最小前进量。
            if (advance < charFontSize * 0.1f)
                advance = charFontSize;
            charAdvances[i] = advance;
        }

        // ── Calculate alignment offset ──
        float totalWidth = 0f;
        for (int i = 0; i < n; i++)
            totalWidth += charAdvances[i];

        float xOffset = entry.Alignment switch
        {
            TextAlignment.Center => -totalWidth * 0.5f,
            TextAlignment.Right => -totalWidth,
            _ => 0f,
        };

        // ── Second pass: create elements ──
        float cursorX = xOffset;
        for (int i = 0; i < n; i++)
        {
            if (line[i] == ' ')
            {
                cursorX += charAdvances[i];
                continue;
            }

            FontFace? rFont = resolvedFonts[i];
            ushort rIdx = resolvedIndices[i];
            if (rFont is null)
            {
                cursorX += charAdvances[i];
                continue;
            }

            // Per-character style overrides for rendering (colors, etc.)
            float charFontSize = entry.FontSize;
            ushort fillR = entry.FillR, fillG = entry.FillG, fillB = entry.FillB;
            float fillA = entry.FillA;
            ushort strokeR = entry.StrokeR, strokeG = entry.StrokeG, strokeB = entry.StrokeB;
            float strokeA = entry.StrokeA, strokeThickness = entry.StrokeThickness;
            var charVariationAxes = entry.VariationAxes;

            if (rich is not null)
            {
                foreach (var range in rich.GetRangesAt(i))
                {
                    var s = range.Style;
                    if (s.FontSize.HasValue) charFontSize = s.FontSize.Value;
                    if (s.FillR.HasValue) fillR = s.FillR.Value;
                    if (s.FillG.HasValue) fillG = s.FillG.Value;
                    if (s.FillB.HasValue) fillB = s.FillB.Value;
                    if (s.FillA.HasValue) fillA = s.FillA.Value;
                    if (s.StrokeR.HasValue) strokeR = s.StrokeR.Value;
                    if (s.StrokeG.HasValue) strokeG = s.StrokeG.Value;
                    if (s.StrokeB.HasValue) strokeB = s.StrokeB.Value;
                    if (s.StrokeA.HasValue) strokeA = s.StrokeA.Value;
                    if (s.StrokeThickness.HasValue) strokeThickness = s.StrokeThickness.Value;
                    if (s.VariationAxes is not null) charVariationAxes = s.VariationAxes;
                }
            }

            if (rFont.IsVariableFont && charVariationAxes.Count > 0)
                rFont.SetVariationAxes(charVariationAxes);

            Glyph? glyph = rFont.GetVariedGlyph(rIdx);
            if (glyph is not null && !glyph.IsEmpty)
            {
                var element = new GlyphCanvasElement(glyph, rFont.UnitsPerEm)
                {
                    FontSize = charFontSize,
                    RelativeX = cursorX,
                    RelativeY = baselineY,
                };

                if (fillA > 0f)
                    element.WithFill(fillR, fillG, fillB, fillA);
                if (strokeThickness > 0f && strokeA > 0f)
                    element.WithStroke(strokeR, strokeG, strokeB, strokeA, strokeThickness);

                result.Elements.Add(element);
            }

            cursorX += charAdvances[i];
        }
    }
}
