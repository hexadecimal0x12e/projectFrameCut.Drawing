using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Resizing;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Processing;

[MemoryDiagnoser]
public class ResizeBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;
    private Picture16bpp _source16 = null!;
    private HDRPicture16bpp _sourceHdr = null!;
    private BilinearPictureResizer _resizer = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _resizer = new BilinearPictureResizer();
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
        _source16 = BenchmarkImageFactory.CreateRandomUShortPicture(InputSize.Width, InputSize.Height);
        _sourceHdr = BenchmarkImageFactory.CreateRandomHDRPicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Resize 8bpp to half")]
    public IPicture<byte> Resize8bpp_Half() =>
        _resizer.Resize(_source8, InputSize.Width / 2, InputSize.Height / 2, false);

    [Benchmark(Description = "Resize 8bpp to double")]
    public IPicture<byte> Resize8bpp_Double() =>
        _resizer.Resize(_source8, InputSize.Width * 2, InputSize.Height * 2, false);

    [Benchmark(Description = "Resize 16bpp to half")]
    public IPicture<ushort> Resize16bpp_Half() =>
        _resizer.Resize(_source16, InputSize.Width / 2, InputSize.Height / 2, false);

    [Benchmark(Description = "Resize HDR to half")]
    public IHDRPicture<ushort> ResizeHDR_Half() =>
        _resizer.Resize(_sourceHdr, InputSize.Width / 2, InputSize.Height / 2, false);
}
