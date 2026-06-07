using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class RotationEffectBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    [Params(15f, 45f, 90f)]
    public float AngleDegrees { get; set; }

    private Picture8bpp _source8 = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Rotate 8bpp")]
    public IPicture<byte> Rotate() => RotationEffect.Process(_source8, AngleDegrees, true);
}
