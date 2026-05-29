using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Cropping;
using projectFrameCut.Drawing.Processing.Resizing;
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
            var testSrc = GetImagePath();
            Console.WriteLine($"Test image path: {testSrc}");
            var p = new Picture16bpp(testSrc);
            Console.WriteLine(p.GetDiagnosticsInfo()); 
            Picture16bpp result = p
                .EnterProcessContext()
                .Crop(10,20,500,600)
                .Resize(50, 60, false);
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
            Console.WriteLine(result.GetDiagnosticsInfo());
            result.SaveToDisk(Path.Combine(Path.GetDirectoryName(testSrc) ?? "", $"result-{DateTime.Now:yyyyMMddHHmmss}.png"), PictureExtensions.SharedPngPictureEncoder);
        }
    }
}
