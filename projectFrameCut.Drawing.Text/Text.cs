using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text
{
    /// <summary>
    /// A simple class for rendering text into vector graphics using a provided <see cref="FontFace"/>.
    /// </summary>
    internal static class TextRender
    {
        /// <summary>
        /// Render <paramref name="text"/> into a <see cref="VectorPicture"/> sized to
        /// <paramref name="targetWidth"/> × <paramref name="targetHeight"/>.
        /// The text is scaled so its font size is roughly 10 % of the shorter dimension.
        /// </summary>
        public static VectorPicture Render(string text, FontFace font, double targetWidth, double targetHeight)
        {
            var entry = new TextEntry
            {
                Text = text,
                FontName = font.FamilyName,
                FontSize = (float)Math.Min(targetWidth, targetHeight) * 0.1f,
                FillR = 0,
                FillG = 0,
                FillB = 0,
                FillA = 1f,
            };

            var ts = new NormalTypesettingEngine();
            return ts.Layout(entry, font);
        }

        /// <summary>
        /// Render a <see cref="TextEntry"/> into a <see cref="VectorPicture"/>
        /// using the specified <paramref name="primaryFont"/> and optional fallback fonts.
        /// </summary>
        public static VectorPicture Render(TextEntry entry, FontFace primaryFont, IList<FontFace>? fallbackFonts = null)
        {
            var ts = new NormalTypesettingEngine();

            if (fallbackFonts is not null)
            {
                foreach (var fb in fallbackFonts)
                    ts.FallbackFonts.Add(fb);
            }

            var picture = ts.Layout(entry, primaryFont);

            // Layout already sets BaseX/Y and LayerIndex/Rotation on every element.
            return picture;
        }
    }
}
