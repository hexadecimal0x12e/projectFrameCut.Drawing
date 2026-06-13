using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Cropping;
using projectFrameCut.Drawing.Processing.Resizing;
using projectFrameCut.Drawing.Text;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;
using System.Text.Json;

namespace projectFrameCut.Drawing.Test.App
{
    internal class Program
    {
        public static string GetImagePath()
        {
            var TestImagePath = "";
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (Path.GetDirectoryName(dir) != null)
            {
                if (dir is null) throw new FileNotFoundException("Test image 'sample.png' not found in any parent directory.", TestImagePath);
                var testImagePath = System.IO.Path.Combine(dir, "sample.png");
                if (System.IO.File.Exists(testImagePath))
                {
                    TestImagePath = testImagePath;
                    break;
                }
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            if (!File.Exists(TestImagePath)) throw new FileNotFoundException("Test image 'sample.png' not found in any parent directory.", TestImagePath);

            return TestImagePath;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            SVGToVectorElement.UsePrivateColorSavingMode = true;
            var mode = args.Length > 0 ? args[0] : "";
            switch (mode)
            {
                case "IPicture":
                    {
                        var testSrc = GetImagePath();
                        Console.WriteLine($"Test image path: {testSrc}");
                        var p = new Picture16bpp(testSrc);
                        Console.WriteLine(p.GetDiagnosticsInfo());
                        Picture16bpp result = p
                            .EnterProcessContext()
                            .Crop(10, 20, 500, 600)
                            .Resize(50, 60, false);
                        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                        Console.WriteLine(result.GetDiagnosticsInfo());
                        result.SaveToDisk(Path.Combine(Path.GetDirectoryName(testSrc) ?? "", $"result-{DateTime.Now:yyyyMMddHHmmss}.png"), PictureExtensions.SharedPngPictureEncoder);
                        return;
                    }
                case "font":
                    {
                        Console.Write("Input font file path: ");
                        var path = Console.ReadLine();
                        if (!File.Exists(path))
                        {
                            var fontFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            var sysDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
                            try
                            {
                                if (Directory.Exists(sysDir))
                                {
                                    foreach (var f in (new[] { "*.ttf", "*.otf", "*.ttc" }).SelectMany(ext => Directory.GetFiles(sysDir, ext)))
                                    {
                                        fontFiles.Add(f);
                                    }
                                }
                            }
                            catch { }

                            foreach (var item in fontFiles)
                            {
                                try
                                {
                                    if (Path.GetExtension(item).ToLower() == ".ttc")
                                    {
                                        var collection = FontFace.OpenTtcCollection(item);
                                        foreach (var f in collection)
                                        {
                                            Console.WriteLine($"{item} - {f.FamilyName}:");
                                            using var font = f.Load();
                                            Console.WriteLine($"字体名称: {font.FamilyName}");
                                            Console.WriteLine($"子系列: {font.SubfamilyName}");
                                            Console.WriteLine($"显示名称: {font.DisplayName}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"{item}:");
                                        using var font = FontFace.Load(item);
                                        Console.WriteLine($"字体名称: {font.FamilyName}");
                                        Console.WriteLine($"子系列: {font.SubfamilyName}");
                                        Console.WriteLine($"显示名称: {font.DisplayName}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                            return;
                        }
                        Console.Write("Input characters to query (leave empty for defaults): ");
                        var userStr = Console.ReadLine() ?? "";
                        if (Path.GetExtension(path) == ".ttc")
                        {
                            var collection = FontFace.OpenTtcCollection(path);
                            foreach (var item in collection)
                            {
                                using var font = item.Load();
                                ShowFontInfo(font, userStr);
                            }
                        }
                        else
                        {
                            using var font = FontFace.Load(path);
                            ShowFontInfo(font, userStr);
                        }
                        return;
                    }
                case "svg":
                    {
                        // Demonstrate the ShapeCanvasElement fluent API
                        var canvas = new VectorPicture();
                        canvas.Elements.AddRange(
                            [
                                ShapeCanvasElement.DrawRectangle(0.5f, 0.2f)
                                    .WithStroke(ushort.MaxValue, 0, 0, 1f, 2f)    // red stroke
                                    .WithFill(ushort.MaxValue, 0, 0, 0.3f)         // red fill, 30%
                                    .WithPosition(0.1f, 0.1f),

                                ShapeCanvasElement.DrawEllipse(0.15f, 0.15f)
                                    .WithFill(0, 0, ushort.MaxValue, 0.4f)         // blue fill, 40%
                                    .WithStroke(0, 0, ushort.MaxValue, 1f, 1.5f)   // blue stroke
                                    .WithPosition(0.7f, 0.3f),

                                ShapeCanvasElement.DrawPolygon(
                                        new Point(0.1f, 0.1f),
                                        new Point(0.5f, 0.1f),
                                        new Point(0.3f, 0.5f))
                                    .WithStroke(0, ushort.MaxValue, 0, 1f, 1f)     // green stroke
                                    .WithLayer(1),

                                ShapeCanvasElement.DrawLine(0f, 0f, 0.3f, 0.3f)
                                    .WithStroke(0, 0, 0, 1f, 3f)                   // black thick line
                                    .WithPosition(0.5f, 0.5f),
                            ]);

                        Console.WriteLine($"Canvas created with {canvas.Elements.Count} elements.");
                        Console.WriteLine("Elements:");
                        foreach (var el in canvas.Elements)
                            Console.WriteLine($"  [{el.LayerIndex}] ({el.RelativeX:F2}, {el.RelativeY:F2}) → {el.Draw().Length} segment(s)");

                        // 导出为 SVG
                        const int svgWidth = 800, svgHeight = 600;
                        var svg = SVGToVectorElement.ExportToSvg(canvas, svgWidth, svgHeight);
                        Console.WriteLine($"SVG output ({svg.Length} chars):\n{svg}");

                        // 保存到文件
                        var outDir = AppDomain.CurrentDomain.BaseDirectory;
                        var svgPath = Path.Combine(outDir, $"result-{DateTime.Now:yyyyMMddHHmmss}.svg");
                        File.WriteAllText(svgPath, svg);
                        Console.WriteLine($"SVG saved to: {svgPath}");

                        // 从文件导入 SVG
                        var fromFile = SVGToVectorElement.ImportFromFile(svgPath);
                        Console.WriteLine($"Imported from file: {fromFile.Elements.Count} elements.");
                        Console.WriteLine("Elements:");
                        foreach (var el in fromFile.Elements)
                            Console.WriteLine($"  [{el.LayerIndex}] ({el.RelativeX:F2}, {el.RelativeY:F2}) → {el.Draw().Length} segment(s)");

                        var bitmap = new VectorToIPicture().Convert(canvas, svgWidth, svgHeight, false);
                        var pngPath = Path.Combine(outDir, $"result-{DateTime.Now:yyyyMMddHHmmss}.png");
                        Console.WriteLine($"Saved bitmap from vector canvas: {bitmap.GetDiagnosticsInfo()} to {pngPath}");
                        bitmap.SaveToDisk(pngPath, PictureExtensions.SharedPngPictureEncoder);

                        return;
                    }

                case "dumpAdvances":
                    {
                        Console.Write("Input font file path: ");
                        var path = Console.ReadLine();
                        if (!File.Exists(path))
                        {
                            Console.WriteLine("File not found.");
                            return;
                        }
                        FontFace font = null!;
                        if (Path.GetExtension(path).Equals(".ttc", StringComparison.InvariantCultureIgnoreCase))
                        {
                            font = FontCollection.Load(path)?.FirstOrDefault()?.Load();
                        }
                        else
                        {
                            font = FontFace.Load(path);
                        }
                        if (font is null) return;

                        Console.Write("Input text (default '一二三四五'): ");
                        var txt = Console.ReadLine();
                        if (string.IsNullOrEmpty(txt)) txt = "一二三四五";

                        Console.Write("Input font size 0..1 (default 0.18): ");
                        var fsStr = Console.ReadLine();
                        float fs = float.TryParse(fsStr, out var fsv) ? fsv : 0.18f;

                        var entry = new TextEntry
                        {
                            Text = txt,
                            FontName = font.FamilyName,
                            FontSize = fs,
                            X = 0f,
                            Y = 0f,
                            FillR = 0, FillG = 0, FillB = 0, FillA = 1f,
                        };

                        Console.WriteLine($"\n=== Font: {font.FamilyName} / {font.SubfamilyName} ===");
                        Console.WriteLine($"=== UPM: {font.UnitsPerEm} ===");
                        Console.WriteLine($"=== Text: '{txt}' (len={txt.Length})  FontSize={fs} ===\n");

                        Console.WriteLine("--- Measure ---");
                        var (mw, mh) = new NormalTypesettingEngine().Measure(entry, font);
                        Console.WriteLine($"\nMeasure -> width={mw:F4}  height={mh:F4}");

                        Console.WriteLine("\n--- Per-Character Glyph Bounds (em units) ---");
                        var upem = font.UnitsPerEm;
                        foreach (var ch in txt)
                        {
                            var gi = font.GetGlyphIndex(ch);
                            var glyph = font.GetGlyph(gi);
                            if (glyph != null && !glyph.IsEmpty)
                            {
                                var wEm = (glyph.XMax - glyph.XMin) / (float)upem;
                                var hEm = (glyph.YMax - glyph.YMin) / (float)upem;
                                Console.WriteLine($"  '{ch}' (glyph {gi}): bbox=({glyph.XMin},{glyph.YMin})-({glyph.XMax},{glyph.YMax}) font-units,  size={wEm:F6}×{hEm:F6} em");
                            }
                            else
                            {
                                Console.WriteLine($"  '{ch}' (glyph {gi}): (empty glyph)");
                            }
                        }

                        Console.WriteLine("\n--- Layout ---");
                        var vp = new NormalTypesettingEngine().Layout(entry, font);
                        Console.WriteLine($"\nLayout elements: {vp.Elements.Count}");
                        return;
                    }

                case "glyph2VectPicture":
                    {
                        Console.Write("Input font file path: ");
                        var path = Console.ReadLine();
                        if (!File.Exists(path))
                        {
                            Console.WriteLine("File not found.");
                            return;
                        }
                        FontFace font = null!;
                        if (Path.GetExtension(path).Equals(".ttc", StringComparison.InvariantCultureIgnoreCase))
                        {
                            font = FontCollection.Load(path)?.FirstOrDefault()?.Load();
                        }
                        else
                        {
                            font = FontFace.Load(path);
                        }
                        if (font is null) return;
                        Console.Write("Input character to convert to vector (default 'A'): ");
                        var str = Console.ReadLine();
                        var entry = new RichTextEntry
                        {
                            Text = string.IsNullOrEmpty(str) ? "A" : str,
                            FontName = font.FamilyName,
                            FontSize = 0.3f,
                            FillR = 0,
                            FillG = 0,
                            FillB = 0,
                            FillA = 1f,
                            X = 0.005f,
                            Y = 0.5f,
                            StyledRanges = Enumerable.Range(0, string.IsNullOrEmpty(str) ? 1 : str.Length)
                                .Select(i => new StyledRange
                                {
                                    Start = i,
                                    Length = 1,
                                    Style = new CharacterStyle
                                    {
                                        FontSize = 0.05f + 0.25f * Random.Shared.NextSingle(),
                                        FillR = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        FillG = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        FillB = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        FillA = Random.Shared.NextSingle(),
                                        StrokeR = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        StrokeG = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        StrokeB = (ushort?)Random.Shared.Next(0, ushort.MaxValue),
                                        StrokeA = Random.Shared.NextSingle(),
                                        Decoration = Enum.GetValues<TextDecoration>().Cast<TextDecoration>().Where(d => d != TextDecoration.None).OrderBy(_ => Random.Shared.Next()).FirstOrDefault(),
                                    }
                                })
                                .ToList(),
                            VariationAxes = new Dictionary<string, float> { { "wght", 900 } }
                        };
                        var size = new NormalTypesettingEngine().Measure(entry, font);
                        var canvas = new NormalTypesettingEngine().Layout(entry, font);
                        const float svgScale = 8000f;
                        var maxDim = Math.Max(size.width, size.height);
                        var svgSize = Math.Max(200, (int)(maxDim * svgScale));
                        var svg = SVGToVectorElement.ExportToSvg(canvas, svgSize, svgSize);
                        var outDir = AppDomain.CurrentDomain.BaseDirectory;
                        var svgPath = Path.Combine(outDir, $"result-{DateTime.Now:yyyyMMddHHmmss}.svg");
                        File.WriteAllText(svgPath, svg);
                        Console.WriteLine($"SVG saved to: {svgPath}");
                        return;
                    }
            }

            static void ShowFontInfo(FontFace font, string userStr)
            {
                // 2. 查看字体信息
                Console.WriteLine($"字体名称: {font.FamilyName}");
                Console.WriteLine($"子系列: {font.SubfamilyName}");
                Console.WriteLine($"显示名称: {font.DisplayName}");
                Console.WriteLine($"本地化名称: {string.Join(',', font.LocalizedNames.Select(c => $"{c.Key.DisplayName}({c.Key.PlatformName}): {c.Value}"))}");
                Console.WriteLine($"UnitsPerEm: {font.UnitsPerEm}");
                Console.WriteLine($"字形数量: {font.GlyphCount}");
                Console.WriteLine($"斜体: {font.IsItalic}");
                Console.WriteLine($"字重: {font.WeightClass}");

                // 3. 字符 → 字形索引
                if (string.IsNullOrWhiteSpace(userStr))
                {
                    GetGlyphInfo(font, 'A');
                    GetGlyphInfo(font, '你');
                    GetGlyphInfo(font, '好');
                    return;
                }
                else
                {
                    foreach (var item in userStr)
                    {
                        GetGlyphInfo(font, item);
                    }
                    return;
                }
            }

            static void GetGlyphInfo(FontFace font, char character)
            {
                ushort glyphIndex = font.GetGlyphIndex(character);
                Console.WriteLine($"字符 '{character}' 的字形索引: {glyphIndex}");

                // 4. 获取 advance width
                ushort width = font.GetAdvanceWidth(glyphIndex);
                Console.WriteLine($"Advance Width: {width} (font units)");

                // 5. 获取字形轮廓
                Glyph? glyph = font.GetGlyph(glyphIndex);

                if (glyph != null && !glyph.IsEmpty)
                {
                    Console.WriteLine($"字形包围盒: ({glyph.XMin}, {glyph.YMin}) - ({glyph.XMax}, {glyph.YMax})");
                    Console.WriteLine($"轮廓数量: {glyph.Contours.Length}");

                    for (int c = 0; c < glyph.Contours.Length; c++)
                    {
                        Console.WriteLine($"  轮廓 {c}: {glyph.Contours[c].Length} 个点");
                        foreach (var pt in glyph.Contours[c])
                        {
                            Console.WriteLine($"    ({pt.X}, {pt.Y}) {(pt.OnCurve ? "●" : "○")}");
                            // ● = 曲线上的点, ○ = 控制点(贝塞尔)
                        }
                    }
                }
            }


        }
    }
}
