using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Converting;

[MemoryDiagnoser]
public class BitDepthConversionBenchmarks : BenchmarkBase{
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

    [Benchmark(Description = "Convert 8bpp to 16bpp")]
    public IPicture To16()
    {
        return _source8.ToBitPerPixel(IPicture.PicturePixelMode.UShortPicture);
    }

    [Benchmark(Description = "Convert 16bpp to 8bpp")]
    public IPicture To8()
    {
        return _source16.ToBitPerPixel(IPicture.PicturePixelMode.BytePicture);
    }
}
