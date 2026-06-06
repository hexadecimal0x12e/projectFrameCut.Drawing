using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

[DebuggerNonUserCode()]
internal static class HeadTable
{
    public static HeadData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 54)
            throw new InvalidFontFileException("head table is too small.");

        int offset = 0;

        // Skip tableVersion (fixed 16.16), fontRevision (fixed 16.16)
        offset += 8;

        // Skip checkSumAdjustment (uint32) and magicNumber (uint32)
        offset += 8;

        // Skip flags (uint16)
        offset += 2;

        ushort unitsPerEm = BigEndianReader.ReadUInt16(data, ref offset);
        if (unitsPerEm < 16 || unitsPerEm > 16384)
            throw new InvalidFontFileException($"Invalid unitsPerEm: {unitsPerEm}");

        // Skip created (longdatetime, 8 bytes)
        offset += 8;

        // Skip modified (longdatetime, 8 bytes)
        offset += 8;

        short xMin = BigEndianReader.ReadInt16(data, ref offset);
        short yMin = BigEndianReader.ReadInt16(data, ref offset);
        short xMax = BigEndianReader.ReadInt16(data, ref offset);
        short yMax = BigEndianReader.ReadInt16(data, ref offset);

        ushort macStyle = BigEndianReader.ReadUInt16(data, ref offset);
        ushort lowestRecPpem = BigEndianReader.ReadUInt16(data, ref offset);

        // Skip fontDirectionHint (int16)
        offset += 2;

        short indexToLocFormat = BigEndianReader.ReadInt16(data, ref offset);
        if (indexToLocFormat != 0 && indexToLocFormat != 1)
            throw new InvalidFontFileException(
                $"Invalid indexToLocFormat: {indexToLocFormat}");

        // Skip glyphDataFormat (int16)
        // offset += 2;

        return new HeadData(
            unitsPerEm, xMin, yMin, xMax, yMax,
            macStyle, lowestRecPpem, indexToLocFormat);
    }
}

internal readonly struct HeadData
{
    public ushort UnitsPerEm { get; }
    public short XMin { get; }
    public short YMin { get; }
    public short XMax { get; }
    public short YMax { get; }
    public ushort MacStyle { get; }
    public ushort LowestRecPpem { get; }
    public short IndexToLocFormat { get; }

    public bool IsItalic => (MacStyle & 2) != 0;
    public bool IsBold => (MacStyle & 1) != 0;

    internal HeadData(
        ushort unitsPerEm,
        short xMin, short yMin, short xMax, short yMax,
        ushort macStyle, ushort lowestRecPpem, short indexToLocFormat)
    {
        UnitsPerEm = unitsPerEm;
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
        MacStyle = macStyle;
        LowestRecPpem = lowestRecPpem;
        IndexToLocFormat = indexToLocFormat;
    }
}
