using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Base.ReadWriteConvert;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.IO;

[MemoryDiagnoser]
public class PngBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;
    private MemoryStream _pngStream8 = null!;
    private PngPictureEncoder _encoder = null!;
    private PngPictureDecoder _decoder = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _encoder = new PngPictureEncoder();
        _decoder = new PngPictureDecoder();
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);

        _pngStream8 = new MemoryStream();
        _encoder.Save(_source8, _pngStream8);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _pngStream8.Position = 0;
    }

    [Benchmark(Description = "PNG encode 8bpp")]
    public void PngEncode8()
    {
        _encoder.Save(_source8, Stream.Null);
    }

    [Benchmark(Description = "PNG decode 8bpp")]
    public IPicture<byte> PngDecode8()
    {
        return _decoder.LoadByte(_pngStream8);
    }
}
