using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class FlipEffectBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;
    private Picture16bpp _source16 = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
        _source16 = BenchmarkImageFactory.CreateRandomUShortPicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Flip horizontal 8bpp")]
    public IPicture<byte> FlipHorizontal8() => FlipEffect.Process(_source8, true, false);

    [Benchmark(Description = "Flip vertical 8bpp")]
    public IPicture<byte> FlipVertical8() => FlipEffect.Process(_source8, false, true);

    [Benchmark(Description = "Flip both 16bpp")]
    public IPicture<ushort> FlipBoth16() => FlipEffect.Process(_source16, true, true);
}
