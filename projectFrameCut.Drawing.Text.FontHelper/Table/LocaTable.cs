using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

[DebuggerNonUserCode()]
internal static class LocaTable
{
    public static LocaData Parse(
        ReadOnlySpan<byte> data,
        ushort numGlyphs,
        short indexToLocFormat)
    {
        // Need numGlyphs + 1 offsets
        int entryCount = numGlyphs + 1;
        var offsets = new uint[entryCount];

        if (indexToLocFormat == 0)
        {
            // Short offsets: each is ushort, multiplied by 2
            int expectedLength = entryCount * 2;
            if (data.Length < expectedLength)
                throw new InvalidFontFileException("loca table (short format) too small.");

            int offset = 0;
            for (int i = 0; i < entryCount; i++)
                offsets[i] = (uint)BigEndianReader.ReadUInt16(data, ref offset) * 2u;
        }
        else
        {
            // Long offsets: each is uint32
            int expectedLength = entryCount * 4;
            if (data.Length < expectedLength)
                throw new InvalidFontFileException("loca table (long format) too small.");

            int offset = 0;
            for (int i = 0; i < entryCount; i++)
                offsets[i] = BigEndianReader.ReadUInt32(data, ref offset);
        }

        return new LocaData(offsets);
    }
}

internal readonly struct LocaData
{
    private readonly uint[] _offsets;

    internal LocaData(uint[] offsets)
    {
        _offsets = offsets;
    }

    public uint GetGlyphOffset(ushort glyphIndex) =>
        _offsets[glyphIndex];

    public uint GetGlyphLength(ushort glyphIndex) =>
        _offsets[glyphIndex + 1] - _offsets[glyphIndex];
}
