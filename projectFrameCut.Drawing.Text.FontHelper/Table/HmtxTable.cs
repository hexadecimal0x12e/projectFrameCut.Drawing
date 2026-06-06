using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

[DebuggerNonUserCode()]
internal static class HmtxTable
{
    public static HmtxData Parse(
        ReadOnlySpan<byte> data,
        ushort numOfLongHorMetrics,
        ushort numGlyphs)
    {
        if (numGlyphs < numOfLongHorMetrics)
            throw new InvalidFontFileException(
                "numGlyphs is less than numOfLongHorMetrics.");

        int offset = 0;
        int expectedLength = numOfLongHorMetrics * 4 + (numGlyphs - numOfLongHorMetrics) * 2;

        if (data.Length < expectedLength)
            throw new InvalidFontFileException("hmtx table is too small.");

        var advanceWidths = new ushort[numGlyphs];
        var leftSideBearings = new short[numGlyphs];

        // Read long metrics (advanceWidth + lsb pairs)
        ushort lastAdvanceWidth = 0;
        for (int i = 0; i < numOfLongHorMetrics; i++)
        {
            lastAdvanceWidth = BigEndianReader.ReadUInt16(data, ref offset);
            short lsb = BigEndianReader.ReadInt16(data, ref offset);
            advanceWidths[i] = lastAdvanceWidth;
            leftSideBearings[i] = lsb;
        }

        // Remaining glyphs use the last advance width
        for (int i = numOfLongHorMetrics; i < numGlyphs; i++)
        {
            advanceWidths[i] = lastAdvanceWidth;
            leftSideBearings[i] = BigEndianReader.ReadInt16(data, ref offset);
        }

        return new HmtxData(advanceWidths, leftSideBearings);
    }
}

internal readonly struct HmtxData
{
    private readonly ushort[] _advanceWidths;
    private readonly short[] _leftSideBearings;

    internal HmtxData(ushort[] advanceWidths, short[] leftSideBearings)
    {
        _advanceWidths = advanceWidths;
        _leftSideBearings = leftSideBearings;
    }

    public ushort GetAdvanceWidth(ushort glyphIndex) =>
        _advanceWidths[glyphIndex];

    public short GetLeftSideBearing(ushort glyphIndex) =>
        _leftSideBearings[glyphIndex];
}
