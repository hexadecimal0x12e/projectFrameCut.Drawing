using System.Numerics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal readonly record struct ColorValue(ushort R, ushort G, ushort B, float A);
internal readonly record struct ColorStop(float Offset, ColorValue Color);
internal abstract record ColorBrush;
internal sealed record SolidColorBrush(ColorValue Color) : ColorBrush;
internal sealed record LinearColorBrush(byte Extend, ColorStop[] Stops, float X0, float Y0, float X1, float Y1, float X2, float Y2) : ColorBrush;
internal sealed record RadialColorBrush(byte Extend, ColorStop[] Stops, float X0, float Y0, float R0, float X1, float Y1, float R1) : ColorBrush;
internal sealed record SweepColorBrush(byte Extend, ColorStop[] Stops, float CenterX, float CenterY, float StartAngle, float EndAngle) : ColorBrush;
internal readonly record struct ColorGlyphLayer(
    ushort GlyphId, ColorBrush Brush, Matrix3x2 GlyphTransform, Matrix3x2 BrushTransform);

/// <summary>Defensive parser for CPAL and the layer-oriented parts of COLR 0/1.</summary>
internal sealed class ColorEmojiTables
{
    private readonly byte[] _colr;
    private readonly ColorValue[] _palette;
    private readonly Dictionary<(ushort GlyphId, ColorValue Foreground), ColorGlyphLayer[]> _cache = [];
    private readonly HashSet<ushort> _resolving = [];

    public ColorEmojiTables(byte[] colr, byte[] cpal)
    {
        _colr = colr;
        _palette = ParsePalette(cpal);
    }

    public bool HasGlyph(ushort glyphId) => TryGetLayers(glyphId, default, out _);

    public bool TryGetLayers(ushort glyphId, ColorValue foreground, out ColorGlyphLayer[] layers)
    {
        var cacheKey = (glyphId, foreground);
        if (_cache.TryGetValue(cacheKey, out layers!)) return layers.Length != 0;
        if (!_resolving.Add(glyphId)) { layers = []; return false; }
        var result = new List<ColorGlyphLayer>();
        try
        {
            ushort version = U16(_colr, 0);
            // Modern emoji fonts (including Segoe UI Emoji) intentionally
            // carry a COLR v0 flat-colour fallback alongside their richer v1
            // paint graph for the very same glyph. Prefer v1 whenever it is
            // present; v0 is only the compatibility fallback.
            if (version >= 1)
                TryV1(glyphId, foreground, result);
            if (result.Count == 0)
                TryV0(glyphId, foreground, result);
        }
        catch { result.Clear(); }
        finally { _resolving.Remove(glyphId); }
        layers = result.ToArray();
        _cache[cacheKey] = layers;
        return layers.Length != 0;
    }

    private bool TryV0(ushort glyphId, ColorValue foreground, List<ColorGlyphLayer> output)
    {
        if (_colr.Length < 14) return false;
        int count = U16(_colr, 2), baseOff = I32(_colr, 4), layerOff = I32(_colr, 8);
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2, o = baseOff + mid * 6;
            if (o < 0 || o + 6 > _colr.Length) return false;
            ushort gid = U16(_colr, o);
            if (gid < glyphId) lo = mid + 1;
            else if (gid > glyphId) hi = mid - 1;
            else
            {
                int first = U16(_colr, o + 2), n = U16(_colr, o + 4);
                for (int i = 0; i < n; i++)
                {
                    int p = layerOff + (first + i) * 4;
                    if (p < 0 || p + 4 > _colr.Length) return false;
                    output.Add(new(U16(_colr, p), new SolidColorBrush(GetColor(U16(_colr, p + 2), foreground)),
                        Matrix3x2.Identity, Matrix3x2.Identity));
                }
                return output.Count != 0;
            }
        }
        return false;
    }

    private bool TryV1(ushort glyphId, ColorValue foreground, List<ColorGlyphLayer> output)
    {
        if (_colr.Length < 34) return false;
        int baseList = I32(_colr, 14), layerList = I32(_colr, 18);
        if (baseList <= 0 || baseList + 4 > _colr.Length) return false;
        int count = I32(_colr, baseList);
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2, r = baseList + 4 + mid * 6;
            if (r + 6 > _colr.Length) return false;
            ushort gid = U16(_colr, r);
            if (gid < glyphId) lo = mid + 1;
            else if (gid > glyphId) hi = mid - 1;
            else
            {
                int paint = baseList + I32(_colr, r + 2);
                var visiting = new HashSet<int>();
                ReadPaint(paint, layerList, foreground, Matrix3x2.Identity, Matrix3x2.Identity, output, visiting, 0, null);
                return output.Count != 0;
            }
        }
        return false;
    }

    private void ReadPaint(int p, int layerList, ColorValue foreground, Matrix3x2 glyphTransform, Matrix3x2 brushTransform,
        List<ColorGlyphLayer> output, HashSet<int> visiting, int depth, ushort? clipGlyph)
    {
        if (depth > 64 || p < 0 || p >= _colr.Length || !visiting.Add(p)) return;
        try
        {
            int format = _colr[p];
            if (format == 1) // PaintColrLayers
            {
                int n = _colr[p + 1], first = I32(_colr, p + 2);
                if (layerList <= 0 || layerList + 4 > _colr.Length) return;
                int total = I32(_colr, layerList);
                for (int i = 0; i < n && first + i < total; i++)
                    ReadPaint(layerList + I32(_colr, layerList + 4 + (first + i) * 4), layerList,
                        foreground, glyphTransform, brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is >= 2 and <= 9) // basic fill paint
            {
                // PaintGlyph supplies the outline that clips a basic fill. A
                // basic paint can be reached through one or more transforms,
                // so only emit when its clipping glyph is known.
                if (clipGlyph.HasValue)
                    output.Add(new(clipGlyph.Value, ReadBrush(p, foreground), glyphTransform, brushTransform));
            }
            else if (format == 10) // PaintGlyph: clip child paint to this outline
            {
                int child = p + U24(_colr, p + 1);
                ReadPaint(child, layerList, foreground, glyphTransform, brushTransform, output, visiting, depth + 1,
                    U16(_colr, p + 4));
            }
            else if (format == 11) // PaintColrGlyph
            {
                TryAppendNested(U16(_colr, p + 1), foreground, glyphTransform, brushTransform, output, depth + 1);
            }
            else if (format is 12 or 13) // affine transform
            {
                int child = p + U24(_colr, p + 1), t = p + U24(_colr, p + 4);
                Matrix3x2 m = FromColrAffine(t);
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is 14 or 15) // translate
            {
                int child = p + U24(_colr, p + 1);
                Matrix3x2 m = Matrix3x2.CreateTranslation(S16(_colr, p + 4), -S16(_colr, p + 6));
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is 16 or 17 or 18 or 19) // scale, optionally around centre
            {
                int child = p + U24(_colr, p + 1);
                float sx = F2(_colr, p + 4), sy = F2(_colr, p + 6);
                Vector2 center = format is 18 or 19 ? new(S16(_colr, p + 8), -S16(_colr, p + 10)) : Vector2.Zero;
                Matrix3x2 m = Matrix3x2.CreateScale(sx, sy, center);
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is 20 or 21 or 22 or 23) // uniform scale
            {
                int child = p + U24(_colr, p + 1); float scale = F2(_colr, p + 4);
                Vector2 center = format is 22 or 23 ? new(S16(_colr, p + 6), -S16(_colr, p + 8)) : Vector2.Zero;
                Matrix3x2 m = Matrix3x2.CreateScale(scale, center);
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is 24 or 25 or 26 or 27) // rotate; COLR angle is in half-turns
            {
                int child = p + U24(_colr, p + 1); float radians = -F2(_colr, p + 4) * MathF.PI;
                Vector2 center = format is 26 or 27 ? new(S16(_colr, p + 6), -S16(_colr, p + 8)) : Vector2.Zero;
                Matrix3x2 m = Matrix3x2.CreateRotation(radians, center);
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format is 28 or 29 or 30 or 31) // skew
            {
                int child = p + U24(_colr, p + 1);
                float ax = -MathF.Tan(F2(_colr, p + 4) * MathF.PI), ay = -MathF.Tan(F2(_colr, p + 6) * MathF.PI);
                Vector2 center = format is 30 or 31 ? new(S16(_colr, p + 8), -S16(_colr, p + 10)) : Vector2.Zero;
                Matrix3x2 m = Matrix3x2.CreateTranslation(-center) * new Matrix3x2(1, ay, ax, 1, 0, 0) * Matrix3x2.CreateTranslation(center);
                ReadPaint(child, layerList, foreground, clipGlyph.HasValue ? glyphTransform : m * glyphTransform,
                    m * brushTransform, output, visiting, depth + 1, clipGlyph);
            }
            else if (format == 32) // preserve backdrop/source order for source-over fallback
            {
                ReadPaint(p + U24(_colr, p + 5), layerList, foreground, glyphTransform, brushTransform, output, visiting, depth + 1, clipGlyph);
                ReadPaint(p + U24(_colr, p + 1), layerList, foreground, glyphTransform, brushTransform, output, visiting, depth + 1, clipGlyph);
            }
        }
        finally { visiting.Remove(p); }
    }

    private void TryAppendNested(ushort glyph, ColorValue foreground, Matrix3x2 glyphTransform, Matrix3x2 brushTransform,
        List<ColorGlyphLayer> output, int depth)
    {
        if (depth > 64 || !TryGetLayers(glyph, foreground, out var nested)) return;
        foreach (var l in nested)
            output.Add(l with
            {
                GlyphTransform = l.GlyphTransform * glyphTransform,
                BrushTransform = l.BrushTransform * brushTransform,
            });
    }

    private ColorBrush ReadBrush(int p, ColorValue foreground)
    {
        if (p < 0 || p >= _colr.Length) return new SolidColorBrush(foreground);
        int f = _colr[p];
        if (f is 2 or 3)
        {
            var c = GetColor(U16(_colr, p + 1), foreground);
            return new SolidColorBrush(c with { A = c.A * Math.Clamp(F2(_colr, p + 3), 0f, 1f) });
        }
        // The basic paint records contain both the ColorLine and the geometry
        // that maps it onto the design grid.  Keeping that geometry is essential:
        // reducing a gradient to its first stop makes COLR v1 emoji look flat.
        if (f is 4 or 5) // Paint[Var]LinearGradient
        {
            int line = p + U24(_colr, p + 1);
            return new LinearColorBrush(ReadExtend(line), ReadColorLine(line, foreground, f == 5),
                S16(_colr, p + 4), S16(_colr, p + 6), S16(_colr, p + 8),
                S16(_colr, p + 10), S16(_colr, p + 12), S16(_colr, p + 14));
        }
        if (f is 6 or 7) // Paint[Var]RadialGradient
        {
            int line = p + U24(_colr, p + 1);
            return new RadialColorBrush(ReadExtend(line), ReadColorLine(line, foreground, f == 7),
                S16(_colr, p + 4), S16(_colr, p + 6), U16(_colr, p + 8),
                S16(_colr, p + 10), S16(_colr, p + 12), U16(_colr, p + 14));
        }
        if (f is 8 or 9) // Paint[Var]SweepGradient
        {
            int line = p + U24(_colr, p + 1);
            // COLR stores sweep angles as a half-turn value with a +1 bias.
            // VectorGradientBrush uses radians in the canvas (Y-down) system;
            // ToVectorGradient performs the axis conversion.
            return new SweepColorBrush(ReadExtend(line), ReadColorLine(line, foreground, f == 9),
                S16(_colr, p + 4), S16(_colr, p + 6),
                (F2(_colr, p + 8) + 1f) * MathF.PI,
                (F2(_colr, p + 10) + 1f) * MathF.PI);
        }
        return new SolidColorBrush(foreground);
    }

    private byte ReadExtend(int p) => p < 0 || p >= _colr.Length ? (byte)0 : _colr[p];

    private Matrix3x2 FromColrAffine(int p)
    {
        // COLR matrices operate in the font's Y-up design grid, while glyph
        // contours are stored by this library in Y-down canvas coordinates.
        // Convert C * M * C (C = scale(1, -1)) before applying it to outlines.
        float xx = Fixed(_colr, p), yx = Fixed(_colr, p + 4);
        float xy = Fixed(_colr, p + 8), yy = Fixed(_colr, p + 12);
        float dx = Fixed(_colr, p + 16), dy = Fixed(_colr, p + 20);
        return new Matrix3x2(xx, -yx, -xy, yy, dx, -dy);
    }

    private ColorStop[] ReadColorLine(int p, ColorValue foreground, bool variable)
    {
        if (p < 0 || p + 3 > _colr.Length) return [new(0, foreground)];
        int count = U16(_colr, p + 1), o = p + 3;
        var result = new ColorStop[count];
        int size = variable ? 10 : 6; // VarColorStop adds a uint32 varIndexBase.
        for (int i = 0; i < count; i++, o += size)
        {
            if (o < 0 || o + size > _colr.Length) return result[..i];
            float offset = F2(_colr, o);
            ColorValue color = GetColor(U16(_colr, o + 2), foreground);
            result[i] = new(offset, color with { A = color.A * Math.Clamp(F2(_colr, o + 4), 0f, 1f) });
        }
        return result.OrderBy(stop => stop.Offset).ToArray();
    }

    private ColorValue GetColor(ushort index, ColorValue foreground) =>
        index == 0xFFFF ? foreground : index < _palette.Length ? _palette[index] : foreground;

    private static ColorValue[] ParsePalette(byte[] data)
    {
        if (data.Length < 12) return [];
        int entries = U16(data, 2), palettes = U16(data, 4), records = U16(data, 6), off = I32(data, 8);
        if (entries <= 0 || palettes <= 0 || 12 + palettes * 2 > data.Length) return [];
        int first = U16(data, 12);
        var result = new ColorValue[entries];
        for (int i = 0; i < entries; i++)
        {
            int idx = first + i, p = off + idx * 4;
            if (idx >= records || p + 4 > data.Length) break;
            result[i] = new((ushort)(data[p + 2] * 257), (ushort)(data[p + 1] * 257),
                (ushort)(data[p] * 257), data[p + 3] / 255f);
        }
        return result;
    }

    private static ushort U16(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
    private static short S16(byte[] b, int o) => unchecked((short)U16(b, o));
    private static int I32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
    private static int U24(byte[] b, int o) => (b[o] << 16) | (b[o + 1] << 8) | b[o + 2];
    private static float F2(byte[] b, int o) => S16(b, o) / 16384f;
    private static float Fixed(byte[] b, int o) => I32(b, o) / 65536f;
}
