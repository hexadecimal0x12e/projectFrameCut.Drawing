using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Vector;

[MemoryDiagnoser]
public class VectorRenderBenchmarks : BenchmarkBase{
    [ParamsSource(nameof(StandardSizes))]
    public ImageSize OutputSize { get; set; }

    private VectorPicture _simpleVector = null!;
    private VectorPicture _complexVector = null!;

    [GlobalSetup]
    public void Setup()
    {
        PictureLifecycleTracker.Enabled = false;

        _simpleVector = new VectorPicture
        {
            Elements =
            {
                ShapeCanvasElement.DrawRectangle(0.8f, 0.8f)
                    .WithFill(ushort.MaxValue, 0, 0, 0.5f)
                    .WithStroke(0, 0, 0, 1f, 2f)
                    .WithPosition(0.1f, 0.1f)
                    .WithLayer(0),

                ShapeCanvasElement.DrawEllipse(0.3f, 0.3f)
                    .WithFill(0, ushort.MaxValue, 0, 0.5f)
                    .WithStroke(0, 0, 0, 1f, 2f)
                    .WithPosition(0.5f, 0.5f)
                    .WithLayer(1),
            }
        };

        var rng = new Random(42);
        var elements = new List<VectorCanvasElement>();
        for (int i = 0; i < 50; i++)
        {
            float x = (float)rng.NextDouble() * 0.8f + 0.1f;
            float y = (float)rng.NextDouble() * 0.8f + 0.1f;
            float w = (float)rng.NextDouble() * 0.3f + 0.05f;
            float h = (float)rng.NextDouble() * 0.3f + 0.05f;
            var r = (ushort)rng.Next(65536);
            var g = (ushort)rng.Next(65536);
            var b = (ushort)rng.Next(65536);

            elements.Add(
                (i % 2 == 0
                    ? ShapeCanvasElement.DrawRectangle(w, h)
                    : ShapeCanvasElement.DrawEllipse(w * 0.5f, h * 0.5f))
                .WithFill(r, g, b, 0.3f)
                .WithStroke(0, 0, 0, 1f, 1f)
                .WithPosition(x, y)
                .WithLayer(i));
        }
        _complexVector = new VectorPicture { Elements = elements };
    }

    [Benchmark(Description = "Render simple vector (2 shapes)")]
    public IPicture RenderSimple() =>
        new CPUVectorPictureRasterizer().Convert(_simpleVector, OutputSize.Width, OutputSize.Height, false);

    [Benchmark(Description = "Render complex vector (50 shapes)")]
    public IPicture RenderComplex() =>
        new CPUVectorPictureRasterizer().Convert(_complexVector, OutputSize.Width, OutputSize.Height, false);
}
