using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Cropping;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Processing;

[MemoryDiagnoser]
public class CropBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;
    private Picture16bpp _source16 = null!;
    private CPUPictureCropper _cropper = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _cropper = new CPUPictureCropper();
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
        _source16 = BenchmarkImageFactory.CreateRandomUShortPicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Crop 8bpp center 50%")]
    public IPicture<byte> Crop8bpp_Center() =>
        _cropper.Crop(_source8, InputSize.Width / 4, InputSize.Height / 4,
            InputSize.Width / 2, InputSize.Height / 2);

    [Benchmark(Description = "Crop 8bpp edge 10%")]
    public IPicture<byte> Crop8bpp_Edge() =>
        _cropper.Crop(_source8, 0, 0, InputSize.Width / 10, InputSize.Height / 10);

    [Benchmark(Description = "Crop 16bpp center 50%")]
    public IPicture<ushort> Crop16bpp_Center() =>
        _cropper.Crop(_source16, InputSize.Width / 4, InputSize.Height / 4,
            InputSize.Width / 2, InputSize.Height / 2);
}
