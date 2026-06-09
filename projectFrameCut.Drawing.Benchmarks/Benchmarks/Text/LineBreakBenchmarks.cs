using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Text;

[MemoryDiagnoser]
public class LineBreakBenchmarks : BenchmarkBase
{
    private FontFace _font = null!;
    private string _text = null!;
    private TextEntry _entry = null!;

    public enum TextLength { Short, Long }

    [Params(TextLength.Short, TextLength.Long)]
    public TextLength Length { get; set; }

    [Params(0.5f, 0.2f)]
    public float TargetWidth { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        NormalTypesettingEngine.DebugDumpAdvance = false;

        var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
        _font = FontFace.Load(fontPath);

        _text = Length switch
        {
            TextLength.Short => "Hello, World! This is a short sentence.",
            TextLength.Long => "The quick brown fox jumps over the lazy dog. " +
                               "Pack my box with five dozen liquor jugs. " +
                               "How vexingly quick daft zebras jump! " +
                               "The five boxing wizards jump quickly. " +
                               "Sphinx of black quartz, judge my vow. " +
                               "When zombies arrive, quickly fax each judge. " +
                               "The jay, pig, fox, zebra, and my wolves quack! " +
                               "Blowzy night-frumps vex'd Jack Q.",
            _ => ""
        };

        _entry = new TextEntry
        {
            Text = _text,
            FontName = "OpenSans",
            FontSize = 0.1f
        };
    }

    [Benchmark(Description = "BreakLine (string)")]
    public string BreakLineString() => LineBreakHandler.BreakLine(_text, _font, TargetWidth);

    [Benchmark(Description = "BreakLine (TextEntry)")]
    public string BreakLineEntry() => LineBreakHandler.BreakLine(_entry, _font, TargetWidth);
}
