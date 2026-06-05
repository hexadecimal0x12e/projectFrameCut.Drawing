using System.Text.Json.Serialization;

namespace projectFrameCut.Drawing.Text.Entry
{
    public record TextEntry
    {
        public required string Text { get; set; }
        
        public required string FontName { get; set; }
        public string FontStyle { get; set; } = "Regular";
        public string[] FallbackFonts { get; set; } = [];
        public float FontSize { get; set; } = 0.1f;
        
        public float X { get; set; }
        public float Y { get; set; }
        public int LayerIndex { get; set; }
        public float Rotation { get; set; }

        public ushort FillR { get; set; }
        public ushort FillG { get; set; }
        public ushort FillB { get; set; }
        public float FillA { get; set; } = 1f;

        public ushort StrokeR { get; set; }
        public ushort StrokeG { get; set; }
        public ushort StrokeB { get; set; }
        public float StrokeA { get; set; }
        public float StrokeThickness { get; set; }
        [JsonIgnore]
        public bool HasStroke => StrokeThickness > 0f && StrokeA > 0f;
        
        public float CharacterSpacing { get; set; }
        public float WordSpacing { get; set; }
        public float LineSpacing { get; set; } = 0.3f;

        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        public TextDecoration Decoration { get; set; } = TextDecoration.None;

        /// <summary>
        /// Variation axis coordinates for variable font support.
        /// Key = axis tag (e.g., "wght"), Value = desired coordinate (e.g., 700).
        /// Only used when the font supports OpenType Font Variations.
        /// </summary>
        public Dictionary<string, float> VariationAxes { get; set; } = new Dictionary<string, float>();

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    }
}
