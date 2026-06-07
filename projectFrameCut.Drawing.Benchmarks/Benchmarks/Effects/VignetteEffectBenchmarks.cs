using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class VignetteEffectBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Vignette 8bpp")]
    public IPicture<byte> Vignette() => VignetteEffect.Process(_source8, 0.5f, 0.75f);
}
