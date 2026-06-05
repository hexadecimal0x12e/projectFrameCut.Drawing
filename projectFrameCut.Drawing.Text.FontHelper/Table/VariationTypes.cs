namespace projectFrameCut.Drawing.Text.FontHelper.Table;

/// <summary>
/// A single variation axis declared in the 'fvar' table.
/// </summary>
public readonly record struct VariationAxis(
    string Tag,
    float MinValue,
    float DefaultValue,
    float MaxValue,
    int AxisValueNameId);

/// <summary>
/// A named instance (predefined coordinate set) declared in the 'fvar' table.
/// </summary>
public readonly record struct NamedInstance(
    string SubfamilyName,
    IReadOnlyList<float> Coordinates);

/// <summary>
/// A single tuple variation region used by 'gvar' and 'HVAR' tables.
/// Peak values per axis, plus optional intermediate region start/end.
/// </summary>
internal sealed class TupleVariation
{
    /// <summary>Normalized peak coordinates for each axis (-1..1 in F2Dot14, stored as float).</summary>
    public float[] Peak { get; }

    /// <summary>Optional intermediate region: (start, end) per axis. Null means axis is not in the region.</summary>
    public (float start, float end)[]? Region { get; }

    /// <summary>Per-point X deltas (after de-duffing/packing).</summary>
    public short[] DeltaX { get; }

    /// <summary>Per-point Y deltas.</summary>
    public short[] DeltaY { get; }

    /// <summary>Indices of points that have explicit deltas.</summary>
    public ushort[]? ExplicitPointIndices { get; }

    /// <summary>Total point count this variation applies to (excluding phantom points).</summary>
    public int PointCount { get; }

    /// <summary>Index into shared tuples, or -1 if glyph-specific.</summary>
    public int SharedTupleIndex { get; } = -1;

    public TupleVariation(
        float[] peak,
        (float start, float end)[]? region,
        short[] deltaX,
        short[] deltaY,
        ushort[]? explicitPointIndices,
        int pointCount,
        int sharedTupleIndex = -1)
    {
        Peak = peak;
        Region = region;
        DeltaX = deltaX;
        DeltaY = deltaY;
        ExplicitPointIndices = explicitPointIndices;
        PointCount = pointCount;
        SharedTupleIndex = sharedTupleIndex;
    }
}

/// <summary>
/// Parsed variation data for a single glyph from the 'gvar' table.
/// </summary>
internal sealed class GlyphVariationData
{
    public ushort GlyphIndex { get; }
    public TupleVariation[] Tuples { get; }

    public GlyphVariationData(ushort glyphIndex, TupleVariation[] tuples)
    {
        GlyphIndex = glyphIndex;
        Tuples = tuples;
    }
}
