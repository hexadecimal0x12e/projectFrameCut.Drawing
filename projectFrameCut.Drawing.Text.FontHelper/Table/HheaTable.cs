using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

[DebuggerNonUserCode()]
internal static class HheaTable
{
    public static HheaData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 36)
            throw new InvalidFontFileException("hhea table is too small.");

        int offset = 0;

        // Skip table version (fixed 16.16, 4 bytes)
        offset += 4;

        short ascent = BigEndianReader.ReadInt16(data, ref offset);
        short descent = BigEndianReader.ReadInt16(data, ref offset);
        short lineGap = BigEndianReader.ReadInt16(data, ref offset);

        // Skip advanceWidthMax, minLeftSideBearing, minRightSideBearing, xMaxExtent
        offset += 8;

        // Skip caretSlopeRise, caretSlopeRun, caretOffset
        offset += 6;

        // Skip 4 reserved int16 values
        offset += 8;

        // Skip metricDataFormat
        offset += 2;

        ushort numOfLongHorMetrics = BigEndianReader.ReadUInt16(data, ref offset);
        if (numOfLongHorMetrics == 0)
            throw new InvalidFontFileException("numOfLongHorMetrics is 0.");

        return new HheaData(ascent, descent, lineGap, numOfLongHorMetrics);
    }
}

internal readonly struct HheaData
{
    public short Ascent { get; }
    public short Descent { get; }
    public short LineGap { get; }
    public ushort NumOfLongHorMetrics { get; }

    internal HheaData(short ascent, short descent, short lineGap, ushort numOfLongHorMetrics)
    {
        Ascent = ascent;
        Descent = descent;
        LineGap = lineGap;
        NumOfLongHorMetrics = numOfLongHorMetrics;
    }
}
