using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using System.Diagnostics;
using System.Text;

namespace projectFrameCut.Drawing.Text.Typology
{
    public static class LineBreakHandler
    {
        // ──────────────────────────────────────────────
        //  Font-unit based
        // ──────────────────────────────────────────────

        /// <summary>
        /// Break <paramref name="input"/> into lines that fit within
        /// <paramref name="targetWidth"/> when rendered with <paramref name="targetFont"/>.
        /// </summary>
        /// <param name="targetWidth">Available width in font design units (em units),
        /// i.e. the same unit space as <see cref="FontFace.GetVariedAdvanceWidth"/>.</param>
        /// <param name="NewLine">Line separator (defaults to <see cref="System.Environment.NewLine"/>).</param>
        public static string BreakLine(string input, FontFace targetFont, float targetWidth, string? NewLine = null)
        {
            NewLine ??= System.Environment.NewLine;

            if (string.IsNullOrEmpty(input) || targetWidth <= 0)
                return input;

            var result = new StringBuilder();
            var paragraphs = input.Split('\n');

            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    result.Append(NewLine);

                string para = paragraphs[p];
                int n = para.Length;
                var widths = new float[n];
                for (int i = 0; i < n; i++)
                    widths[i] = targetFont.GetVariedAdvanceWidth(targetFont.GetGlyphIndex(para[i]));

                BreakParagraphCore(para, widths, targetWidth, result, NewLine);
            }

            return result.ToString();
        }

        // ──────────────────────────────────────────────
        //  Canvas-space based (TextEntry / RichTextEntry)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Break the text from <paramref name="entry"/> into lines that fit within
        /// <paramref name="targetWidth"/> in canvas (normalized) space.
        /// Supports <see cref="RichTextEntry"/> per-character font-size and
        /// spacing overrides.
        /// </summary>
        public static string BreakLine(TextEntry entry, FontFace font, float targetWidth, string? NewLine = null)
        {
            NewLine ??= System.Environment.NewLine;

            if (string.IsNullOrEmpty(entry.Text) || targetWidth <= 0)
                return entry.Text;

            var result = new StringBuilder();
            var paragraphs = entry.Text.Split('\n');
            var rich = entry as RichTextEntry;

            // Base space advance in canvas space.
            ushort spaceGlyphIndex = font.GetGlyphIndex(' ');
            float spaceAdvanceWidth = font.GetVariedAdvanceWidth(spaceGlyphIndex) *
                                      (entry.FontSize / font.UnitsPerEm);

            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    result.Append(NewLine);

                string para = paragraphs[p];
                int n = para.Length;
                var widths = new float[n];
                for (int i = 0; i < n; i++)
                {
                    char c = para[i];

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
                        widths[i] = spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                    }
                    else
                    {
                        if (font.IsVariableFont && charVariationAxes.Count > 0)
                            font.SetVariationAxes(charVariationAxes);

                        // Reuse the same advance formula as the rendering engine
                        // (NormalTypesettingEngine.ComputeCharacterAdvance) so line
                        // breaking decisions match the cursor positions used at render
                        // time — otherwise a wrap point chosen here can disagree with
                        // the rendered layout.
                        ushort glyphIndex = font.GetGlyphIndex(c);
                        widths[i] = NormalTypesettingEngine.ComputeCharacterAdvance(
                            font, glyphIndex, charFontSize, charCharSpacing);
                    }
                }

                BreakParagraphCore(para, widths, targetWidth, result, NewLine);
            }

            return result.ToString();
        }

        // ──────────────────────────────────────────────
        //  Core greedy line-breaking algorithm
        // ──────────────────────────────────────────────

        private static void BreakParagraphCore(
            string paragraph, float[] widths, float targetWidth,
            StringBuilder result, string newLine)
        {
            int n = paragraph.Length;

            int lineStart = 0;
            int lastBreakIdx = -1;   // last space position in the current line
            float currentWidth = 0;
            bool needNewLine = false;

            for (int i = 0; i < n; i++)
            {
                float charWidth = widths[i];

                if (currentWidth + charWidth > targetWidth)
                {
                    if (lastBreakIdx >= lineStart)
                    {
                        // Backtrack to the last space — break there.
                        if (needNewLine) result.Append(newLine);
                        result.Append(paragraph, lineStart, lastBreakIdx - lineStart);
                        needNewLine = true;

                        lineStart = lastBreakIdx + 1;
                        currentWidth = 0;
                        lastBreakIdx = -1;
                        for (int j = lineStart; j <= i; j++)
                        {
                            currentWidth += widths[j];
                            if (paragraph[j] == ' ')
                                lastBreakIdx = j;
                        }
                    }
                    else if (i > lineStart)
                    {
                        // No space to backtrack — break at the current character.
                        if (needNewLine) result.Append(newLine);
                        result.Append(paragraph, lineStart, i - lineStart);
                        needNewLine = true;

                        lineStart = i;
                        currentWidth = charWidth;
                        lastBreakIdx = paragraph[i] == ' ' ? i : -1;
                    }
                    else
                    {
                        // Single character wider than targetWidth — emit it anyway.
                        if (needNewLine) result.Append(newLine);
                        result.Append(paragraph[i]);
                        needNewLine = true;

                        lineStart = i + 1;
                        currentWidth = 0;
                        lastBreakIdx = -1;
                    }
                }
                else
                {
                    currentWidth += charWidth;
                    if (paragraph[i] == ' ')
                        lastBreakIdx = i;
                }
            }

            // Flush remaining segment.
            if (lineStart < n)
            {
                if (needNewLine) result.Append(newLine);
                result.Append(paragraph, lineStart, n - lineStart);
            }
        }
    }
}
