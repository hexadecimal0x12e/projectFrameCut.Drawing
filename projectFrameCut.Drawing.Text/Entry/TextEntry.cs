using System.Text.Json.Serialization;

namespace projectFrameCut.Drawing.Text.Entry
{
    /// <summary>Describes a text layout entry with font, positioning, and styling properties.</summary>
    public record TextEntry
    {
        /// <summary>The text content to render.</summary>
        public required string Text { get; set; }

        /// <summary>Primary font name or path.</summary>
        public required string FontName { get; set; }
        /// <summary>Font style name (e.g., "Regular", "Bold").</summary>
        public string FontStyle { get; set; } = "Regular";
        /// <summary>Fallback font names or paths used when a glyph is missing in the primary font.</summary>
        public string[] FallbackFonts { get; set; } = [];
        /// <summary>
        /// Font size expressed as a fraction of the canvas height (the canvas is
        /// normalised 0..1 in both X and Y). A <c>FontSize</c> of <c>0.1</c>
        /// means the font's em-square occupies 10% of the canvas height.
        /// The render engine (<see cref="Typology.NormalTypesettingEngine"/>)
        /// converts this to a per-font-unit scale via <c>FontSize / UnitsPerEm</c>,
        /// so a glyph with <c>GetAdvanceWidth = UnitsPerEm</c> ends up with an
        /// advance of exactly <c>FontSize</c> in normalised space.
        /// </summary>
        public float FontSize { get; set; } = 0.1f;
        
        /// <summary>X position on the canvas (0..1).</summary>
        public float X { get; set; }
        /// <summary>Y position on the canvas (0..1).</summary>
        public float Y { get; set; }
        /// <summary>Canvas layer index (lower = drawn first).</summary>
        public int LayerIndex { get; set; }
        /// <summary>Rotation angle in radians.</summary>
        public float Rotation { get; set; }

        /// <summary>Fill red channel (0..65535).</summary>
        public ushort FillR { get; set; }
        /// <summary>Fill green channel (0..65535).</summary>
        public ushort FillG { get; set; }
        /// <summary>Fill blue channel (0..65535).</summary>
        public ushort FillB { get; set; }
        /// <summary>Fill alpha (0.0 = transparent, 1.0 = opaque).</summary>
        public float FillA { get; set; } = 1f;

        /// <summary>Stroke red channel (0..65535).</summary>
        public ushort StrokeR { get; set; }
        /// <summary>Stroke green channel (0..65535).</summary>
        public ushort StrokeG { get; set; }
        /// <summary>Stroke blue channel (0..65535).</summary>
        public ushort StrokeB { get; set; }
        /// <summary>Stroke alpha (0.0 = transparent, 1.0 = opaque).</summary>
        public float StrokeA { get; set; }
        /// <summary>Stroke thickness in normalized units.</summary>
        public float StrokeThickness { get; set; }
        /// <summary>Whether the text has a visible stroke.</summary>
        [JsonIgnore]
        public bool HasStroke => StrokeThickness > 0f && StrokeA > 0f;

        /// <summary>Additional spacing between characters in normalized units.</summary>
        public float CharacterSpacing { get; set; }
        /// <summary>Additional spacing between words in normalized units.</summary>
        public float WordSpacing { get; set; }
        /// <summary>Spacing between lines as a fraction of FontSize.</summary>
        public float LineSpacing { get; set; } = 0.3f;

        /// <summary>Horizontal text alignment.</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        /// <summary>Text decoration (underline, strikethrough).</summary>
        public TextDecoration Decoration { get; set; } = TextDecoration.None;

        /// <summary>Text flow direction (LTR or RTL).</summary>
        public TextFlowDirection FlowDirection { get; set; } = TextFlowDirection.LeftToRight;

        /// <summary>
        /// Variation axis coordinates for variable font support.
        /// Key = axis tag (e.g., "wght"), Value = desired coordinate (e.g., 700).
        /// Only used when the font supports OpenType Font Variations.
        /// </summary>
        public Dictionary<string, float> VariationAxes { get; set; } = new Dictionary<string, float>();

        /// <summary>Additional JSON extension data for custom properties.</summary>
        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    }
}
