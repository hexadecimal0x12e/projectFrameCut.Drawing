using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Cropping;
using projectFrameCut.Drawing.Processing.Resizing;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.FontHelper.Table;
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
