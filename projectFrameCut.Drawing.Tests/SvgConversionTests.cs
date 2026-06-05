using Vc = projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class SvgConversionTests
{
    [TestMethod]
    public void Export_Rectangle_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.RectangleVectorSegment
            {
                X = 0.1f, Y = 0.2f,
                Width = 0.5f, Height = 0.3f,
                FillR = ushort.MaxValue, FillG = 0, FillB = 0, FillA = 1f,
                Thickness = 2f,
                StrokeR = 0, StrokeG = 0, StrokeB = ushort.MaxValue, StrokeA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 200, 100);
        Assert.IsNotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("<rect", svg);
        Assert.Contains("fill=\"#FF0000\"", svg);
        Assert.Contains("stroke=\"#0000FF\"", svg);
        Assert.Contains("stroke-width=\"2\"", svg);
    }

    [TestMethod]
    public void Export_Ellipse_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.EllipseVectorSegment
            {
                X = 0.5f, Y = 0.5f,
                RadiusX = 0.3f, RadiusY = 0.2f,
                FillR = 0, FillG = ushort.MaxValue, FillB = 0, FillA = 0.5f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 400, 300);
        Assert.Contains("cx=\"200\"", svg);
        Assert.Contains("cy=\"150\"", svg);
        Assert.Contains("fill=\"#00FF00\"", svg);
        Assert.Contains("fill-opacity=\"0.5\"", svg);
    }

    [TestMethod]
    public void Export_Line_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.StraightLineVectorSegment
            {
                X1 = 0f, Y1 = 0f, X2 = 0.5f, Y2 = 0.5f,
                Thickness = 3f,
                StrokeR = ushort.MaxValue, StrokeG = ushort.MaxValue, StrokeB = 0, StrokeA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 100, 100);
        Assert.Contains("x1=\"0\"", svg);
        Assert.Contains("y2=\"50\"", svg);
    }

    [TestMethod]
    public void Export_CubicBezier_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.CubicBezierVectorSegment
            {
                X1 = 0f, Y1 = 0f, X2 = 0.2f, Y2 = 0.8f,
                X3 = 0.4f, Y3 = 0.2f, X4 = 0.6f, Y4 = 0.6f,
                Thickness = 1.5f,
                StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 500, 500);
        Assert.Contains("C ", svg);
        Assert.Contains("fill=\"none\"", svg);
    }

    [TestMethod]
    public void Export_Polygon_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.PolygonVectorSegment
            {
                Points = [new Vc.Point(0.1f, 0.1f), new Vc.Point(0.5f, 0.1f), new Vc.Point(0.3f, 0.5f)],
                FillR = 128, FillG = 128, FillB = 128, FillA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 200, 200);
        Assert.Contains("<polygon", svg);
        Assert.Contains("points=", svg);
    }

    [TestMethod]
    public void Export_Arc_RendersCorrectSvg()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.ArcVectorSegment
            {
                X = 0.5f, Y = 0.5f,
                RadiusX = 0.3f, RadiusY = 0.3f,
                StartAngle = 0f, SweepAngle = MathF.PI,
                Thickness = 2f,
                StrokeR = 255, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 200, 200);
        Assert.Contains("A ", svg);
        Assert.Contains("stroke-width=\"2\"", svg);
    }

    [TestMethod]
    public void Export_EmptyCanvas_ReturnsValidSvg()
    {
        var canvas = new Vc.VectorPicture();
        var svg = SVGToVectorElement.ExportToSvg(canvas, 100, 100);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [TestMethod]
    public void Import_BasicRect_RoundTrips()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
              <rect x="20" y="10" width="100" height="50" fill="#FF0000" stroke="#0000FF" stroke-width="2"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        Assert.HasCount(1, canvas.Elements);

        var segments = canvas.Elements[0].Draw();
        Assert.HasCount(1, segments);
        var rect = segments[0] as Vc.RectangleVectorSegment;
        Assert.IsNotNull(rect);

        Assert.AreEqual(0.1f, rect.X, 0.01f);
        Assert.AreEqual(0.1f, rect.Y, 0.01f);
        Assert.AreEqual(0.5f, rect.Width, 0.01f);
        Assert.AreEqual(0.5f, rect.Height, 0.01f);
        Assert.AreEqual(2f, rect.Thickness, 0.01f);
    }

    [TestMethod]
    public void Import_Ellipse_ParsesCorrectly()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="300">
              <ellipse cx="200" cy="150" rx="100" ry="75" fill="blue" stroke="red" stroke-width="1"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        Assert.HasCount(1, canvas.Elements);

        var segs = canvas.Elements[0].Draw();
        var ellipse = segs[0] as Vc.EllipseVectorSegment;
        Assert.IsNotNull(ellipse);
        Assert.AreEqual(0.5f, ellipse.X, 0.01f);
        Assert.AreEqual(0.5f, ellipse.Y, 0.01f);
        Assert.AreEqual(0.25f, ellipse.RadiusX, 0.01f);
        Assert.AreEqual(0.25f, ellipse.RadiusY, 0.01f);
    }

    [TestMethod]
    public void Import_Line_ParsesCorrectly()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <line x1="10" y1="20" x2="80" y2="90" stroke="black" stroke-width="2"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        var segs = canvas.Elements[0].Draw();
        var line = segs[0] as Vc.StraightLineVectorSegment;
        Assert.IsNotNull(line);
        Assert.AreEqual(0.1f, line.X1, 0.01f);
        Assert.AreEqual(0.2f, line.Y1, 0.01f);
        Assert.AreEqual(0.8f, line.X2, 0.01f);
        Assert.AreEqual(0.9f, line.Y2, 0.01f);
    }

    [TestMethod]
    public void Import_Path_ParsesCorrectly()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <path d="M 10 10 L 50 50 L 90 10" fill="none" stroke="red" stroke-width="1"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        var allSegments = canvas.Elements.SelectMany(e => e.Draw()).ToArray();
        Assert.IsGreaterThanOrEqualTo(2, allSegments.Length);
        Assert.IsTrue(allSegments.Any(s => s is Vc.StraightLineVectorSegment));
    }

    [TestMethod]
    public void Import_Group_WithTranslate_ParsesCorrectly()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200">
              <g transform="translate(50, 30)">
                <rect x="10" y="10" width="40" height="40" fill="red"/>
              </g>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        var rect = canvas.Elements[0].Draw()[0] as Vc.RectangleVectorSegment;
        Assert.IsNotNull(rect);
        Assert.AreEqual(0.3f, rect.X, 0.01f);
        Assert.AreEqual(0.2f, rect.Y, 0.01f);
    }

    [TestMethod]
    public void RoundTrip_ExportThenImport_ProducesSameGeometry()
    {
        var original = new Vc.VectorPicture
        {
            Elements =
            {
                new SegmentCollectionElement(
                    new Vc.RectangleVectorSegment
                    {
                        X = 0.1f, Y = 0.2f, Width = 0.3f, Height = 0.4f,
                        FillR = ushort.MaxValue, FillG = 0, FillB = 0, FillA = 0.8f,
                        Thickness = 2f,
                        StrokeR = 0, StrokeG = 0, StrokeB = ushort.MaxValue, StrokeA = 1f,
                    }
                ),
                new SegmentCollectionElement(
                    new Vc.EllipseVectorSegment
                    {
                        X = 0.5f, Y = 0.5f, RadiusX = 0.25f, RadiusY = 0.15f,
                        FillR = 0, FillG = ushort.MaxValue, FillB = 0, FillA = 1f,
                        Thickness = 1f,
                        StrokeR = 0, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
                    }
                ),
            }
        };

        const int w = 400, h = 300;
        var svg = SVGToVectorElement.ExportToSvg(original, w, h);
        var imported = SVGToVectorElement.ImportFromSvg(svg);

        Assert.HasCount(original.Elements.Count, imported.Elements);

        for (var i = 0; i < original.Elements.Count; i++)
        {
            var origSegs = original.Elements[i].Draw();
            var impSegs = imported.Elements[i].Draw();
            Assert.HasCount(origSegs.Length, impSegs);

            for (var j = 0; j < origSegs.Length; j++)
            {
                Assert.AreEqual(origSegs[j].GetType(), impSegs[j].GetType());
                Assert.AreEqual(origSegs[j].FillA > 0f ? origSegs[j].FillA : 0f,
                                impSegs[j].FillA > 0f ? impSegs[j].FillA : 0f, 0.05f);
            }
        }
    }

    [TestMethod]
    public void Import_ViewBox_DimensionResolution()
    {
        const string svgContent = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 500 250">
              <rect x="0" y="0" width="250" height="125" fill="red"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svgContent);
        var rect = canvas.Elements[0].Draw()[0] as Vc.RectangleVectorSegment;
        Assert.IsNotNull(rect);
        Assert.AreEqual(0.5f, rect.Width, 0.01f);
        Assert.AreEqual(0.5f, rect.Height, 0.01f);
    }

    [TestMethod]
    public void Export_LayerIndex_SortsByLayer()
    {
        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.RectangleVectorSegment { X = 0, Y = 0, Width = 1, Height = 1, FillR = ushort.MaxValue, FillG = 0, FillB = 0, FillA = 1f }
        ) { LayerIndex = 5 });
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.RectangleVectorSegment { X = 0, Y = 0, Width = 1, Height = 1, FillR = 0, FillG = ushort.MaxValue, FillB = 0, FillA = 1f }
        ) { LayerIndex = 1 });
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.RectangleVectorSegment { X = 0, Y = 0, Width = 1, Height = 1, FillR = 0, FillG = 0, FillB = ushort.MaxValue, FillA = 1f }
        ) { LayerIndex = 3 });

        var svg = SVGToVectorElement.ExportToSvg(canvas, 100, 100);
        var greenPos = svg.IndexOf("#00FF00");
        var bluePos = svg.IndexOf("#0000FF");
        var redPos = svg.IndexOf("#FF0000");

        Assert.IsLessThan(bluePos, greenPos);
        Assert.IsLessThan(redPos, bluePos);
    }

    // ---------------------------------------------------------------
    // UsePrivateColorSavingMode tests
    // ---------------------------------------------------------------

    [TestCleanup]
    public void CleanupPrivateColorMode()
    {
        SVGToVectorElement.UsePrivateColorSavingMode = false;
    }

    [TestMethod]
    public void Export_PrivateColorMode_EmitsCustomAttributes()
    {
        SVGToVectorElement.UsePrivateColorSavingMode = true;

        var canvas = new Vc.VectorPicture();
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.RectangleVectorSegment
            {
                X = 0.1f, Y = 0.2f, Width = 0.5f, Height = 0.3f,
                FillR = ushort.MaxValue, FillG = 0, FillB = 0, FillA = 0.5f,
                Thickness = 2f,
                StrokeR = 0, StrokeG = 0, StrokeB = ushort.MaxValue, StrokeA = 1f,
            }
        ));

        var svg = SVGToVectorElement.ExportToSvg(canvas, 200, 100);

        // Standard attributes still present
        Assert.Contains("fill=\"#FF0000\"", svg);
        Assert.Contains("stroke=\"#0000FF\"", svg);
        Assert.Contains("fill-opacity=\"0.5\"", svg);

        // Custom private color attributes present
        Assert.Contains("fill_projectFrameCut.Drawing.Color=", svg);
        Assert.Contains("stroke_projectFrameCut.Drawing.Color=", svg);

        // Verify the fill private color contains the 16-bit red value
        Assert.Contains("#FFFF00000000", svg);

        // Verify the stroke private color contains the 16-bit blue value
        Assert.Contains("#00000000FFFF", svg);

        // Line segment should also have private stroke attribute
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.StraightLineVectorSegment
            {
                X1 = 0, Y1 = 0, X2 = 1, Y2 = 1,
                Thickness = 1f,
                StrokeR = 65535, StrokeG = 65535, StrokeB = 65535, StrokeA = 0.8f,
            }
        ));
        svg = SVGToVectorElement.ExportToSvg(canvas, 100, 100);
        Assert.Contains("stroke_projectFrameCut.Drawing.Color=", svg);

        // Bezier should also have private stroke attribute
        canvas.Elements.Add(new SegmentCollectionElement(
            new Vc.CubicBezierVectorSegment
            {
                X1 = 0, Y1 = 0, X2 = 0.5f, Y2 = 0.5f,
                X3 = 1, Y3 = 0, X4 = 1, Y4 = 1,
                Thickness = 1f,
                StrokeR = 65535, StrokeG = 0, StrokeB = 0, StrokeA = 1f,
            }
        ));
        svg = SVGToVectorElement.ExportToSvg(canvas, 100, 100);
        Assert.Contains("stroke_projectFrameCut.Drawing.Color=", svg);
    }

    [TestMethod]
    public void Import_PrivateColorAttributes_ParsesCorrectly()
    {
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
              <rect x="20" y="10" width="100" height="50"
                    fill="#FF0000" fill_projectFrameCut.Drawing.Color="#FFFF00000000{Convert.ToBase64String(BitConverter.GetBytes(1.0f))}"
                    stroke="#0000FF" stroke_projectFrameCut.Drawing.Color="#00000000FFFF{Convert.ToBase64String(BitConverter.GetBytes(1.0f))}"
                    stroke-width="2"/>
            </svg>
            """;

        var canvas = SVGToVectorElement.ImportFromSvg(svg);
        var rect = canvas.Elements[0].Draw()[0] as Vc.RectangleVectorSegment;
        Assert.IsNotNull(rect);

        // Private color should give exact 16-bit values (not truncated to 8-bit)
        Assert.AreEqual(ushort.MaxValue, rect.FillR);
        Assert.AreEqual(0, rect.FillG);
        Assert.AreEqual(0, rect.FillB);
        Assert.AreEqual(1.0f, rect.FillA, 0.001f);

        // Stroke
        Assert.AreEqual(0, rect.StrokeR);
        Assert.AreEqual(0, rect.StrokeG);
        Assert.AreEqual(ushort.MaxValue, rect.StrokeB);
        Assert.AreEqual(1.0f, rect.StrokeA, 0.001f);
    }

    [TestMethod]
    public void RoundTrip_PrivateColorMode_PreservesPrecision()
    {
        SVGToVectorElement.UsePrivateColorSavingMode = true;

        var original = new Vc.VectorPicture();
        var rect = new Vc.RectangleVectorSegment
        {
            X = 0.1f, Y = 0.2f, Width = 0.5f, Height = 0.3f,
            FillR = 12345, FillG = 40000, FillB = 1000, FillA = 0.753f,
            Thickness = 1.5f,
            StrokeR = 50000, StrokeG = 20000, StrokeB = 800, StrokeA = 0.333f,
        };
        original.Elements.Add(new SegmentCollectionElement(rect));

        var svg = SVGToVectorElement.ExportToSvg(original, 400, 300);
        var imported = SVGToVectorElement.ImportFromSvg(svg);
        var importedRect = imported.Elements[0].Draw()[0] as Vc.RectangleVectorSegment;
        Assert.IsNotNull(importedRect);

        // 16-bit color channels must match exactly
        Assert.AreEqual(rect.FillR, importedRect.FillR);
        Assert.AreEqual(rect.FillG, importedRect.FillG);
        Assert.AreEqual(rect.FillB, importedRect.FillB);
        Assert.AreEqual(rect.FillA, importedRect.FillA, 0.001f);

        Assert.AreEqual(rect.StrokeR, importedRect.StrokeR);
        Assert.AreEqual(rect.StrokeG, importedRect.StrokeG);
        Assert.AreEqual(rect.StrokeB, importedRect.StrokeB);
        Assert.AreEqual(rect.StrokeA, importedRect.StrokeA, 0.001f);

        Assert.AreEqual(rect.Thickness, importedRect.Thickness, 0.001f);
    }
}
