using BenchmarkDotNet.Attributes;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks;

public abstract class BenchmarkBase
{
    public static IEnumerable<ImageSize> StandardSizes => ImageSize.StandardSizes;

    protected static string GetCjkFontPath()
    {
        var candidates = new[]
        {
            @"C:\Windows\Fonts\SIMKAI.ttf",
            @"C:\Windows\Fonts\msyh.ttc",
            @"C:\Windows\Fonts\simsun.ttc",
            @"C:\Windows\Fonts\msyhl.ttc",
            @"/System/Library/Fonts/PingFang.ttc",
            @"/System/Library/Fonts/STHeiti Light.ttc",
            @"/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
    }
}
