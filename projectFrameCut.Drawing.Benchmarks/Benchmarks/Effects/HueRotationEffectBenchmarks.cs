using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class HueRotationEffectBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Hue rotation 8bpp 45deg")]
    public IPicture<byte> HueRotate45() => HueRotationEffect.Process(_source8, 45f);

    [Benchmark(Description = "Hue rotation 8bpp 180deg")]
    public IPicture<byte> HueRotate180() => HueRotationEffect.Process(_source8, 180f);
}
