using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.Typology
{
    /// <summary>Defines operations for text layout and measurement.</summary>
    public interface ITypesettingEngine
    {
        /// <summary>Layout text from an entry into a vector picture.</summary>
        VectorPicture Layout(TextEntry entry, FontFace font);
        /// <summary>Measure the dimensions of the text without rendering.</summary>
        (float width, float height) Measure(TextEntry entry, FontFace font);
    }
}
