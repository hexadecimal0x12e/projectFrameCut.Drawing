using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Text.Typology;

/// <summary>
/// Lays out a string of text using a <see cref="FontFace"/> and produces a
/// <see cref="VectorPicture"/> where each visible glyph is a positioned
/// <see cref="GlyphCanvasElement"/>.
/// </summary>
public class NormalTypesettingEngine : ITypesettingEngine
{
    // ──────────────────────────────────────────────
    //  Font & size
    // ──────────────────────────────────────────────

    /// <summary>Desired glyph height in normalized canvas coordinates (0–1).</summary>
    public float FontSize { get; set; } = 0.1f;

    // ──────────────────────────────────────────────
    //  Fill
    // ──────────────────────────────────────────────

    public ushort FillR { get; set; }
    public ushort FillG { get; set; }
    public ushort FillB { get; set; }
    public float FillA { get; set; } = 1f;

    // ──────────────────────────────────────────────
    //  Stroke (outline)
    // ──────────────────────────────────────────────

    public ushort StrokeR { get; set; }
    public ushort StrokeG { get; set; }
    public ushort StrokeB { get; set; }
    public float StrokeA { get; set; }
    public float StrokeThickness { get; set; }

    // ──────────────────────────────────────────────
    //  Spacing  (all values are in normalized 0-1 canvas coordinates)
    // ──────────────────────────────────────────────

    /// <summary>Extra horizontal space added after every visible character.</summary>
    public float CharacterSpacing { get; set; }

    /// <summary>Extra horizontal space added after each space character (on top of the space glyph's own advance width).</summary>
    public float WordSpacing { get; set; }

    /// <summary>Extra vertical space added on top of <c>FontSize</c> for each line. Line height = FontSize x (1 + LineSpacing).</summary>
    public float LineSpacing { get; set; } = 0.3f;

    // ──────────────────────────────────────────────
    //  Alignment
    // ──────────────────────────────────────────────


    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

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
    //  Variable font variation axes
    // ──────────────────────────────────────────────

    /// <summary>
    /// Variation axis coordinates for variable font support.
    /// Key = axis tag (e.g., "wght"), Value = desired coordinate (e.g., 700).
    /// </summary>
    public Dictionary<string, float> VariationAxes { get; set; } = new Dictionary<string, float>();

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Lay out <paramref name="text"/> using the specified <paramref name="font"/>
    /// (and any <see cref="FallbackFonts"/>).
    /// Line-breaks (LF / \n) start a new line. The result is a <see cref="VectorPicture"/>
    /// with one <see cref="GlyphCanvasElement"/> per visible glyph.
    /// </summary>
    /// <remarks>
    /// The text baseline starts at (0, 0), i.e. the top-left of the canvas.
    /// Use <see cref="VectorCanvasElement.RelativeX"/> and <see cref="VectorCanvasElement.RelativeY"/>
    /// on each element (or overlay the whole picture via <c>VectorPicture.Overlay()</c>
    /// with an offset picture) to position the result.
    /// </remarks>
    public VectorPicture Layout(string text, FontFace font)
    {
        var result = new VectorPicture();
        if (string.IsNullOrEmpty(text))
            return result;

        float lineHeight = FontSize * (1f + LineSpacing);

        // Pre-cache the space glyph's advance width from the primary font
        ushort spaceGlyphIndex = font.GetGlyphIndex(' ');
        float spaceAdvanceWidth = font.GetAdvanceWidth(spaceGlyphIndex) *
                                  (FontSize / font.UnitsPerEm);

        var lines = text.Split('\n');
        float baselineY = 0;

        foreach (var line in lines)
        {
            LayoutLine(line, font, spaceAdvanceWidth, baselineY, result);
            baselineY += lineHeight;
        }

        return result;
    }

    // ──────────────────────────────────────────────
    //  Measurement
    // ──────────────────────────────────────────────

    /// <inheritdoc/>
    public (float width, float height) Measure(string text, FontFace font)
    {
        if (string.IsNullOrEmpty(text))
            return (0f, 0f);

        float lineHeight = FontSize * (1f + LineSpacing);

        ushort spaceGlyphIndex = font.GetGlyphIndex(' ');
        float spaceAdvanceWidth = font.GetAdvanceWidth(spaceGlyphIndex) *
                                  (FontSize / font.UnitsPerEm);
        float spaceTotalAdvance = spaceAdvanceWidth + WordSpacing + CharacterSpacing;

        var lines = text.Split('\n');
        float maxWidth = 0f;

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            float lineWidth = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == ' ')
                {
                    lineWidth += spaceTotalAdvance;
                }
                else
                {
                    var (rFont, rIdx) = ResolveChar(c, font);

                    // Apply variable font variation axes if supported
                    if (rFont.IsVariableFont && VariationAxes.Count > 0)
                        rFont.SetVariationAxes(VariationAxes);

                    float charScale = FontSize / rFont.UnitsPerEm;
                    lineWidth += rFont.GetVariedAdvanceWidth(rIdx) * charScale + CharacterSpacing;
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

        // Ultimate fallback: render the primary font's .notdef
        return (primaryFont, primaryFont.GetGlyphIndex(c));
    }

    private void LayoutLine(
        string line, FontFace primaryFont,
        float spaceAdvanceWidth, float baselineY, VectorPicture result)
    {
        int n = line.Length;
        if (n == 0) return;

        // ── First pass: measure every character (resolve font per-char) ──
        var charAdvances = new float[n];
        var resolvedFonts = new FontFace?[n];
        var resolvedIndices = new ushort[n];

        for (int i = 0; i < n; i++)
        {
            char c = line[i];

            if (c == ' ')
            {
                charAdvances[i] = spaceAdvanceWidth + WordSpacing + CharacterSpacing;
                continue;
            }

            var (rFont, rIdx) = ResolveChar(c, primaryFont);
            resolvedFonts[i] = rFont;
            resolvedIndices[i] = rIdx;

            // Apply variable font variation axes if supported
            if (rFont.IsVariableFont && VariationAxes.Count > 0)
                rFont.SetVariationAxes(VariationAxes);

            float charScale = FontSize / rFont.UnitsPerEm;
            charAdvances[i] = rFont.GetVariedAdvanceWidth(rIdx) * charScale + CharacterSpacing;
        }

        // ── Calculate alignment offset ──
        float totalWidth = 0f;
        for (int i = 0; i < n; i++)
            totalWidth += charAdvances[i];

        float xOffset = Alignment switch
        {
            TextAlignment.Center => -totalWidth * 0.5f,
            TextAlignment.Right => -totalWidth,
            _ => 0f,
        };

        // ── Second pass: create elements ──
        float cursorX = xOffset;
        for (int i = 0; i < n; i++)
        {
            char c = line[i];

            if (c != ' ')
            {
                FontFace? rFont = resolvedFonts[i];
                ushort rIdx = resolvedIndices[i];

                if (rFont is not null)
                {
                    // Apply variable font variation axes if supported
                    if (rFont.IsVariableFont && VariationAxes.Count > 0)
                        rFont.SetVariationAxes(VariationAxes);

                    Glyph? glyph = rFont.GetVariedGlyph(rIdx);
                    if (glyph is not null && !glyph.IsEmpty)
                    {
                        var element = new GlyphCanvasElement(glyph, rFont.UnitsPerEm)
                        {
                            FontSize = FontSize,
                            RelativeX = cursorX,
                            RelativeY = baselineY,
                        };

                        if (FillA > 0f)
                            element.WithFill(FillR, FillG, FillB, FillA);
                        if (StrokeThickness > 0f && StrokeA > 0f)
                            element.WithStroke(StrokeR, StrokeG, StrokeB, StrokeA, StrokeThickness);

                        result.Elements.Add(element);
                    }
                }
            }

            cursorX += charAdvances[i];
        }
    }

    public static ITypesettingEngine FromEntry(TextEntry entry)
    {
        return new NormalTypesettingEngine
        {
            FontSize = entry.FontSize,
            FillR = entry.FillR,
            FillG = entry.FillG,
            FillB = entry.FillB,
            FillA = entry.FillA,
            StrokeR = entry.StrokeR,
            StrokeG = entry.StrokeG,
            StrokeB = entry.StrokeB,
            StrokeA = entry.StrokeA,
            StrokeThickness = entry.StrokeThickness,
            CharacterSpacing = entry.CharacterSpacing,
            WordSpacing = entry.WordSpacing,
            LineSpacing = entry.LineSpacing,
            Alignment = entry.Alignment,
            VariationAxes = new Dictionary<string, float>(entry.VariationAxes)
        };
    }
}
