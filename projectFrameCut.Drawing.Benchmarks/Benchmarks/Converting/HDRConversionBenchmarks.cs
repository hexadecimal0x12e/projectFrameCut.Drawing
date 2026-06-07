using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Converting;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Converting;

[MemoryDiagnoser]
public class HDRConversionBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize InputSize { get; set; }

    private HDRPicture16bpp _sourceHdr = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;
        _sourceHdr = BenchmarkImageFactory.CreateRandomHDRPicture(InputSize.Width, InputSize.Height);
    }

    [Benchmark(Description = "HDR to SDR (NormalizeBrightnessToRGB)")]
    public Picture16bpp HdrToSdr_Normalize()
    {
        return HDRPictureConverter.Default.ToSDR(_sourceHdr, HDRImageDegradeToSDRMode.NormalizeBrightnessToRGB);
    }

    [Benchmark(Description = "HDR to SDR (OverlayMaskFromBrightness)")]
    public Picture16bpp HdrToSdr_Overlay()
    {
        return HDRPictureConverter.Default.ToSDR(_sourceHdr, HDRImageDegradeToSDRMode.OverlayMaskFromBrightness);
    }
}
