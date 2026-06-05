using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.Typology
{
    public interface ITypesettingEngine
    {
        VectorPicture Layout(TextEntry entry, FontFace font);
        (float width, float height) Measure(TextEntry entry, FontFace font);
    }
}
