using projectFrameCut.Drawing.Text.FontHelper.Reader;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal static class Os2Table
{
    public static Os2Data Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 78)
            return new Os2Data(400, 5, 0, 0, 0, 0); // Defaults for short table

        int offset = 0;

        ushort version = BigEndianReader.ReadUInt16(data, ref offset);

        // Skip xAvgCharWidth
        offset += 2;

        ushort weightClass = BigEndianReader.ReadUInt16(data, ref offset);
        ushort widthClass = BigEndianReader.ReadUInt16(data, ref offset);

        // Clamp weight class
        if (weightClass < 1) weightClass = 400;
        if (weightClass > 1000) weightClass = 400;

        // Clamp width class
        if (widthClass < 1 || widthClass > 9) widthClass = 5;

        // Skip fsType
        offset += 2;

        // Skip ySubscriptXSize, ySubscriptYSize, ySubscriptXOffset, ySubscriptYOffset
        offset += 8;

        // Skip ySuperscriptXSize, ySuperscriptYSize, ySuperscriptXOffset, ySuperscriptYOffset
        offset += 8;

        // Skip yStrikeoutSize, yStrikeoutPosition
        offset += 4;

        short familyClass = BigEndianReader.ReadInt16(data, ref offset);

        // Skip panose (10 bytes)
        offset += 10;

        // Skip ulUnicodeRange1-4
        offset += 16;

        // Skip achVendID (4 bytes)
        offset += 4;

        ushort fsSelection = BigEndianReader.ReadUInt16(data, ref offset);

        // Skip usFirstCharIndex, usLastCharIndex
        offset += 4;

        // Skip sTypoAscender, sTypoDescender, sTypoLineGap
        short typoAscender = BigEndianReader.ReadInt16(data, ref offset);
        short typoDescender = BigEndianReader.ReadInt16(data, ref offset);
        short typoLineGap = BigEndianReader.ReadInt16(data, ref offset);

        // Skip usWinAscent, usWinDescent
        // offset += 4;

        return new Os2Data(weightClass, widthClass, fsSelection,
                          typoAscender, typoDescender, typoLineGap);
    }
}

internal readonly struct Os2Data
{
    public ushort WeightClass { get; }
    public ushort WidthClass { get; }
    public ushort FsSelection { get; }
    public short TypoAscender { get; }
    public short TypoDescender { get; }
    public short TypoLineGap { get; }

    public bool IsItalic => (FsSelection & 1) != 0;
    public bool IsBold => (FsSelection & 0x20) != 0;
    public bool IsRegular => (FsSelection & 0x40) != 0;

    internal Os2Data(
        ushort weightClass, ushort widthClass, ushort fsSelection,
        short typoAscender, short typoDescender, short typoLineGap)
    {
        WeightClass = weightClass;
        WidthClass = widthClass;
        FsSelection = fsSelection;
        TypoAscender = typoAscender;
        TypoDescender = typoDescender;
        TypoLineGap = typoLineGap;
    }
}
