using projectFrameCut.Drawing.Text.FontHelper.Reader;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal sealed class GvarTable
{
    private readonly byte[] _data;
    private readonly ushort _axisCount;
    private readonly uint[] _glyphOffsets;
    private readonly uint _glyphVariationDataArrayOffset;
    private readonly TupleVariation[]? _sharedTuples;

    private GvarTable(
        byte[] data,
        ushort axisCount,
        uint[] glyphOffsets,
        uint glyphVariationDataArrayOffset,
        TupleVariation[]? sharedTuples)
    {
        _data = data;
        _axisCount = axisCount;
        _glyphOffsets = glyphOffsets;
        _glyphVariationDataArrayOffset = glyphVariationDataArrayOffset;
        _sharedTuples = sharedTuples;
    }

    public static GvarTable? Load(SfntReader sfnt)
    {
        if (!sfnt.HasTable("gvar"))
            return null;

        byte[] data = sfnt.GetTableDataCopy("gvar");
        int offset = 0;

        ushort majorVersion = BigEndianReader.ReadUInt16(data, ref offset);
        ushort minorVersion = BigEndianReader.ReadUInt16(data, ref offset);

        ushort axisCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort sharedTupleCount = BigEndianReader.ReadUInt16(data, ref offset);

        uint sharedTuplesOffset = BigEndianReader.ReadUInt32(data, ref offset);

        ushort glyphCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort flags = BigEndianReader.ReadUInt16(data, ref offset);
        uint glyphVariationDataArrayOffset = BigEndianReader.ReadUInt32(data, ref offset);

        bool longFormat = (flags & 1) != 0;

        // Parse glyph offset array (glyphCount + 1 entries)
        uint[] glyphOffsets = new uint[glyphCount + 1];
        for (int i = 0; i <= glyphCount; i++)
        {
            if (longFormat)
                glyphOffsets[i] = BigEndianReader.ReadUInt32(data, ref offset);
            else
                glyphOffsets[i] = BigEndianReader.ReadUInt16(data, ref offset);
        }

        // Parse shared tuples
        TupleVariation[]? sharedTuples = null;
        if (sharedTupleCount > 0)
        {
            sharedTuples = new TupleVariation[sharedTupleCount];
            int sharedOff = (int)sharedTuplesOffset;

            for (int i = 0; i < sharedTupleCount; i++)
            {
                float[] peak = new float[axisCount];
                for (int a = 0; a < axisCount; a++)
                    peak[a] = BigEndianReader.ReadF2Dot14(data, ref sharedOff);

                sharedTuples[i] = new TupleVariation(
                    peak, null, [], [], null, 0, -1);
            }
        }

        return new GvarTable(
            data, axisCount, glyphOffsets,
            glyphVariationDataArrayOffset, sharedTuples);
    }

    /// <summary>
    /// Get the variation data for a specific glyph. Returns null if no variation data exists.
    /// </summary>
    public GlyphVariationData? GetGlyphVariation(ushort glyphIndex)
    {
        if (glyphIndex >= _glyphOffsets.Length - 1)
            return null;

        uint startOffset = _glyphOffsets[glyphIndex];
        uint endOffset = _glyphOffsets[glyphIndex + 1];

        if (endOffset <= startOffset)
            return null;

        uint absoluteStart = _glyphVariationDataArrayOffset + startOffset;
        uint dataSize = endOffset - startOffset;

        if (absoluteStart + dataSize > _data.Length)
            return null;

        return ParseGlyphVariationData(
            _data, (int)absoluteStart, (int)dataSize,
            glyphIndex, _axisCount, _sharedTuples);
    }

    /// <summary>
    /// Parse variation data for a single glyph.
    /// </summary>
    private static GlyphVariationData? ParseGlyphVariationData(
        byte[] data, int start, int size,
        ushort glyphIndex, ushort axisCount,
        TupleVariation[]? sharedTuples)
    {
        int offset = start;

        ushort tupleVariationCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort dataOffset = BigEndianReader.ReadUInt16(data, ref offset);

        bool usesSharedTuples = (tupleVariationCount & 0x8000) != 0;
        int tupleCount = tupleVariationCount & 0x7FFF;

        if (tupleCount == 0)
            return null;

        int headersEnd = offset;
        // Each header is at least 2 bytes (variationDataIndex)
        // Skip to dataOffset relative to start of this glyph's data
        int serializedDataStart = start + dataOffset;

        var tuples = new List<TupleVariation>(tupleCount);

        for (int t = 0; t < tupleCount; t++)
        {
            ushort variationDataIndex = BigEndianReader.ReadUInt16(data, ref headersEnd);

            if (usesSharedTuples)
            {
                // Shared tuple reference
                int sharedIndex = variationDataIndex;
                if (sharedTuples == null || sharedIndex < 0 || sharedIndex >= sharedTuples.Length)
                    continue;

                var sharedPeak = sharedTuples[sharedIndex].Peak;
                float[] peak = new float[axisCount];
                Array.Copy(sharedPeak, peak, axisCount);

                // Parse optional intermediate region (not stored in shared tuples)
                var region = ParseIntermediateRegion(data, ref headersEnd, axisCount, variationDataIndex);

                // Parse private points
                ushort[]? privatePoints = null;
                if ((variationDataIndex & 0x4000) != 0)
                    privatePoints = ParsePackedPointNumbers(data, ref headersEnd);

                // The serialized data starts at dataOffset from glyph data start.
                // For shared tuples, all the tuple data (points + deltas) follows
                // after ALL headers. But each tuple's private data is at the
                // serializedDataStart position in sequence.

                // Actually, the serialized data for ALL tuples is concatenated
                // at the serializedDataStart. The order of deltas matches the
                // order of the tuples. We need to parse them in sequence.

                // For now, we store the tuple metadata and will lazily parse deltas.
                tuples.Add(new TupleVariation(
                    peak, region, [], [], privatePoints, 0, sharedIndex));
            }
            else
            {
                // Glyph-specific tuple: variationDataIndex contains packed flags
                ushort flags = variationDataIndex;
                int pointCount = (flags & 0x0FFF) + 1;

                float[] peak = null!;
                if ((flags & 0x1000) != 0) // EMBEDDED_PEAK
                {
                    peak = new float[axisCount];
                    for (int a = 0; a < axisCount; a++)
                        peak[a] = BigEndianReader.ReadF2Dot14(data, ref headersEnd);
                }
                else
                {
                    // No peak defined - this shouldn't happen for glyph-specific tuples
                    peak = new float[axisCount];
                }

                var region = ParseIntermediateRegion(data, ref headersEnd, axisCount, flags);

                ushort[]? privatePoints = null;
                if ((flags & 0x4000) != 0) // PRIVATE_POINTS
                    privatePoints = ParsePackedPointNumbers(data, ref headersEnd);

                tuples.Add(new TupleVariation(
                    peak, region, [], [], privatePoints, pointCount, -1));
            }
        }

        // Now parse the serialized delta data for all tuples
        // The serialized data starts at serializedDataStart
        // Each tuple has its point data and delta data in sequence
        int serializedOffset = serializedDataStart;

        for (int t = 0; t < tuples.Count; t++)
        {
            var tuple = tuples[t];

            // Parse point numbers (if private points, they were already parsed;
            // otherwise points are 0..pointCount-1)
            ushort[]? pointIndices = tuple.ExplicitPointIndices;
            int pointCount;

            if (pointIndices != null)
            {
                // Private points already parsed from header
                pointCount = pointIndices.Length;
            }
            else
            {
                // No private points — read packed point numbers from serialized data
                pointIndices = ParsePackedPointNumbers(data, ref serializedOffset);
                pointCount = pointIndices?.Length ?? tuple.PointCount;
            }

            // If no explicit points, this tuple has no deltas to apply
            if (pointIndices == null || pointIndices.Length == 0)
            {
                tuples[t] = new TupleVariation(
                    tuple.Peak, tuple.Region,
                    Array.Empty<short>(), Array.Empty<short>(),
                    null, tuple.PointCount, tuple.SharedTupleIndex);
                continue;
            }

            // Parse delta X
            short[] deltaX = UnpackDeltas(data, ref serializedOffset, pointCount);

            // Parse delta Y
            short[] deltaY = UnpackDeltas(data, ref serializedOffset, pointCount);

            tuples[t] = new TupleVariation(
                tuple.Peak, tuple.Region,
                deltaX, deltaY, pointIndices,
                tuple.PointCount, tuple.SharedTupleIndex);
        }

        return new GlyphVariationData(glyphIndex, tuples.ToArray());
    }

    /// <summary>
    /// Parse intermediate region from header (if INTERMEDIATE_REGION flag is set).
    /// </summary>
    private static (float start, float end)[]? ParseIntermediateRegion(
        byte[] data, ref int offset, ushort axisCount, ushort flags)
    {
        if ((flags & 0x2000) == 0) // INTERMEDIATE_REGION
            return null;

        var region = new (float start, float end)[axisCount];
        for (int a = 0; a < axisCount; a++)
            region[a].start = BigEndianReader.ReadF2Dot14(data, ref offset);

        // Same offset reads start values; we need to read end values after all starts
        // No — actually each axis has start and end adjacent
        // Let me re-read: the spec says "For each axis, for each tuple, read start and end"
        // Actually, the format is all start values first, then all end values
        // Let me correct this:

        // Actually wait, the spec says:
        // "For each axis in the font, in order of fvar table axis indices:
        //  - Start value: F2Dot14
        //  - End value: F2Dot14"
        // So start and end for axis 0, then start and end for axis 1, etc.
        // But wait, the offset was already advanced for starts...

        // Let me re-examine. Actually the reference code shows the format is:
        // All start values (axisCount F2Dot14), then all end values (axisCount F2Dot14).

        // So we need to reset: the region starts have been read,
        // and now we read the end values.
        // But we need the starts to compute, so let me restructure.

        // Actually the simplest approach: read all starts into array, then all ends.
        // Let me redo. Treating the format as:
        //   for each axis: read start[N]
        //   for each axis: read end[N]

        // Hmm, but I already consumed the offset. Let me rethink.

        // Actually, looking at the OpenType spec more carefully, the format is:
        //   for each axis: start_coord = ReadF2Dot14
        //   for each axis: end_coord = ReadF2Dot14
        // So starts come first, then ends.

        // But in my code above, I already read all the starts. The offset
        // now points to where the ends begin. So let me read the ends:

        for (int a = 0; a < axisCount; a++)
            region[a].end = BigEndianReader.ReadF2Dot14(data, ref offset);

        return region;
    }

    /// <summary>
    /// Parse packed point numbers from the serialized data.
    /// Returns null if no explicit points (all points are implicit).
    /// </summary>
    private static ushort[]? ParsePackedPointNumbers(byte[] data, ref int offset)
    {
        if (offset >= data.Length)
            return null;

        byte controlByte = data[offset++];

        int pointCount;
        int runCount;

        if ((controlByte & 0x80) == 0)
        {
            // Long format: 16-bit point count, multi-run
            runCount = ((controlByte >> 4) & 0x07) + 1;
            pointCount = ((controlByte & 0x0F) << 8) + data[offset++];
        }
        else
        {
            // Short format: point count from low 6 bits, 1-2 runs
            runCount = ((controlByte >> 6) & 0x01) + 1;
            pointCount = (controlByte & 0x3F) + 1;
        }

        if (pointCount == 0)
            return null;

        var points = new List<ushort>(pointCount);
        int lastPoint = -1;

        for (int r = 0; r < runCount && points.Count < pointCount; r++)
        {
            if (offset >= data.Length)
                break;

            byte runControl = data[offset++];

            if ((runControl & 0x80) != 0)
            {
                // Consecutive points: runCount = low 7 bits + 1
                int runLen = (runControl & 0x7F) + 1;
                for (int i = 0; i < runLen && points.Count < pointCount; i++)
                    points.Add((ushort)(++lastPoint));
            }
            else
            {
                // Explicit points
                int runLen = runControl & 0x7F;
                for (int i = 0; i < runLen && points.Count < pointCount; i++)
                {
                    ushort point = BigEndianReader.ReadUInt16(data, ref offset);
                    points.Add(point);
                    lastPoint = point;
                }
            }
        }

        return points.ToArray();
    }

    /// <summary>
    /// Unpack a run of packed delta values from the serialized data.
    /// Returns an array of <paramref name="count"/> delta values.
    /// </summary>
    private static short[] UnpackDeltas(byte[] data, ref int offset, int count)
    {
        var deltas = new short[count];
        int di = 0;

        while (di < count && offset < data.Length)
        {
            byte ctrl = data[offset++];

            if ((ctrl & 0x80) == 0)
            {
                // TWO_DELTAS: two 4-bit signed values
                sbyte d1 = SignExtendNibble((byte)((ctrl >> 4) & 0xF));
                sbyte d2 = SignExtendNibble((byte)(ctrl & 0xF));

                if (di < count) deltas[di++] = d1;
                if (di < count) deltas[di++] = d2;
            }
            else if ((ctrl & 0x40) == 0)
            {
                // ZEROES: (ctrl & 0x3F) + 1 zero deltas
                int zeroCount = (ctrl & 0x3F) + 1;
                for (int z = 0; z < zeroCount && di < count; z++)
                    deltas[di++] = 0;
            }
            else if ((ctrl & 0x20) == 0)
            {
                // SHORT_WORDS: (ctrl & 0x1F) + 1 signed byte values
                int wordCount = (ctrl & 0x1F) + 1;
                for (int w = 0; w < wordCount && di < count; w++)
                    deltas[di++] = (sbyte)data[offset++];
            }
            else
            {
                // WORDS: (ctrl & 0x1F) + 1 signed int16 values
                int wordCount = (ctrl & 0x1F) + 1;
                for (int w = 0; w < wordCount && di < count; w++)
                {
                    short val = BigEndianReader.ReadInt16(data, ref offset);
                    deltas[di++] = val;
                }
            }
        }

        return deltas;
    }

    /// <summary>
    /// Sign-extend a 4-bit value to a signed byte.
    /// </summary>
    private static sbyte SignExtendNibble(byte nibble)
    {
        return (sbyte)((nibble & 0x08) != 0
            ? (nibble | 0xF0)   // negative
            : nibble);          // positive
    }

    /// <summary>
    /// Compute the scalar for a tuple variation at given axis coordinates.
    /// Returns 0 if the coordinates are outside the variation region.
    /// </summary>
    public static float ComputeScalar(TupleVariation tuple, float[] currentCoords)
    {
        float scalar = 1.0f;
        int axisCount = currentCoords.Length;

        for (int i = 0; i < axisCount; i++)
        {
            float peak = tuple.Peak[i];
            if (peak == 0)
                continue; // No variation contribution on this axis

            float coord = currentCoords[i];

            float regionStart, regionEnd;
            if (tuple.Region != null && i < tuple.Region.Length)
            {
                regionStart = tuple.Region[i].start;
                regionEnd = tuple.Region[i].end;
            }
            else
            {
                // No explicit region: use peak as a single point
                if (coord != peak)
                    return 0; // Outside the single-point region
                continue;
            }

            // Check if coord is within the region
            if (coord <= regionStart || coord >= regionEnd)
                return 0;

            if (coord == peak)
                continue; // Full contribution from this axis

            // Interpolate within the region
            if (coord < peak)
                scalar *= (coord - regionStart) / (peak - regionStart);
            else
                scalar *= (regionEnd - coord) / (regionEnd - peak);
        }

        return scalar;
    }

    /// <summary>
    /// Apply IUP (Interpolation of Unaffected Points) to fill in deltas
    /// for points that don't have explicit deltas, per contour boundaries.
    /// </summary>
    /// <param name="contours">The base glyph contours (original unvaried points).</param>
    /// <param name="explicitDeltaX">Delta X for points that have explicit deltas (0 for others).</param>
    /// <param name="explicitDeltaY">Delta Y for points that have explicit deltas (0 for others).</param>
    /// <param name="explicitIndices">Indices of points with explicit deltas.</param>
    /// <param name="totalPoints">Total points in the glyph including phantom points.</param>
    /// <param name="fullDeltaX">Output: interpolated delta X for all points.</param>
    /// <param name="fullDeltaY">Output: interpolated delta Y for all points.</param>
    public static void ApplyIup(
        GlyphPoint[][] contours,
        short[] explicitDeltaX,
        short[] explicitDeltaY,
        ushort[] explicitIndices,
        int totalPoints,
        out short[] fullDeltaX,
        out short[] fullDeltaY)
    {
        fullDeltaX = new short[totalPoints];
        fullDeltaY = new short[totalPoints];

        // Build a set of explicit indices for O(1) lookup
        var explicitSet = new HashSet<ushort>(explicitIndices);

        // Copy explicit deltas
        foreach (ushort idx in explicitIndices)
        {
            if (idx < totalPoints)
            {
                fullDeltaX[idx] = explicitDeltaX[idx];
                fullDeltaY[idx] = explicitDeltaY[idx];
            }
        }

        // IUP per contour
        int pointIndex = 0;
        for (int c = 0; c < contours.Length; c++)
        {
            var contour = contours[c];
            int contourLen = contour.Length;

            // Collect explicit indices within this contour
            var expIndices = new List<int>();
            var expDx = new List<short>();
            var expDy = new List<short>();

            for (int j = 0; j < contourLen; j++)
            {
                int globalIdx = pointIndex + j;
                if (explicitSet.Contains((ushort)globalIdx))
                {
                    expIndices.Add(j);
                    expDx.Add(fullDeltaX[globalIdx]);
                    expDy.Add(fullDeltaY[globalIdx]);
                }
            }

            if (expIndices.Count == 0)
            {
                // No explicit points in this contour — all deltas stay 0
            }
            else if (expIndices.Count == 1)
            {
                // Only one explicit point — all points inherit its delta
                int soleIdx = pointIndex + expIndices[0];
                short dx = fullDeltaX[soleIdx];
                short dy = fullDeltaY[soleIdx];
                for (int j = 0; j < contourLen; j++)
                {
                    fullDeltaX[pointIndex + j] = dx;
                    fullDeltaY[pointIndex + j] = dy;
                }
            }
            else
            {
                // IUP: interpolate non-explicit points between explicit ones
                for (int j = 0; j < contourLen; j++)
                {
                    if (explicitSet.Contains((ushort)(pointIndex + j)))
                        continue; // Already set

                    // Find nearest explicit points on each side (with wraparound)
                    int prevIdx = -1, prevPos = -1;
                    int nextIdx = -1, nextPos = -1;

                    // Search backward
                    for (int k = j - 1; k >= 0; k--)
                    {
                        if (explicitSet.Contains((ushort)(pointIndex + k)))
                        {
                            prevPos = k;
                            prevIdx = expIndices.IndexOf(k);
                            break;
                        }
                    }
                    if (prevPos == -1)
                    {
                        // Wrap around to the end
                        for (int k = contourLen - 1; k > j; k--)
                        {
                            if (explicitSet.Contains((ushort)(pointIndex + k)))
                            {
                                prevPos = k;
                                prevIdx = expIndices.IndexOf(k);
                                break;
                            }
                        }
                    }

                    // Search forward
                    for (int k = j + 1; k < contourLen; k++)
                    {
                        if (explicitSet.Contains((ushort)(pointIndex + k)))
                        {
                            nextPos = k;
                            nextIdx = expIndices.IndexOf(k);
                            break;
                        }
                    }
                    if (nextPos == -1)
                    {
                        // Wrap around to the start
                        for (int k = 0; k < j; k++)
                        {
                            if (explicitSet.Contains((ushort)(pointIndex + k)))
                            {
                                nextPos = k;
                                nextIdx = expIndices.IndexOf(k);
                                break;
                            }
                        }
                    }

                    if (prevPos == -1 || nextPos == -1 || prevIdx == -1 || nextIdx == -1)
                        continue;

                    // Interpolate using original coordinates
                    var ptPrev = contour[prevPos];
                    var ptNext = contour[nextPos];
                    var ptCurr = contour[j];

                    // X interpolation using original X coordinates
                    if (ptPrev.X != ptNext.X)
                    {
                        float tX = (float)(ptCurr.X - ptPrev.X) / (ptNext.X - ptPrev.X);
                        fullDeltaX[pointIndex + j] = (short)Math.Round(
                            expDx[prevIdx] + tX * (expDx[nextIdx] - expDx[prevIdx]));
                    }
                    else
                    {
                        fullDeltaX[pointIndex + j] = (short)((expDx[prevIdx] + expDx[nextIdx]) / 2);
                    }

                    // Y interpolation using original Y coordinates
                    if (ptPrev.Y != ptNext.Y)
                    {
                        float tY = (float)(ptCurr.Y - ptPrev.Y) / (ptNext.Y - ptPrev.Y);
                        fullDeltaY[pointIndex + j] = (short)Math.Round(
                            expDy[prevIdx] + tY * (expDy[nextIdx] - expDy[prevIdx]));
                    }
                    else
                    {
                        fullDeltaY[pointIndex + j] = (short)((expDy[prevIdx] + expDy[nextIdx]) / 2);
                    }
                }
            }

            pointIndex += contourLen;
        }

        // Phantom points: just copy any explicit deltas (already done via explicitIndices loop above)
    }

    /// <summary>
    /// Apply variations to a base glyph and return a new glyph with varied outlines.
    /// Returns the base glyph unchanged if there are no variations or the font isn't variable.
    /// </summary>
    public Glyph? ApplyVariations(
        Glyph? baseGlyph,
        ushort glyphIndex,
        float[] currentCoords,
        GlyphPoint[][] baseContours,
        int totalBasePoints)
    {
        if (baseGlyph == null || baseGlyph.IsEmpty)
            return baseGlyph;

        GlyphVariationData? varData = GetGlyphVariation(glyphIndex);
        if (varData == null || varData.Tuples.Length == 0)
            return baseGlyph;

        // Check if any axis has non-default value
        // (This should be checked earlier by caller, but double-check here)
        bool hasActiveVariation = false;
        for (int i = 0; i < currentCoords.Length; i++)
        {
            if (Math.Abs(currentCoords[i]) > 0.001f) // Non-default normalized value
            {
                hasActiveVariation = true;
                break;
            }
        }
        if (!hasActiveVariation)
            return baseGlyph;

        int phantomCount = 4; // LSB, RSB, TSB, BSB
        int totalPoints = totalBasePoints + phantomCount;

        // Accumulate deltas as floats for precision, then round at the end
        float[] accumDeltaX = new float[totalPoints];
        float[] accumDeltaY = new float[totalPoints];

        foreach (var tuple in varData.Tuples)
        {
            float scalar = ComputeScalar(tuple, currentCoords);
            if (Math.Abs(scalar) < 0.0001f)
                continue;

            // Build explicit delta arrays sized to totalPoints
            short[] expDeltaX = new short[totalPoints];
            short[] expDeltaY = new short[totalPoints];
            ushort[] expIndices;

            if (tuple.ExplicitPointIndices != null && tuple.ExplicitPointIndices.Length > 0)
            {
                expIndices = tuple.ExplicitPointIndices;
                for (int i = 0; i < expIndices.Length && i < tuple.DeltaX.Length; i++)
                {
                    ushort idx = expIndices[i];
                    if (idx < totalPoints)
                    {
                        expDeltaX[idx] = tuple.DeltaX[i];
                        expDeltaY[idx] = tuple.DeltaY.Length > i ? tuple.DeltaY[i] : (short)0;
                    }
                }
            }
            else
            {
                // All points 0..pointCount-1 have explicit deltas
                expIndices = new ushort[tuple.DeltaX.Length];
                for (int i = 0; i < tuple.DeltaX.Length; i++)
                {
                    expIndices[i] = (ushort)i;
                    expDeltaX[i] = tuple.DeltaX[i];
                    expDeltaY[i] = tuple.DeltaY.Length > i ? tuple.DeltaY[i] : (short)0;
                }
            }

            // Apply IUP
            ApplyIup(
                baseContours,
                expDeltaX, expDeltaY, expIndices, totalPoints,
                out short[] fullDeltaX, out short[] fullDeltaY);

            // Accumulate scaled deltas
            for (int i = 0; i < totalPoints; i++)
            {
                accumDeltaX[i] += fullDeltaX[i] * scalar;
                accumDeltaY[i] += fullDeltaY[i] * scalar;
            }
        }

        // Build new glyph contours with applied deltas
        var newContours = new GlyphPoint[baseContours.Length][];
        int pointIdx = 0;

        for (int c = 0; c < baseContours.Length; c++)
        {
            var contour = baseContours[c];
            var newContour = new GlyphPoint[contour.Length];

            for (int p = 0; p < contour.Length; p++)
            {
                short newX = (short)Math.Round(contour[p].X + accumDeltaX[pointIdx]);
                short newY = (short)Math.Round(contour[p].Y + accumDeltaY[pointIdx]);
                newContour[p] = new GlyphPoint(newX, newY, contour[p].OnCurve);
                pointIdx++;
            }

            newContours[c] = newContour;
        }

        // Compute new bounding box (y-flipped already)
        short xMin = short.MaxValue, yMin = short.MaxValue;
        short xMax = short.MinValue, yMax = short.MinValue;
        bool hasAnyPoints = false;

        foreach (var contour in newContours)
        {
            foreach (var pt in contour)
            {
                hasAnyPoints = true;
                if (pt.X < xMin) xMin = pt.X;
                if (pt.X > xMax) xMax = pt.X;
                if (pt.Y < yMin) yMin = pt.Y;
                if (pt.Y > yMax) yMax = pt.Y;
            }
        }

        if (!hasAnyPoints)
        {
            xMin = yMin = xMax = yMax = 0;
        }

        return new Glyph(baseGlyph.OutlineType, newContours, xMin, yMin, xMax, yMax);
    }
}
