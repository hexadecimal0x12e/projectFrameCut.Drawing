using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Text.FontHelper;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.FontHelper;

[MemoryDiagnoser]
public class FontLoadingBenchmarks : BenchmarkBase
{
    private byte[] _fontData = null!;

    [GlobalSetup]
    public void Setup()
    {
        var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
        _fontData = File.ReadAllBytes(fontPath);
    }

    [Benchmark(Description = "Load OpenSans-Regular TTF")]
    public FontFace LoadFont() => FontFace.Load(_fontData);
}
