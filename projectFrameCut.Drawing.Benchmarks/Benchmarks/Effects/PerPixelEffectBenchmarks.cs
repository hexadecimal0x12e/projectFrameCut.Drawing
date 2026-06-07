using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Effects;

[MemoryDiagnoser]
public class PerPixelEffectBenchmarks : BenchmarkBase{
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

    [Benchmark(Description = "Brightness 8bpp")]
    public IPicture<byte> Brightness8() => BrightnessEffect.Process(_source8, 0.5f);

    [Benchmark(Description = "Contrast 8bpp")]
    public IPicture<byte> Contrast8() => ContrastEffect.Process(_source8, 1.5f);

    [Benchmark(Description = "Saturation 8bpp")]
    public IPicture<byte> Saturation8() => SaturationEffect.Process(_source8, 1.2f);

    [Benchmark(Description = "Gamma 8bpp")]
    public IPicture<byte> Gamma8() => GammaEffect.Process(_source8, 2.2f);

    [Benchmark(Description = "Threshold 8bpp")]
    public IPicture<byte> Threshold8() => ThresholdEffect.Process(_source8, 0.5f);

    [Benchmark(Description = "Invert 8bpp")]
    public IPicture<byte> Invert8() => InvertEffect.Process(_source8);

    [Benchmark(Description = "Grayscale 8bpp")]
    public IPicture<byte> Grayscale8() => GrayscaleEffect.Process(_source8);

    [Benchmark(Description = "Opacity 8bpp")]
    public IPicture<byte> Opacity8() => OpacityEffect.Process(_source8, 0.5f);

    [Benchmark(Description = "Invert 16bpp")]
    public IPicture<ushort> Invert16() => InvertEffect.Process(_source16);
}
