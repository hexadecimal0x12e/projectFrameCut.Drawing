using projectFrameCut.Drawing.Text.FontHelper;

namespace projectFrameCut.Drawing.Gallary.Services;


internal static class FontDiscoveryService
{
    public static List<FontFace> DiscoverSystemFonts()
    {
        string? fontsDir = GetSystemFontsDirectory();
        if (fontsDir == null || !Directory.Exists(fontsDir))
            return [];

        var fonts = new List<FontFace>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fontFiles = Directory.EnumerateFiles(fontsDir, "*.*")
            .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

        foreach (var file in fontFiles)
        {
            bool ttfSeen = false, ttcSeen = false;
        ttf:
            try
            {
                ttfSeen = true;
                if (!string.IsNullOrEmpty(file) && seen.Add(file))
                    fonts.Add(FontFace.Load(file));
            }
            catch
            {
                if (!ttcSeen) goto ttc;
            }
        ttc:
            try
            {
                ttcSeen = true;
                if (!string.IsNullOrEmpty(file) && seen.Add(file))
                    fonts.AddRange(FontCollection.Load(file).Select(C => C.Load()));
            }
            catch
            {
                if (!ttfSeen) goto ttf;
            }

        }



        return fonts.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }


    private static string? GetSystemFontsDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (OperatingSystem.IsLinux())
            return "/usr/share/fonts";
        if (OperatingSystem.IsMacOS())
            return "/System/Library/Fonts";
        return null;
    }
}
