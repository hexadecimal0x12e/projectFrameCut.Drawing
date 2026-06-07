using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class SharpenEffectBenchmarks : BenchmarkBase{
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

    [Benchmark(Description = "Sharpen 8bpp")]
    public IPicture<byte> Sharpen8() => SharpenEffect.Process(_source8, 0.5f);

    [Benchmark(Description = "Sharpen 16bpp")]
    public IPicture<ushort> Sharpen16() => SharpenEffect.Process(_source16, 0.5f);
}
