using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using System.Diagnostics;
using System.Text;
using System.Globalization;

namespace projectFrameCut.Drawing.Text.Typology
{
    public static class LineBreakHandler
    {
        public static string BreakLatinText(TextEntry entry, FontFace font, float maxWidth, string NewLine = "\n", bool useDashWhenWordAcrossLine = false)
        {
            List<string> result = new();
            var rich = entry as RichTextEntry;
            string currentLine = "", currentWord = "";
            float currentLineWidth = 0f;
            float spaceAdvanceWidth = font.GetVariedAdvanceWidth(font.GetGlyphIndex(' ')) * (entry.FontSize / font.UnitsPerEm);
            var targetWidth = maxWidth;
            var dashWidth = useDashWhenWordAcrossLine ? (entry.CharacterSpacing + (font.GetVariedAdvanceWidth(font.GetGlyphIndex('-')) * (entry.FontSize / font.UnitsPerEm))) : 0;
            for (int i = 0; i < entry.Text.Length; i++)
            {
                char item = entry.Text[i];
                currentWord += item;
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

                float width = 0f;

                if (item == ' ' && string.IsNullOrWhiteSpace(currentLine))
                {
                    currentWord = "";
                    continue;
                }
                else if (item == ' ')
                {
                    // Mirror the rendering: scale the pre-measured space advance
                    // by the per-character FontSize (rich text may override the
                    // size of a space inside a range) and include
                    // CharacterSpacing in the width.
                    float spaceScale = charFontSize / entry.FontSize;
                    width = spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                    currentWord = "";
                }
                else if (item == '\n')
                {
                    width = 0f;
                    result.Add(currentLine);
                    currentLine = "";
                    currentWord = "";
                    continue;
                }
                else
                {
                    // Must mirror NormalTypesettingEngine.LayoutLine: apply the
                    // current variation axes BEFORE reading the advance,
                    // otherwise GetVariedAdvanceWidth silently falls back to
                    // the unvaried hmtx value and the line breaker's width
                    // estimate disagrees with the rendered cursor position.
                    if (font.IsVariableFont && charVariationAxes.Count > 0)
                        font.SetVariationAxes(charVariationAxes);

                    width = charCharSpacing + (font.GetVariedAdvanceWidth(font.GetGlyphIndex(item)) * (charFontSize / font.UnitsPerEm));
                    if (width < charFontSize * 0.1f)
                        width = charFontSize * 0.5f;
                }

                if (width + currentLineWidth <= targetWidth - dashWidth || (width + currentLineWidth > (targetWidth - dashWidth) && char.IsPunctuation(item)))
                {
                    currentLine += item;
                    currentLineWidth += width;
                }
                else
                {
                    Debug.WriteLine($"Line break: current width {width + currentLineWidth} of {targetWidth}, content '{currentLine}', word '{currentWord}', char '{item}'");
                    if (item != ' ' && useDashWhenWordAcrossLine)
                    {
                        // Apply variation axes before reading the dash's advance
                        // so the dash width estimate matches the rendered cursor
                        // (mirrors the measurement done for the current
                        // character above).
                        if (font.IsVariableFont && charVariationAxes.Count > 0)
                            font.SetVariationAxes(charVariationAxes);

                        // Append a hyphen to the END of the current line (i.e.
                        // after the last char that DID fit) and start a new line
                        // with the overflowing char. The original code appended
                        // `{item}-` to the current line, which pushed the dash
                        // past the right margin; a stricter check that added
                        // `currentLineWidth + width` made this branch
                        // unreachable, since the outer else already implies
                        // `width + currentLineWidth + dashWidth > targetWidth`.
                        // The correct fit test is therefore just whether the
                        // current line has room left for the dash itself —
                        // `item` will start a fresh line.
                        if (currentLineWidth + (charCharSpacing + (font.GetVariedAdvanceWidth(font.GetGlyphIndex('-')) * (charFontSize / font.UnitsPerEm))) <= targetWidth && i + 1 <= entry.Text.Length - 1 && entry.Text[i + 1] != ' ')
                        {
                            currentLine += "-";
                            result.Add(currentLine);
                            currentLine = item.ToString();
                            currentLineWidth = width;
                            currentWord = item.ToString();
                            continue;
                        }
                        else
                        {
                            if (currentLine.Length < currentWord.Length && !entry.Text.Contains(' ')) //only 1 word in a line
                            {
                                throw new InvalidOperationException("Please use BreakCJKText() with allowPunctuationOverflowMaxWidth = false instead for Latin text which only have 1 word.");
                            }
                            currentLine = currentLine.Substring(0, currentLine.Length - currentWord.Length);
                            result.Add(currentLine);
                            currentLine = "";
                            i -= currentWord.Length;
                            currentWord = "";
                            currentLineWidth = 0;
                        }
                    }
                    else
                    {
                        if (currentLine.Length < currentWord.Length && !entry.Text.Contains(' ')) //only 1 word in a line
                        {
                            // I don't want to write 2 logic to handle this case hah
                            throw new InvalidOperationException("Please use BreakCJKText() with allowPunctuationOverflowMaxWidth = false instead for Latin text which only have 1 word.");
                        }
                        else
                        {
                            currentLine = currentLine.Substring(0, currentLine.Length - currentWord.Length);
                            result.Add(currentLine);
                            currentLine = "";
                            i -= currentWord.Length;
                            currentWord = "";
                            currentLineWidth = 0;
                        }


                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentLine)) result.Add(currentLine);
            return string.Join(NewLine, result);
        }

        public static string BreakCJKText(TextEntry entry, FontFace font, float maxWidth, string NewLine = "\n", bool allowPunctuationOverflowMaxWidth = false)
        {
            List<string> result = new();
            var rich = entry as RichTextEntry;
            string currentLine = "";
            float currentLineWidth = 0f;
            float spaceAdvanceWidth = font.GetVariedAdvanceWidth(font.GetGlyphIndex(' ')) * (entry.FontSize / font.UnitsPerEm);

            for (int i = 0; i < entry.Text.Length; i++)
            {
                char item = entry.Text[i];
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

                float advanceWidth;

                if (item == ' ' && string.IsNullOrWhiteSpace(currentLine)) continue;
                else if (item == ' ')
                {
                    float spaceScale = charFontSize / entry.FontSize;
                    advanceWidth = spaceAdvanceWidth * spaceScale + charWordSpacing + charCharSpacing;
                }
                else if (item == '\n')
                {
                    result.Add(currentLine);
                    currentLine = "";
                    currentLineWidth = 0f;
                    continue;
                }
                else
                {
                    if (font.IsVariableFont && charVariationAxes.Count > 0)
                        font.SetVariationAxes(charVariationAxes);

                    ushort glyphIndex = font.GetGlyphIndex(item);
                    advanceWidth = charCharSpacing + (font.GetVariedAdvanceWidth(glyphIndex) * (charFontSize / font.UnitsPerEm));
                    if (advanceWidth < charFontSize * 0.1f)
                        advanceWidth = charFontSize * 0.5f;
                }

                if (currentLineWidth + advanceWidth > maxWidth)
                {
                    // CJK allows punctuation to slightly overflow the margin
                    if (char.IsPunctuation(item) && allowPunctuationOverflowMaxWidth)
                    {
                        currentLine += item;
                        result.Add(currentLine);
                        currentLine = "";
                        currentLineWidth = 0f;
                        continue;
                    }

                    if (currentLine.Length > 0)
                    {
                        result.Add(currentLine);
                        currentLine = "";
                        currentLineWidth = 0f;
                        i--;
                        continue;
                    }
                }

                currentLine += item;
                currentLineWidth += advanceWidth;
            }

            if (!string.IsNullOrWhiteSpace(currentLine)) result.Add(currentLine);
            return string.Join(NewLine, result);
        }

        /// <summary>
        /// Break the text from <paramref name="entry"/> into lines that fit within
        /// <paramref name="targetWidth"/> in canvas (normalized) space.
        /// Supports <see cref="RichTextEntry"/> per-character font-size and
        /// spacing overrides.
        /// </summary>
        public static string BreakLine(TextEntry entry, FontFace font, float targetWidth, string NewLine = "\n", bool useDashWhenWordAcrossLineInLatin = false, bool allowPunctuationOverflowMaxWidthInCJK = false)
        {
            if (string.IsNullOrEmpty(entry.Text) || targetWidth <= 0)
                return entry.Text;

            // Preserve extended grapheme clusters (surrogate Emoji, modifiers and ZWJ sequences).
            if (entry.Text.Any(char.IsSurrogate) || entry.Text.Contains('\u200D') || entry.Text.Contains('\uFE0F'))
                return BreakGraphemeClusters(entry, font, targetWidth, NewLine);

            // Chinese or Japanese need special way to process as they don't have space between words
            if (entry.Text.Any(c => (c >= '一' && c <= 0x9FFF) || (c >= 'ぁ' && c <= 'ゟ') || (c >= '゠' && c <= 'ヿ')))
            {
                return BreakCJKText(entry, font, targetWidth, NewLine, allowPunctuationOverflowMaxWidthInCJK);
            }
            else if (entry.Text.TrimStart(' ').TrimEnd(' ').Split(' ').Length == 1 && !useDashWhenWordAcrossLineInLatin)
            {
                // If the text is a single word (no spaces), we can break it by character to avoid overflow
                return BreakCJKText(entry, font, targetWidth, NewLine, false);
            }
            else
            {
                return BreakLatinText(entry, font, targetWidth, NewLine, useDashWhenWordAcrossLineInLatin);
            }
        }

        private static string BreakGraphemeClusters(TextEntry entry, FontFace font, float targetWidth, string newLine)
        {
            var engine = new NormalTypesettingEngine();
            var sb = new StringBuilder(); float width = 0f;
            var e = StringInfo.GetTextElementEnumerator(entry.Text);
            while (e.MoveNext())
            {
                string element = e.GetTextElement();
                if (element is "\n" or "\r" or "\r\n") { sb.Append(newLine); width = 0; continue; }
                var single = entry with { Text = element };
                float advance = engine.Measure(single, font).width + entry.CharacterSpacing;
                if (width > 0 && width + advance > targetWidth) { sb.Append(newLine); width = 0; }
                sb.Append(element); width += advance;
            }
            return sb.ToString();
        }


    }
}
