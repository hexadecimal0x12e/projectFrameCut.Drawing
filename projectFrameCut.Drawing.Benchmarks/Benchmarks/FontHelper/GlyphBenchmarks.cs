using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.FontHelper;

[MemoryDiagnoser]
public class GlyphBenchmarks : BenchmarkBase
{
    private FontFace _font = null!;

    [Params('A', '1', 'é', '.')]
    public char Char { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
        _font = FontFace.Load(fontPath);
    }

    [Benchmark(Description = "Glyph index lookup")]
    public ushort GetGlyphIndex() => _font.GetGlyphIndex(Char);

    [Benchmark(Description = "Advance width lookup")]
    public ushort GetAdvanceWidth() => _font.GetAdvanceWidth(_font.GetGlyphIndex(Char));

    [Benchmark(Description = "Parse glyph outline")]
    public Glyph? GetGlyph() => _font.GetGlyph(_font.GetGlyphIndex(Char));
}
