namespace projectFrameCut.Drawing.Benchmarks.Benchmarks;

public readonly record struct ImageSize(int Width, int Height)
{
    public int Pixels => Width * Height;

    public override string ToString() => $"{Width}x{Height}";

    public static IEnumerable<ImageSize> StandardSizes =>
    [
        new(512, 512),
        new(1920, 1080),
        new(3840, 2160)
    ];
}
