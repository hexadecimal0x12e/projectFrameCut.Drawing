using projectFrameCut.Drawing.Text.FontHelper.Reader;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

/// <summary>
/// Represents a single contour point within a glyph.
/// Coordinates are in image space (Y-down, flipped from TTF's Y-up).
/// </summary>
public readonly struct GlyphPoint
{
    public short X { get; }
    public short Y { get; }
    public bool OnCurve { get; }

    public GlyphPoint(short x, short y, bool onCurve)
    {
        X = x;
        Y = y;
        OnCurve = onCurve;
    }
}

/// <summary>
/// Represents a parsed glyph with its contours, bounding box, and metrics.
/// </summary>
public sealed class Glyph
{
    /// <summary>0 = Simple outline, 1 = Compound, -1 = Empty (no contours).</summary>
    public int OutlineType { get; }
    public GlyphPoint[][] Contours { get; }
    public short XMin { get; }
    public short YMin { get; }
    public short XMax { get; }
    public short YMax { get; }
    public ushort AdvanceWidth { get; internal set; }
    public short LeftSideBearing { get; internal set; }
    public bool IsEmpty => OutlineType < 0;

    internal Glyph(
        int outlineType,
        GlyphPoint[][] contours,
        short xMin, short yMin, short xMax, short yMax)
    {
        OutlineType = outlineType;
        Contours = contours;
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
    }

    internal static readonly Glyph Empty = new(-1, [], 0, 0, 0, 0);
}

internal static class GlyfTable
{
    /// <summary>
    /// Callback for applying variation data to a glyph. Used for variable font support.
    /// Called for each simple glyph (including component glyphs of composites).
    /// </summary>
    internal delegate Glyph? VariationApplier(ushort glyphIndex, Glyph? parsedGlyph);

    public static Glyph? ParseGlyph(
        ReadOnlySpan<byte> glyfTableData,
        LocaData loca,
        ushort glyphIndex,
        SfntReader sfnt,
        MaxpData maxp,
        int recursionDepth = 0,
        VariationApplier? onGlyphParsed = null)
    {
        uint offset = loca.GetGlyphOffset(glyphIndex);
        uint length = loca.GetGlyphLength(glyphIndex);

        if (length == 0)
            return null;

        if (offset + length > glyfTableData.Length)
            return null;

        ReadOnlySpan<byte> glyphData = glyfTableData.Slice((int)offset, (int)length);

        if (glyphData.Length < 10)
            return Glyph.Empty;

        int headerOffset = 0;
        short numberOfContours = BigEndianReader.ReadInt16(glyphData, ref headerOffset);

        if (numberOfContours == 0)
            return Glyph.Empty;

        short xMin = BigEndianReader.ReadInt16(glyphData, ref headerOffset);
        short yMin = BigEndianReader.ReadInt16(glyphData, ref headerOffset);
        short xMax = BigEndianReader.ReadInt16(glyphData, ref headerOffset);
        short yMax = BigEndianReader.ReadInt16(glyphData, ref headerOffset);

        // Y-flip the bounding box
        short flippedYMin = (short)(-yMax);
        short flippedYMax = (short)(-yMin);

        Glyph? result;

        if (numberOfContours > 0)
        {
            result = ParseSimpleGlyph(glyphData, numberOfContours,
                xMin, flippedYMin, xMax, flippedYMax);
        }
        else
        {
            result = ParseCompoundGlyph(glyphData, sfnt, loca, maxp, recursionDepth,
                xMin, flippedYMin, xMax, flippedYMax, onGlyphParsed);
        }

        // Apply variation if callback provided (for composite glyphs,
        // each component is varied individually during recursion;
        // for simple glyphs, the variation is applied here).
        if (onGlyphParsed != null && result != null && !result.IsEmpty)
        {
            result = onGlyphParsed(glyphIndex, result);
        }

        return result;
    }

    private static Glyph ParseSimpleGlyph(
        ReadOnlySpan<byte> glyphData, short contourCount,
        short xMin, short yMin, short xMax, short yMax)
    {
        int offset = 10; // After numberOfContours + bbox

        // Read endPtsOfContours
        var endPts = new ushort[contourCount];
        for (int i = 0; i < contourCount; i++)
            endPts[i] = BigEndianReader.ReadUInt16(glyphData, ref offset);

        int pointCount = endPts[^1] + 1;

        // Read instruction length and skip instructions
        ushort instructionLength = BigEndianReader.ReadUInt16(glyphData, ref offset);
        offset += instructionLength;

        // Read flags
        var flags = new byte[pointCount];
        int flagIndex = 0;
        while (flagIndex < pointCount)
        {
            byte flag = glyphData[offset++];
            flags[flagIndex++] = flag;

            if ((flag & 8) != 0) // REPEAT flag
            {
                byte repeatCount = glyphData[offset++];
                for (int r = 0; r < repeatCount && flagIndex < pointCount; r++)
                    flags[flagIndex++] = flag;
            }
        }

        // Read X coordinates
        var xCoords = new short[pointCount];
        int prevX = 0;
        for (int i = 0; i < pointCount; i++)
        {
            byte flag = flags[i];
            int delta;

            if ((flag & 2) != 0) // X_SHORT
            {
                byte shortVal = glyphData[offset++];
                delta = (flag & 16) != 0 ? shortVal : -shortVal; // X_POSITIVE
            }
            else if ((flag & 16) == 0) // Not X_SAME
            {
                delta = BigEndianReader.ReadInt16(glyphData, ref offset);
            }
            else
            {
                delta = 0; // Same X as previous
            }

            prevX += delta;
            xCoords[i] = (short)prevX;
        }

        // Read Y coordinates
        var yCoords = new short[pointCount];
        int prevY = 0;
        for (int i = 0; i < pointCount; i++)
        {
            byte flag = flags[i];
            int delta;

            if ((flag & 4) != 0) // Y_SHORT
            {
                byte shortVal = glyphData[offset++];
                delta = (flag & 32) != 0 ? shortVal : -shortVal; // Y_POSITIVE
            }
            else if ((flag & 32) == 0) // Not Y_SAME
            {
                delta = BigEndianReader.ReadInt16(glyphData, ref offset);
            }
            else
            {
                delta = 0; // Same Y as previous
            }

            prevY += delta;
            yCoords[i] = (short)prevY;
        }

        // Build contours: group points by endPts boundaries and flip Y
        var contours = new GlyphPoint[contourCount][];
        int currentPoint = 0;

        for (int c = 0; c < contourCount; c++)
        {
            int contourSize = endPts[c] - currentPoint + 1;
            var points = new GlyphPoint[contourSize];

            for (int p = 0; p < contourSize; p++)
            {
                int idx = currentPoint + p;
                points[p] = new GlyphPoint(
                    xCoords[idx],
                    (short)(-yCoords[idx]), // Y-flip
                    (flags[idx] & 1) != 0   // ON_CURVE
                );
            }

            contours[c] = points;
            currentPoint = endPts[c] + 1;
        }

        return new Glyph(0, contours, xMin, yMin, xMax, yMax);
    }

    private static Glyph ParseCompoundGlyph(
        ReadOnlySpan<byte> glyphData,
        SfntReader sfnt,
        LocaData loca,
        MaxpData maxp,
        int recursionDepth,
        short xMin, short yMin, short xMax, short yMax,
        VariationApplier? onGlyphParsed = null)
    {
        int offset = 10; // After numberOfContours + bbox
        ushort maxDepth = maxp.MaxComponentDepth;
        if (maxDepth == 0) maxDepth = 1;

        var allContours = new List<GlyphPoint[]>();

        bool moreComponents = true;
        while (moreComponents)
        {
            ushort flags = BigEndianReader.ReadUInt16(glyphData, ref offset);
            ushort componentGlyphIndex = BigEndianReader.ReadUInt16(glyphData, ref offset);

            // Read arg1, arg2
            int arg1, arg2;
            if ((flags & 1) != 0) // ARG_1_AND_2_ARE_WORDS
            {
                arg1 = BigEndianReader.ReadInt16(glyphData, ref offset);
                arg2 = BigEndianReader.ReadInt16(glyphData, ref offset);
            }
            else
            {
                arg1 = (sbyte)glyphData[offset++];
                arg2 = (sbyte)glyphData[offset++];
            }

            // Read transformation
            float a = 1, b = 0, c = 0, d = 1;
            int tx = 0, ty = 0;

            if ((flags & 3) != 0) // ARGS_ARE_XY_VALUES
            {
                tx = arg1;
                ty = arg2;
            }
            else
            {
                // Point matching mode: use as offsets for now
                tx = 0;
                ty = 0;
            }

            if ((flags & 8) != 0) // WE_HAVE_A_SCALE
            {
                a = d = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
            }
            else if ((flags & 64) != 0) // WE_HAVE_AN_X_AND_Y_SCALE
            {
                a = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
                d = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
            }
            else if ((flags & 128) != 0) // WE_HAVE_A_TWO_BY_TWO
            {
                a = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
                b = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
                c = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
                d = BigEndianReader.ReadF2Dot14(glyphData, ref offset);
            }

            // Recursively parse the component glyph
            if (recursionDepth < maxDepth)
            {
                ReadOnlySpan<byte> glyfTableData = sfnt.GetTableData("glyf");

                Glyph? componentGlyph = ParseGlyph(
                    glyfTableData, loca, componentGlyphIndex,
                    sfnt, maxp, recursionDepth + 1, onGlyphParsed);

                if (componentGlyph != null && !componentGlyph.IsEmpty)
                {
                    foreach (var contour in componentGlyph.Contours)
                    {
                        var transformedPoints = new GlyphPoint[contour.Length];
                        for (int i = 0; i < contour.Length; i++)
                        {
                            float x = contour[i].X;
                            float y = contour[i].Y;

                            // Apply inverse Y-flip for compound transformation,
                            // Then re-flip after transform
                            // Actually, since glyph already has Y-flipped coordinates,
                            // we need to un-flip, apply transform, re-flip
                            float yTtf = -y;
                            float newX = a * x + b * yTtf + tx;
                            float newY = c * x + d * yTtf + ty;
                            short finalX = (short)Math.Round(newX);
                            short finalY = (short)Math.Round(-newY); // Y-flip back

                            transformedPoints[i] = new GlyphPoint(
                                finalX, finalY, contour[i].OnCurve);
                        }
                        allContours.Add(transformedPoints);
                    }
                }
            }

            moreComponents = (flags & 32) != 0; // MORE_COMPONENTS
        }

        return new Glyph(1, allContours.ToArray(), xMin, yMin, xMax, yMax);
    }
}
