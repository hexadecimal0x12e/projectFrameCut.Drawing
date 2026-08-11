using System.Globalization;
using System.Text;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Text.Typology;

internal sealed record ResolvedTextCluster(
    string Text, int Utf16Start, int Utf16Length, Rune? Rune,
    FontFace? Font, ushort GlyphIndex, ColorGlyphLayer[]? ColorLayers, int? ShapedAdvanceWidth = null)
{
    public bool IsNewLine => Text is "\n" or "\r" or "\r\n";
    public bool IsSpace => Rune?.Value == 0x20;
    public bool IsColorEmoji => ColorLayers is { Length: > 0 };
}

internal static class UnicodeTextPipeline
{
    public static List<ResolvedTextCluster> Resolve(string text, FontFace primary,
        IEnumerable<FontFace> fallbacks, ushort foregroundR, ushort foregroundG, ushort foregroundB)
    {
        var result = new List<ResolvedTextCluster>();
        FontFace? emoji = FontFace.EmojiFont; // one stable snapshot per layout/measure call
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int start = enumerator.ElementIndex;
            if (element is "\n" or "\r" or "\r\n")
            {
                result.Add(new(element, start, element.Length, null, null, 0, null));
                continue;
            }

            if (emoji is not null)
            {
                try
                {
                    var foreground = new ColorValue(foregroundR, foregroundG, foregroundB, 1f);
                    if (emoji.TryShapeColorEmoji(element, foreground, out ushort gid, out var layers, out int shapedAdvance))
                    {
                        result.Add(new(element, start, element.Length, null, emoji, gid, layers, shapedAdvance));
                        continue;
                    }
                }
                catch (ObjectDisposedException) { }
                catch (InvalidDataException) { }
            }

            // A flag is encoded as two regional indicators.  Some colour emoji
            // fonts contain those individual indicators but do not contain (or
            // cannot compose) every flag sequence.  Do not render either
            // indicator on its own in that case: use its ASCII country code
            // letters instead, so an unsupported flag never becomes tofu.
            if (TryGetRegionalIndicatorLetters(element, out string countryCode))
            {
                foreach (char letter in countryCode)
                {
                    Rune letterRune = new(letter);
                    var (font, glyph) = ResolveGlyph(letterRune, primary, fallbacks);
                    result.Add(new(letter.ToString(), start, element.Length, letterRune, font, glyph, null));
                }
                continue;
            }

            foreach (Rune rune in element.EnumerateRunes())
            {
                // Presentation/joining/tag controls have no standalone visual fallback.
                if (rune.Value is 0x200D or 0xFE0E or 0xFE0F || rune.Value is >= 0xE0020 and <= 0xE007F)
                    continue;
                // A failed sequence must still fall back to individually coloured
                // Emoji glyphs from EmojiFont, never to tofu from the text font.
                if (emoji is not null)
                {
                    try
                    {
                        var foreground = new ColorValue(foregroundR, foregroundG, foregroundB, 1f);
                        if (emoji.TryShapeColorEmoji(rune.ToString(), foreground, out ushort emojiGlyph, out var emojiLayers, out int emojiAdvance))
                        {
                            result.Add(new(rune.ToString(), start, element.Length, rune, emoji, emojiGlyph, emojiLayers, emojiAdvance));
                            continue;
                        }
                    }
                    catch (ObjectDisposedException) { }
                    catch (InvalidDataException) { }
                }
                var (resolved, glyph) = ResolveGlyph(rune, primary, fallbacks);
                if (glyph == 0)
                    System.Diagnostics.Debug.WriteLine($"[Emoji] No fallback glyph for U+{rune.Value:X} in cluster '{element}'; rendering .notdef from '{primary.FamilyName}'.");
                result.Add(new(rune.ToString(), start, element.Length, rune, resolved, glyph, null));
            }
        }
        return result;
    }

    private static bool TryGetRegionalIndicatorLetters(string textElement, out string countryCode)
    {
        countryCode = string.Empty;
        Rune[] runes = textElement.EnumerateRunes().ToArray();
        if (runes.Length != 2 || runes.Any(rune => rune.Value is < 0x1F1E6 or > 0x1F1FF))
            return false;

        countryCode = string.Create(2, runes, static (buffer, indicators) =>
        {
            buffer[0] = (char)('A' + indicators[0].Value - 0x1F1E6);
            buffer[1] = (char)('A' + indicators[1].Value - 0x1F1E6);
        });
        return true;
    }

    private static (FontFace Font, ushort Glyph) ResolveGlyph(Rune rune, FontFace primary,
        IEnumerable<FontFace> fallbacks)
    {
        ushort glyph = primary.GetGlyphIndex(rune);
        if (glyph != 0)
            return (primary, glyph);

        foreach (FontFace fb in fallbacks)
        {
            if (fb is null || ReferenceEquals(fb, primary)) continue;
            glyph = fb.GetGlyphIndex(rune);
            if (glyph != 0)
                return (fb, glyph);
        }
        return (primary, 0);
    }
}
