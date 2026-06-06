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
/// <remarks>
/// <para><b>Unit conventions</b> — all coordinates in <see cref="VectorPicture"/>
/// (and in <see cref="TextEntry.X"/>, <see cref="TextEntry.Y"/>, <see cref="TextEntry.FontSize"/>)
/// are normalised to the canvas's <c>0..1</c> space, where <c>1.0</c> spans the full
/// canvas width/height. <see cref="TextEntry.FontSize"/> is therefore a fraction of
/// the canvas height (a <c>FontSize</c> of <c>0.1</c> means the font's em-square
/// occupies 10% of the canvas height).</para>
/// <para><b>Advance computation</b> — the per-character advance is computed in
/// <see cref="ComputeCharacterAdvance"/> using the same formula in
/// <see cref="LayoutLine"/> and <see cref="Measure"/>, so measured widths and
/// rendered cursor positions stay in sync.</para>
/// </remarks>
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

                    lineWidth += ComputeCharacterAdvance(rFont, rIdx, charFontSize, charCharSpacing);
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

    /// <summary>
    /// Compute the horizontal advance (in normalised canvas space, <c>0..1</c>) for
    /// a single glyph.  This is the single source of truth for unit conversion
    /// between the font's design space (hmtx advances in font units,
    /// <see cref="FontFace.UnitsPerEm"/>) and the canvas's normalised 0..1 space.
    /// Both <see cref="LayoutLine"/> and <see cref="Measure"/> call this so the
    /// measured widths and the rendered cursor positions stay in sync.
    /// </summary>
    /// <param name="font">Font used to look up the glyph advance. May be a
    /// fallback font returned by <see cref="ResolveChar"/>.</param>
    /// <param name="glyphIndex">Index of the glyph in <paramref name="font"/>.</param>
    /// <param name="charFontSize">Effective font size for this character,
    /// expressed as a fraction of the canvas height (0..1).</param>
    /// <param name="charCharSpacing">Extra spacing to add per character
    /// (already in normalised canvas space).</param>
    /// <remarks>
    /// <para>The conversion chain is:</para>
    /// <code>
    ///   rawAdvance  = advance(font_units) * (charFontSize / UnitsPerEm) + charCharSpacing
    /// </code>
    /// <para>Only a <b>lower bound</b> is applied: if the font reports a zero or
    /// near-zero advance (&lt; 10% of <paramref name="charFontSize"/>), we fall
    /// back to <c>charFontSize * 0.5f</c> — half the em height, which is a
    /// reasonable default advance for a typical proportional character. Using
    /// the full <c>charFontSize</c> (the entire em height) as a fallback was
    /// too generous: when <c>charFontSize ≈ 1.0</c> (large font or small
    /// canvas), each character would advance by the full canvas width,
    /// pushing all subsequent characters off-canvas and making text appear
    /// to not render completely.</para>
    /// <para><b>No upper clamp</b> is applied here on purpose. The earlier
    /// <c>maxAdvance = charFontSize * 1.5</c> allowed the per-character advance
    /// to exceed the canvas width (because it scaled with the font size), and
    /// a hard clamp to <c>1.0</c> was even worse: it forced every glyph to fit
    /// in one canvas width, so a 2-character string rendered as a single column
    /// of stacked glyphs.</para>
    /// <para>Callers should still cap the normalised font size at <c>1.0</c>
    /// before passing it to this engine, so the per-glyph advance is naturally
    /// bounded and never runs off the canvas in normal operation.</para>
    /// </remarks>
    internal static float ComputeCharacterAdvance(
        FontFace font, ushort glyphIndex, float charFontSize, float charCharSpacing)
    {
        if (font is null)
            return 0f;
        if (charFontSize <= 0f || !float.IsFinite(charFontSize))
            return charCharSpacing;

        ushort upm = font.UnitsPerEm;
        if (upm == 0)
            return charCharSpacing;

        // Convert from font design units to normalised canvas space.
        // 1 em  ≡  charFontSize  (fraction of canvas height)
        // 1 unit ≡  charFontSize / upm
        float charScale = charFontSize / upm;
        float advance = font.GetVariedAdvanceWidth(glyphIndex) * charScale + charCharSpacing;

        // Fallback: when the font reports a zero or near-zero advance (e.g.
        // broken hmtx table, variation delta that nets out to 0, or .notdef
        // glyph with no advance), use half the em height as a sane default.
        // Half-em (~0.5×charFontSize) matches the advance-to-EM ratio of a
        // typical proportional character and keeps the cursor moving without
        // blowing each glyph to the full canvas width.
        if (advance < charFontSize * 0.1f)
            advance = charFontSize * 0.5f;

        return advance;
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

            charAdvances[i] = ComputeCharacterAdvance(rFont, rIdx, charFontSize, charCharSpacing);
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
