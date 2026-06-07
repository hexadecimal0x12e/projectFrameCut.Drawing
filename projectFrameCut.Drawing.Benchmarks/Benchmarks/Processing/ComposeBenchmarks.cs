using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Composing;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Processing;

[MemoryDiagnoser]
public class ComposeBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private Picture8bpp _base8 = null!;
    private Picture8bpp _top8 = null!;
    private Picture16bpp _base16 = null!;
    private Picture16bpp _top16 = null!;
    private CPUBlendPictureComposer _composer = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _composer = new CPUBlendPictureComposer();
        _base8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
        _top8 = BenchmarkImageFactory.CreateRandomBytePicture(InputSize.Width, InputSize.Height);
        _base16 = BenchmarkImageFactory.CreateRandomUShortPicture(InputSize.Width, InputSize.Height);
        _top16 = BenchmarkImageFactory.CreateRandomUShortPicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "Compose 8bpp Overlay")]
    public IPicture<byte> Compose8bpp_Overlay() =>
        _composer.Compose(_base8, _top8, BlendMode.Overlay);

    [Benchmark(Description = "Compose 8bpp Multiply")]
    public IPicture<byte> Compose8bpp_Multiply() =>
        _composer.Compose(_base8, _top8, BlendMode.Multiply);

    [Benchmark(Description = "Compose 8bpp Screen")]
    public IPicture<byte> Compose8bpp_Screen() =>
        _composer.Compose(_base8, _top8, BlendMode.Screen);

    [Benchmark(Description = "Compose 8bpp Add")]
    public IPicture<byte> Compose8bpp_Add() =>
        _composer.Compose(_base8, _top8, BlendMode.Add);

    [Benchmark(Description = "Compose 16bpp Overlay")]
    public IPicture<ushort> Compose16bpp_Overlay() =>
        _composer.Compose(_base16, _top16, BlendMode.Overlay);
}
