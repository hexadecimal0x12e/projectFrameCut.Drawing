using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Base.ReadWriteConvert;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.IO;

[MemoryDiagnoser]
public class VfdBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _source8 = null!;
    private MemoryStream _vfdStream8 = null!;
    private VfdPictureEncoder _encoder = null!;
    private VfdPictureDecoder _decoder = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _encoder = new VfdPictureEncoder();
        _decoder = new VfdPictureDecoder();
        _source8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);

        _vfdStream8 = new MemoryStream();
        _encoder.Save(_source8, _vfdStream8);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _vfdStream8.Position = 0;
    }

    [Benchmark(Description = "VFD encode 8bpp")]
    public void VfdEncode8()
    {
        _encoder.Save(_source8, Stream.Null);
    }

    [Benchmark(Description = "VFD decode 8bpp")]
    public IPicture<byte> VfdDecode8()
    {
        return _decoder.LoadByte(_vfdStream8);
    }
}
