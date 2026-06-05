using projectFrameCut.Drawing.Text.FontHelper.Table;
using projectFrameCut.Drawing.Text.FontHelper.Reader;

namespace projectFrameCut.Drawing.Text.FontHelper;

internal sealed class VariationEngine
{
    private readonly FontFace _fontFace;
    private readonly SfntReader _sfnt;
    private readonly FvarData _fvar;
    private readonly GvarTable? _gvar;
    private readonly float[] _currentCoords;
    private readonly VariationAxis[] _axes;
    private readonly NamedInstance[] _instances;

    // Simple cache: (glyphIndex, coordHash) -> Glyph
    private readonly Dictionary<(ushort glyphIndex, int coordHash), Glyph?> _cache = new();
    private const int MaxCacheSize = 256;
    private bool _cacheValid;

    public VariationEngine(FontFace fontFace, SfntReader sfnt)
    {
        _fontFace = fontFace;
        _sfnt = sfnt;

        // Parse fvar table
        var fvarData = sfnt.GetTableData("fvar");
        _fvar = FvarTable.Parse(fvarData);

        _axes = _fvar.Axes;

        // Parse gvar table
        _gvar = GvarTable.Load(sfnt);

        // Initialize current coordinates to axis default values (normalized to 0)
        _currentCoords = new float[_axes.Length];

        // Resolve instance names from name table
        _instances = _fvar.InitialInstances;
        if (_instances.Length > 0 && sfnt.TryGetTableEntry("name", out var fontData, out var nameOffset, out var nameLength))
        {
            var nameData = NameTable.Parse(fontData, (int)nameOffset, (int)nameLength);
            _instances = FvarTable.ResolveInstanceNames(fvarData, nameData);
        }
    }

    public bool HasActiveVariation
    {
        get
        {
            for (int i = 0; i < _currentCoords.Length; i++)
            {
                if (Math.Abs(_currentCoords[i]) > 0.0001f)
                    return true;
            }
            return false;
        }
    }

    public int AxisCount => _axes.Length;
    public bool IsVariable => _axes.Length > 0 && _gvar != null;
    public IReadOnlyList<VariationAxis> Axes => _axes;
    public IReadOnlyList<NamedInstance> Instances => _instances;
    public float[] CurrentCoords => _currentCoords;

    public bool TrySetAxis(string axisTag, float value)
    {
        for (int i = 0; i < _axes.Length; i++)
        {
            if (string.Equals(_axes[i].Tag, axisTag, StringComparison.OrdinalIgnoreCase))
            {
                // Clamp to axis range
                float clamped = Math.Clamp(value, _axes[i].MinValue, _axes[i].MaxValue);
                _currentCoords[i] = clamped;
                _cacheValid = false;
                return true;
            }
        }
        return false;
    }

    public void SetAxes(IReadOnlyDictionary<string, float> axes)
    {
        // Reset all axes to default (normalized 0)
        Array.Clear(_currentCoords);

        if (axes == null || axes.Count == 0)
        {
            _cacheValid = false;
            return;
        }

        bool changed = false;
        foreach (var (tag, value) in axes)
        {
            for (int i = 0; i < _axes.Length; i++)
            {
                if (string.Equals(_axes[i].Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    float clamped = Math.Clamp(value, _axes[i].MinValue, _axes[i].MaxValue);
                    // Normalize to -1..1 range: (value - default) / range
                    float range = Math.Max(_axes[i].MaxValue - _axes[i].MinValue, 0.001f);
                    float normalized = (clamped - _axes[i].DefaultValue) / range;

                    _currentCoords[i] = normalized;
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
            _cacheValid = false;
    }

    public float GetAxis(string axisTag)
    {
        for (int i = 0; i < _axes.Length; i++)
        {
            if (string.Equals(_axes[i].Tag, axisTag, StringComparison.OrdinalIgnoreCase))
            {
                // Denormalize: default + coord * range
                float range = Math.Max(_axes[i].MaxValue - _axes[i].MinValue, 0.001f);
                return _axes[i].DefaultValue + _currentCoords[i] * range;
            }
        }
        return 0f;
    }

    public Glyph? GetVariedGlyph(ushort glyphIndex)
    {
        if (!HasActiveVariation || _gvar == null)
            return _fontFace.GetGlyph(glyphIndex);

        int coordHash = ComputeCoordHash();

        // Check cache
        if (_cacheValid && _cache.TryGetValue((glyphIndex, coordHash), out var cached))
            return cached;

        // For composite glyphs, components are varied via the callback during
        // recursive decomposition. For simple glyphs, the callback applies
        // gvar variation after parsing.
        GlyfTable.VariationApplier applier = (index, parsedGlyph) =>
        {
            if (parsedGlyph == null || parsedGlyph.IsEmpty)
                return parsedGlyph;

            return _gvar.ApplyVariations(
                parsedGlyph, index, _currentCoords,
                parsedGlyph.Contours, GetTotalBasePoints(parsedGlyph.Contours));
        };

        // Get the glyph with variation applied to all components
        Glyph? variedGlyph = _fontFace.GetGlyphWithVariation(glyphIndex, applier);

        // Compute advance width from phantom points
        if (variedGlyph != null && !variedGlyph.IsEmpty)
        {
            ushort variedAdvanceWidth = ComputeVariedAdvanceWidth(glyphIndex, variedGlyph);
            variedGlyph.AdvanceWidth = variedAdvanceWidth;
        }

        // Cache result
        if (_cache.Count >= MaxCacheSize)
            _cache.Clear();
        _cache[(glyphIndex, coordHash)] = variedGlyph;
        _cacheValid = true;

        return variedGlyph;
    }

    public ushort GetVariedAdvanceWidth(ushort glyphIndex)
    {
        if (!HasActiveVariation)
            return _fontFace.GetAdvanceWidth(glyphIndex);

        // Get base advance width
        ushort baseAdvanceWidth = _fontFace.GetAdvanceWidth(glyphIndex);

        // Compute delta from gvar phantom points
        Glyph? baseGlyph = _fontFace.GetGlyph(glyphIndex);
        if (baseGlyph == null || baseGlyph.IsEmpty)
            return baseAdvanceWidth;

        return ComputeVariedAdvanceWidth(glyphIndex, baseGlyph);
    }

    private ushort ComputeVariedAdvanceWidth(ushort glyphIndex, Glyph? baseGlyph)
    {
        if (baseGlyph == null || baseGlyph.IsEmpty)
            return _fontFace.GetAdvanceWidth(glyphIndex);

        // Get advance width from phantom points via gvar
        // Phantom point 0: left side bearing position
        // Phantom point 1: right side bearing (advance width) position
        // advance width delta = phantom[1].X - phantom[0].X
        // But we don't have explicit phantom points, so let's use a simpler approach:

        // For now, fall back to base advance width. The gvar variation application
        // through ApplyVariations computes a new bounding box, and we use that.
        // The better approach would be to maintain phantom points separately.
        // But since the existing Glyph struct doesn't track phantom points,
        // let's compute using the gvar delta accumulation approach:

        GlyphVariationData? varData = _gvar?.GetGlyphVariation(glyphIndex);
        if (varData == null || varData.Tuples.Length == 0)
            return _fontFace.GetAdvanceWidth(glyphIndex);

        int totalBasePoints = GetTotalBasePoints(baseGlyph.Contours);
        int phantomCount = 4;
        int totalPoints = totalBasePoints + phantomCount;

        float phantomDeltaAW = 0; // Accumulated advance width delta from phantom points

        foreach (var tuple in varData.Tuples)
        {
            float scalar = GvarTable.ComputeScalar(tuple, _currentCoords);
            if (Math.Abs(scalar) < 0.0001f)
                continue;

            // Find phantom point deltas (the last 4 points)
            // Tuple deltas are stored for explicit point indices only
            ushort[]? pointIndices = tuple.ExplicitPointIndices;
            if (pointIndices == null)
                continue;

            short phantom0dx = 0, phantom0dy = 0;
            short phantom1dx = 0, phantom1dy = 0;

            for (int i = 0; i < pointIndices.Length; i++)
            {
                ushort idx = pointIndices[i];
                if (idx == totalBasePoints) // Phantom point 0 (LSB)
                {
                    phantom0dx = tuple.DeltaX.Length > i ? tuple.DeltaX[i] : (short)0;
                    phantom0dy = tuple.DeltaY.Length > i ? tuple.DeltaY[i] : (short)0;
                }
                else if (idx == totalBasePoints + 1) // Phantom point 1 (advance width)
                {
                    phantom1dx = tuple.DeltaX.Length > i ? tuple.DeltaX[i] : (short)0;
                    phantom1dy = tuple.DeltaY.Length > i ? tuple.DeltaY[i] : (short)0;
                }
            }

            // Advance width delta for this tuple: (phantom1.X - phantom0.X) * scalar
            phantomDeltaAW += (phantom1dx - phantom0dx) * scalar;
        }

        int delta = (int)Math.Round(phantomDeltaAW);
        return (ushort)Math.Clamp(_fontFace.GetAdvanceWidth(glyphIndex) + delta, 0, ushort.MaxValue);
    }

    private int ComputeCoordHash()
    {
        int hash = 17;
        foreach (float coord in _currentCoords)
            hash = hash * 31 + BitConverter.SingleToInt32Bits(coord);
        return hash;
    }

    private static int GetTotalBasePoints(GlyphPoint[][] contours)
    {
        int total = 0;
        foreach (var contour in contours)
            total += contour.Length;
        return total;
    }
}
