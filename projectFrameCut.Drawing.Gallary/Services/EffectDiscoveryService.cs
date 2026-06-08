using System.Diagnostics;
using System.Reflection;
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;

namespace projectFrameCut.Drawing.Gallary.Services;

public record EffectInfo(string DisplayName, Func<Picture8bpp, IPicture<byte>> Apply);

internal static class EffectDiscoveryService
{
    private static readonly Dictionary<string, object?[]> ParamMap = new()
    {
        [nameof(InvertEffect)] = [],
        [nameof(GrayscaleEffect)] = [],
        [nameof(BrightnessEffect)] = [0.5f],
        [nameof(ContrastEffect)] = [1.5f],
        [nameof(SaturationEffect)] = [1.2f],
        [nameof(GammaEffect)] = [2.2f],
        [nameof(ThresholdEffect)] = [0.5f, 0f, 1f],
        [nameof(FlipEffect)] = [true, false],
        [nameof(OpacityEffect)] = [0.6f],
        [nameof(VignetteEffect)] = [0.5f, 0.5f],
        [nameof(SharpenEffect)] = [1.5f],
        [nameof(BlurEffect)] = [3f],
        [nameof(HueRotationEffect)] = [90f],
    };

    private static readonly HashSet<string> Skip =
        [nameof(CropEffect), nameof(PlaceEffect), nameof(MaskEffect), nameof(RotationEffect)];

    public static List<EffectInfo> Discover()
    {
        var assembly = typeof(InvertEffect).Assembly;
        var results = new List<EffectInfo>
        {
        };

        foreach (var type in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true })
            .OrderBy(t => t.Name))
        {
            try
            {
                if (Skip.Contains(type.Name))
                    continue;

                if (!ParamMap.TryGetValue(type.Name, out var extraParams))
                    continue;

                var paramTypes = new[] { typeof(IPicture<byte>) }
                    .Concat(extraParams.Select(p => p!.GetType()))
                    .ToArray();

                var method = type.GetMethod("Process", BindingFlags.Public | BindingFlags.Static, paramTypes);
                if (method == null)
                    continue;

                var displayName = type.Name.EndsWith("Effect")
                    ? type.Name[..^6]
                    : type.Name;

                results.Add(new EffectInfo(displayName, src =>
                {
                    try
                    {
                        var args = new object?[] { src }.Concat(extraParams).ToArray();
                        return (IPicture<byte>)method.Invoke(null, args)!;
                    }
                    catch (Exception ex)
                    {
                        Debug.Write(ex);
                        return new Picture8bpp(src.Width, src.Height);
                    }
                }));
            }
            catch { }
        }

        results.Add(new EffectInfo("None (source image)", (s) => s));

        return results;
    }
}
