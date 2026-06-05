using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text
{
    /// <summary>
    /// A simple class for rendering text into vector graphics using a provided <see cref="FontFace"/>.
    /// </summary>
    public static class TextRender
    {
        /// <summary>
        /// Render <paramref name="text"/> into a <see cref="VectorPicture"/> sized to
        /// <paramref name="targetWidth"/> × <paramref name="targetHeight"/>.
        /// The text is scaled so its font size is roughly 10 % of the shorter dimension.
        /// </summary>
        public static VectorPicture Render(string text, FontFace font, double targetWidth, double targetHeight)
        {
            var ts = new NormalTypesettingEngine
            {
                FontSize = (float)Math.Min(targetWidth, targetHeight) * 0.1f,
                FillR = 0,
                FillG = 0,
                FillB = 0,
                FillA = 1f,
            };

            return ts.Layout(text, font);
        }

        /// <summary>
        /// Render a <see cref="TextEntry"/> into a <see cref="VectorPicture"/>
        /// using the specified <paramref name="primaryFont"/> and optional fallback fonts.
        /// </summary>
        public static VectorPicture Render(TextEntry entry, FontFace primaryFont, IList<FontFace>? fallbackFonts = null)
        {
            var ts = new NormalTypesettingEngine
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

            if (fallbackFonts is not null)
            {
                foreach (var fb in fallbackFonts)
                    ts.FallbackFonts.Add(fb);
            }

            var picture = ts.Layout(entry.Text, primaryFont);

            // Position elements according to the TextEntry coordinates
            foreach (var element in picture.Elements)
            {
                element.RelativeX += entry.X;
                element.RelativeY += entry.Y;
                element.LayerIndex = entry.LayerIndex;
                element.Rotation = entry.Rotation;
            }

            return picture;
        }
    }
}
