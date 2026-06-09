using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Text;

[MemoryDiagnoser]
public class TextLayoutBenchmarks : BenchmarkBase
{
    private NormalTypesettingEngine _engine = null!;
    private FontFace _font = null!;
    private TextEntry _entry = null!;

    public enum TextLength { Short, Medium, Long }

    [Params(TextLength.Short, TextLength.Medium, TextLength.Long)]
    public TextLength Length { get; set; }

    [Params(TextAlignment.Left, TextAlignment.Center, TextAlignment.Right)]
    public TextAlignment Alignment { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        NormalTypesettingEngine.DebugDumpAdvance = false;

        var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
        _font = FontFace.Load(fontPath);

        var text = Length switch
        {
            TextLength.Short => "Hello, World!",
            TextLength.Medium => "The quick brown fox jumps over the lazy dog.\n" +
                                 "Pack my box with five dozen liquor jugs.\n" +
                                 "How vexingly quick daft zebras jump!\n" +
                                 "The five boxing wizards jump quickly.\n" +
                                 "Sphinx of black quartz, judge my vow.",
            TextLength.Long => string.Join("\n", Enumerable.Repeat(
                "The quick brown fox jumps over the lazy dog. " +
                "Pack my box with five dozen liquor jugs. " +
                "How vexingly quick daft zebras jump! " +
                "The five boxing wizards jump quickly.\n" +
                "Sphinx of black quartz, judge my vow. " +
                "When zombies arrive, quickly fax each judge. " +
                "The jay, pig, fox, zebra, and my wolves quack!\n" +
                "Blowzy night-frumps vex'd Jack Q. " +
                "Grumpy wizards make a toxic brew for the jovial queen. " +
                "Fred specialized in the job of making very quaint wax toys.\n",
                20)),
            _ => ""
        };

        _entry = new TextEntry
        {
            Text = text,
            FontName = "OpenSans",
            FontSize = 0.1f,
            Alignment = Alignment
        };

        _engine = new NormalTypesettingEngine();
    }

    [Benchmark(Description = "Layout")]
    public VectorPicture Layout() => _engine.Layout(_entry, _font);
}
