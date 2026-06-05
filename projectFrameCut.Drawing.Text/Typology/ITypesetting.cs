using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Text.Typology
{
    public interface ITypesettingEngine
    {
        float FontSize { get; set; }
        float CharacterSpacing { get; set; }
        float LineSpacing { get; set; }
        TextAlignment Alignment { get; set; }
        IList<FontFace> FallbackFonts { get; set; }

        float FillA { get; set; }
        ushort FillB { get; set; }
        ushort FillG { get; set; }
        ushort FillR { get; set; }

        float StrokeA { get; set; }
        ushort StrokeB { get; set; }
        ushort StrokeG { get; set; }
        ushort StrokeR { get; set; }

        float StrokeThickness { get; set; }
        float WordSpacing { get; set; }

        Dictionary<string, float> VariationAxes { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="text"></param>
        /// <param name="font"></param>
        /// <returns></returns>
        VectorPicture Layout(string text, FontFace font);

        (float width, float height) Measure(string text, FontFace font);

    }


}