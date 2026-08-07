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
                FontFace resolved = primary;
                ushort glyph = primary.GetGlyphIndex(rune);
                if (glyph == 0)
                {
                    foreach (FontFace fb in fallbacks)
                    {
                        if (fb is null || ReferenceEquals(fb, primary)) continue;
                        glyph = fb.GetGlyphIndex(rune);
                        if (glyph != 0) { resolved = fb; break; }
                    }
                }
                if (glyph == 0)
                    System.Diagnostics.Debug.WriteLine($"[Emoji] No fallback glyph for U+{rune.Value:X} in cluster '{element}'; rendering .notdef from '{primary.FamilyName}'.");
                result.Add(new(rune.ToString(), start, element.Length, rune, resolved, glyph, null));
            }
        }
        return result;
    }
}
