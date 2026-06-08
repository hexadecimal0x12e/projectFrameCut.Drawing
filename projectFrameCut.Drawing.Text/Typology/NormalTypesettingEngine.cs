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

    /// <summary>
    /// When <c>true</c>, each rendered glyph also gets visual debug overlays:
    /// a cyan box showing the character's full measured advance,
    /// a green box for the character spacing portion,
    /// and an orange box for the word spacing portion (on spaces).
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// When <c>true</c>, <see cref="DumpCharAdvance"/> writes one line per
    /// character to the debug/console output during <see cref="Layout"/> and
    /// <see cref="Measure"/>. Used to diagnose unexpected advance widths.
    /// Default is <c>true</c>; flip to <c>false</c> to silence.
    /// </summary>
    public static bool DebugDumpAdvance { get; set; } = true;

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
            LayoutLine(line, font, spaceGlyphIndex, spaceAdvanceWidth, baselineY, result, entry);
            baselineY += lineHeight;
        }

        foreach (var element in result.Elements)
        {
            // Store the text-block origin separately so the renderer can map it
            // with canvas width/height while per-glyph RelativeX/Y (cursor advances
            // in height-normalised space) are mapped with uniform min(w,h) scale.
            element.BaseX = entry.X;
            element.BaseY = entry.Y;
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

        DumpTextCodepoints("Measure", entry.Text);

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

                float charAdvance;
                if (c == ' ')
                {
                    float spaceScale = charFontSize / entry.FontSize;
                    charAdvance = spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                    DumpCharAdvance(i, "Measure", c, font, spaceGlyphIndex,
                        charFontSize, charCharSpacing, charAdvance);
                }
                else
                {
                    var (rFont, rIdx) = ResolveChar(c, font);

                    if (rFont.IsVariableFont && charVariationAxes.Count > 0)
                        rFont.SetVariationAxes(charVariationAxes);

                    charAdvance = ComputeCharacterAdvance(rFont, rIdx, charFontSize, charCharSpacing);
                    DumpCharAdvance(i, "Measure", c, rFont, rIdx,
                        charFontSize, charCharSpacing, charAdvance);
                }
                lineWidth += charAdvance;
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
        string line, FontFace primaryFont, ushort spaceGlyphIndex,
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
                DumpCharAdvance(i, "Layout", c, primaryFont, spaceGlyphIndex,
                    charFontSize, charCharSpacing, charAdvances[i]);
                continue;
            }

            var (rFont, rIdx) = ResolveChar(c, primaryFont);
            resolvedFonts[i] = rFont;
            resolvedIndices[i] = rIdx;

            if (rFont.IsVariableFont && charVariationAxes.Count > 0)
                rFont.SetVariationAxes(charVariationAxes);

            charAdvances[i] = ComputeCharacterAdvance(rFont, rIdx, charFontSize, charCharSpacing);
            DumpCharAdvance(i, "Layout", c, rFont, rIdx,
                charFontSize, charCharSpacing, charAdvances[i]);
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
            char c = line[i];

            // Per-character style overrides for rendering (colors, spacing, etc.)
            float charFontSize = entry.FontSize;
            float charCharSpacing = entry.CharacterSpacing;
            float charWordSpacing = entry.WordSpacing;
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
                    if (s.CharacterSpacing.HasValue) charCharSpacing = s.CharacterSpacing.Value;
                    if (s.WordSpacing.HasValue) charWordSpacing = s.WordSpacing.Value;
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

            if (c == ' ')
            {
                if (DebugMode)
                    AddDebugBoxes(result, cursorX, baselineY, charAdvances[i],
                        charFontSize, charCharSpacing, charWordSpacing, c, entry,
                        primaryFont, i == 0);
                cursorX += charAdvances[i];
                continue;
            }

            FontFace? rFont = resolvedFonts[i];
            ushort rIdx = resolvedIndices[i];
            if (rFont is null)
            {
                if (DebugMode)
                    AddDebugBoxes(result, cursorX, baselineY, charAdvances[i],
                        charFontSize, charCharSpacing, charWordSpacing, c, entry,
                        primaryFont, i == 0);
                cursorX += charAdvances[i];
                continue;
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
                
                element.WithShowEmBox(DebugMode);
                result.Elements.Add(element);
            }

            if (DebugMode)
            {
                AddDebugBoxes(result, cursorX, baselineY, charAdvances[i],
                    charFontSize, charCharSpacing, charWordSpacing, c, entry,
                    primaryFont, i == 0);
            }

            cursorX += charAdvances[i];
        }
    }

    /// <summary>
    /// Render a short debug label string as individual <see cref="GlyphCanvasElement"/>s
    /// at (x, y) with black fill. Used by <see cref="AddDebugBoxes"/> to annotate
    /// advance widths directly on the debug visuals.
    /// </summary>
    private static void AddDebugLabel(
        VectorPicture result, float x, float y, string text,
        FontFace font, float fontSize, int layerIndex, float rotation)
    {
        float cursor = 0f;
        foreach (char ch in text)
        {
            ushort glyphIdx = font.GetGlyphIndex(ch);
            if (glyphIdx == 0)
            {
                cursor += fontSize * 0.35f;
                continue;
            }

            Glyph? glyph = font.GetVariedGlyph(glyphIdx);
            if (glyph is null || glyph.IsEmpty)
            {
                cursor += fontSize * 0.35f;
                continue;
            }

            var element = new GlyphCanvasElement(glyph, font.UnitsPerEm)
            {
                FontSize = fontSize,
                RelativeX = x + cursor,
                RelativeY = y,
                LayerIndex = layerIndex,
                Rotation = rotation,
            }.WithFill(0, 0, 0, 1f);
            result.Elements.Add(element);

            float adv = font.GetAdvanceWidth(glyphIdx) * (fontSize / font.UnitsPerEm);
            cursor += adv < fontSize * 0.1f ? fontSize * 0.35f : adv;
        }
    }

    private static void AddDebugBoxes(
        VectorPicture result, float cursorX, float baselineY, float advance,
        float charFontSize, float charCharSpacing, float charWordSpacing,
        char c, TextEntry entry, FontFace primaryFont, bool showHeightLabel)
    {
        float emTop = baselineY - charFontSize;
        int debugLayer = entry.LayerIndex < int.MaxValue ? entry.LayerIndex + 1 : int.MaxValue;

        // Full measured advance box (cyan outline)
        // UseUniformScale keeps the debug boxes in the same coordinate space as glyphs.
        var advBox = ShapeCanvasElement.DrawRectangle(advance, charFontSize);
        advBox.UseUniformScale = true;
        advBox.RelativeX = cursorX;
        advBox.RelativeY = emTop;
        advBox.LayerIndex = debugLayer;
        advBox.Rotation = entry.Rotation;
        result.Elements.Add(advBox.WithStroke((ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), 1f, 1f));

        // Character spacing box (green outline) — trailing edge of the advance
        if (MathF.Abs(charCharSpacing) > 0.0001f && advance > charCharSpacing + 0.0001f)
        {
            var csBox = ShapeCanvasElement.DrawRectangle(charCharSpacing, charFontSize);
            csBox.UseUniformScale = true;
            csBox.RelativeX = cursorX + advance - charCharSpacing;
            csBox.RelativeY = emTop;
            csBox.LayerIndex = debugLayer;
            csBox.Rotation = entry.Rotation;
            result.Elements.Add(csBox.WithFill((ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), 1f));
        }

        // Word spacing box (orange outline) — before char spacing, for spaces
        if (c == ' ' && MathF.Abs(charWordSpacing) > 0.0001f &&
            advance > charCharSpacing + charWordSpacing + 0.0001f)
        {
            var wsBox = ShapeCanvasElement.DrawRectangle(charWordSpacing, charFontSize);
            wsBox.UseUniformScale = true;
            wsBox.RelativeX = cursorX + advance - charCharSpacing - charWordSpacing;
            wsBox.RelativeY = emTop;
            wsBox.LayerIndex = debugLayer;
            wsBox.Rotation = entry.Rotation;
            result.Elements.Add(wsBox.WithFill((ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), (ushort)Random.Shared.Next(0,65535), 0.5f));
        }

        // Text label: show the character’s advance width (canvas units) on every box,
        // and the character height on the first character in each line.
        float labelFontSize = MathF.Max(charFontSize * 0.15f, 0.012f);
        AddDebugLabel(result, cursorX, emTop, $"w={advance:F4}",
            primaryFont, labelFontSize, debugLayer, entry.Rotation);

        if (showHeightLabel)
        {
            AddDebugLabel(result, cursorX, emTop + labelFontSize * 1.1f, $"h={charFontSize:F4}",
                primaryFont, labelFontSize, debugLayer, entry.Rotation);
        }
    }

    /// <summary>
    /// Diagnostic dump: writes one line per character with everything that
    /// went into the advance calculation, so unexpected widths can be
    /// traced back to the font (hmtx) or to the engine logic.
    /// </summary>
    /// <remarks>
    /// Toggled by <see cref="DebugDumpAdvance"/>. Safe to leave in place —
    /// the <see cref="Debug.WriteLine(string)"/> call is a no-op when no
    /// debugger / <c>Console</c> listener is attached.
    /// </remarks>
    private static void DumpCharAdvance(
        int index, string context, char c, FontFace? font,
        ushort glyphIndex, float charFontSize, float charCharSpacing,
        float advance)
    {
        if (!DebugDumpAdvance) return;
        if (font is null) return;

        try
        {
            ushort upm = font.UnitsPerEm;
            ushort hmtxAdv = font.GetAdvanceWidth(glyphIndex);
            ushort variedAdv = font.GetVariedAdvanceWidth(glyphIndex);
            string line =
                $"[NTE/{context}] idx={index} char='{c}' U+{(int)c:X4} " +
                $"glyph={glyphIndex} " +
                $"hmtx={hmtxAdv}/{upm}={hmtxAdv / (float)upm:F3}em " +
                $"varied={variedAdv}/{upm}={variedAdv / (float)upm:F3}em " +
                $"charFS={charFontSize:F4} " +
                $"charSpacing={charCharSpacing:F4} " +
                $"advance={advance:F4}";
            System.Diagnostics.Debug.WriteLine(line);
            Console.WriteLine(line);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NTE/{context}] idx={index} char='{c}' U+{(int)c:X4} " +
                $"ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// Print a one-line summary of the input text broken down into
    /// codepoints (so hidden characters like U+2003 / U+3000 / zero-width
    /// spaces can be spotted). Emitted at the start of every
    /// <see cref="Layout"/> and <see cref="Measure"/> call.
    /// </summary>
    private static void DumpTextCodepoints(string context, string text)
    {
        if (!DebugDumpAdvance || text is null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append($"[NTE/{context}/text] len={text.Length} codepoints=[");
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                ? char.ConvertToUtf32(text[i], text[i + 1])
                : text[i];
            sb.Append($"U+{cp:X4}");
        }
        sb.Append("] rawBytes=");
        sb.Append(BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(text)));
        string line = sb.ToString();
        System.Diagnostics.Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
