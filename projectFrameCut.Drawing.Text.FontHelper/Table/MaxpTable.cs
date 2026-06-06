using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

[DebuggerNonUserCode()]
internal static class MaxpTable
{
    public static MaxpData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6)
            throw new InvalidFontFileException("maxp table is too small.");

        int offset = 0;

        // Read table version (fixed 16.16)
        uint version = BigEndianReader.ReadUInt32(data, ref offset);
        ushort numGlyphs = BigEndianReader.ReadUInt16(data, ref offset);

        ushort maxComponentDepth = 0;
        if (version >= 0x00010000 && data.Length >= 32)
        {
            // Skip: maxPoints, maxContours, maxCompositePoints,
            //       maxCompositeContours, maxZones, maxTwilightPoints,
            //       maxStorage, maxFunctionDefs, maxInstructionDefs,
            //       maxStackElements, maxSizeOfInstructions,
            //       maxComponentElements
            offset += 22;
            maxComponentDepth = BigEndianReader.ReadUInt16(data, ref offset);
        }

        return new MaxpData(numGlyphs, maxComponentDepth);
    }
}

internal readonly struct MaxpData
{
    public ushort NumGlyphs { get; }
    public ushort MaxComponentDepth { get; }

    internal MaxpData(ushort numGlyphs, ushort maxComponentDepth)
    {
        NumGlyphs = numGlyphs;
        MaxComponentDepth = maxComponentDepth;
    }
}
