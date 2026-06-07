using BenchmarkDotNet.Attributes;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks;

public abstract class BenchmarkBase
{
    public static IEnumerable<ImageSize> StandardSizes => ImageSize.StandardSizes;
}
