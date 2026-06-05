using System.Text.Json.Serialization;

namespace projectFrameCut.Drawing.Text.Entry
{
    /// <summary>
    /// A rich-text entry that supports per-character style overrides via
    /// <see cref="StyledRange"/> entries. Base property values serve as
    /// defaults; any character covered by a <see cref="StyledRange"/> uses
    /// the overrides from that range (null override fields fall back to the
    /// base default).
    /// </summary>
    public record RichTextEntry : TextEntry
    {
        /// <summary>
        /// Styled ranges for per-character overrides. Ranges should not overlap;
        /// if they do, ranges earlier in the list take priority.
        /// </summary>
        public List<StyledRange> StyledRanges { get; set; } = new List<StyledRange>();

        /// <summary>
        /// Returns all <see cref="StyledRange"/> entries that cover the character
        /// at <paramref name="charIndex"/>, in list order.
        /// </summary>
        public IEnumerable<StyledRange> GetRangesAt(int charIndex)
        {
            foreach (var range in StyledRanges)
            {
                if (charIndex >= range.Start && charIndex < range.Start + range.Length)
                    yield return range;
            }
        }
    }

    /// <summary>
    /// A contiguous range of characters that share the same <see cref="CharacterStyle"/> override.
    /// Ranges should not overlap; if they do, the first matching range wins.
    /// </summary>
    public record StyledRange
    {
        /// <summary>Zero-based index of the first character in the range.</summary>
        public int Start { get; init; }

        /// <summary>Number of characters in the range.</summary>
        public int Length { get; init; }

        /// <summary>Style overrides applied to this range.</summary>
        public required CharacterStyle Style { get; init; }
    }

    /// <summary>
    /// Per-character style overrides. Null fields mean "use the default
    /// from the parent <see cref="RichTextEntry"/>".
    /// </summary>
    public record CharacterStyle
    {
        // ── Font ──
        public string? FontName { get; init; }
        public string? FontStyle { get; init; }
        public string[]? FallbackFonts { get; init; }
        public float? FontSize { get; init; }

        // ── Fill ──
        public ushort? FillR { get; init; }
        public ushort? FillG { get; init; }
        public ushort? FillB { get; init; }
        public float? FillA { get; init; }

        // ── Stroke ──
        public ushort? StrokeR { get; init; }
        public ushort? StrokeG { get; init; }
        public ushort? StrokeB { get; init; }
        public float? StrokeA { get; init; }
        public float? StrokeThickness { get; init; }

        // ── Spacing ──
        public float? CharacterSpacing { get; init; }
        public float? WordSpacing { get; init; }
        public float? LineSpacing { get; init; }

        // ── Alignment & Decoration ──
        public TextAlignment? Alignment { get; init; }
        public TextDecoration? Decoration { get; init; }

        // ── Variable font ──
        public Dictionary<string, float>? VariationAxes { get; init; }
    }
}
