using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal interface ICmapSubtable
{
    ushort GetGlyphIndex(uint charCode);
}
[DebuggerNonUserCode()]
internal static class CmapTable
{
    public static CmapData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return new CmapData(null);

        int offset = 0;
        ushort version = BigEndianReader.ReadUInt16(data, ref offset);
        ushort numTables = BigEndianReader.ReadUInt16(data, ref offset);

        // Scan all encoding records and find the best subtable
        int bestScore = -1;
        int bestSubtableOffset = -1;

        int recordStart = offset;
        for (int i = 0; i < numTables; i++)
        {
            if (recordStart + 8 > data.Length)
                break;

            int recOff = recordStart + i * 8;
            ushort platformId = BigEndianReader.ReadUInt16(data, ref recOff);
            ushort encodingId = BigEndianReader.ReadUInt16(data, ref recOff);
            uint subtableOffset = BigEndianReader.ReadUInt32(data, ref recOff);

            int score = GetEncodingScore(platformId, encodingId);
            if (score > bestScore)
            {
                bestScore = score;
                bestSubtableOffset = (int)subtableOffset;
            }
        }

        if (bestSubtableOffset < 0 || bestSubtableOffset >= data.Length)
            return new CmapData(null);

        // Parse the selected subtable
        ReadOnlySpan<byte> subtable = data[bestSubtableOffset..];

        if (subtable.Length < 2)
            return new CmapData(null);

        offset = 0;
        ushort format = BigEndianReader.ReadUInt16(subtable, ref offset);

        ICmapSubtable? parser = format switch
        {
            4 => ParseFormat4(subtable),
            12 => ParseFormat12(subtable),
            _ => null
        };

        return new CmapData(parser);
    }

    private static int GetEncodingScore(ushort platformId, ushort encodingId)
    {
        // Windows, UTF-16 (full Unicode) -> Format 12
        if (platformId == 3 && encodingId == 10) return 5;
        // Windows, Unicode BMP -> Format 4
        if (platformId == 3 && encodingId == 1) return 4;
        // Windows, Symbol
        if (platformId == 3 && encodingId == 0) return 3;
        // Unicode platform, UTF-32
        if (platformId == 0 && encodingId == 3) return 2;
        // Unicode platform, BMP
        if (platformId == 0 && encodingId <= 2) return 1;
        // Macintosh
        if (platformId == 1) return 0;
        return -1;
    }

    private static ICmapSubtable? ParseFormat4(ReadOnlySpan<byte> data)
    {
        if (data.Length < 14)
            return null;

        int offset = 0;
        ushort format = BigEndianReader.ReadUInt16(data, ref offset);
        if (format != 4) return null;

        ushort length = BigEndianReader.ReadUInt16(data, ref offset);
        // Skip language
        offset += 2;

        ushort segCountX2 = BigEndianReader.ReadUInt16(data, ref offset);
        int segCount = segCountX2 / 2;

        if (segCount <= 0 || length > data.Length)
            return null;

        // Skip searchRange, entrySelector, rangeShift
        offset += 6;

        // endCode array: segCount entries
        int endCodesOffset = offset;
        if (endCodesOffset + segCount * 2 > data.Length)
            return null;
        offset += segCount * 2;

        // Skip reservedPad
        offset += 2;

        // startCode array: segCount entries
        int startCodesOffset = offset;
        if (startCodesOffset + segCount * 2 > data.Length)
            return null;
        offset += segCount * 2;

        // idDelta array: segCount entries
        int idDeltaOffset = offset;
        if (idDeltaOffset + segCount * 2 > data.Length)
            return null;
        offset += segCount * 2;

        // idRangeOffset array: segCount entries
        int idRangeOffsetOffset = offset;
        if (idRangeOffsetOffset + segCount * 2 > data.Length)
            return null;
        offset += segCount * 2;

        // The rest is glyphIdArray
        int glyphIdArrayOffset = offset;

        return new CmapFormat4(data.ToArray(), segCount,
            endCodesOffset, startCodesOffset,
            idDeltaOffset, idRangeOffsetOffset,
            glyphIdArrayOffset);
    }

    private static ICmapSubtable? ParseFormat12(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
            return null;

        int offset = 0;
        ushort format = BigEndianReader.ReadUInt16(data, ref offset);
        if (format != 12) return null;

        // Skip reserved (uint16)
        offset += 2;

        uint length = BigEndianReader.ReadUInt32(data, ref offset);
        // Skip language (uint32)
        offset += 4;

        uint numGroups = BigEndianReader.ReadUInt32(data, ref offset);
        if (numGroups == 0 || numGroups > data.Length / 12)
            return null;

        int groupsOffset = offset;
        return new CmapFormat12(data.ToArray(), groupsOffset, (int)numGroups);
    }

    [DebuggerNonUserCode()]
    private sealed class CmapFormat4 : ICmapSubtable
    {
        private readonly byte[] _data;
        private readonly int _segCount;
        private readonly int _endCodesOffset;
        private readonly int _startCodesOffset;
        private readonly int _idDeltaOffset;
        private readonly int _idRangeOffsetOffset;
        private readonly int _glyphIdArrayOffset;

        public CmapFormat4(
            byte[] data, int segCount,
            int endCodesOffset, int startCodesOffset,
            int idDeltaOffset, int idRangeOffsetOffset,
            int glyphIdArrayOffset)
        {
            _data = data;
            _segCount = segCount;
            _endCodesOffset = endCodesOffset;
            _startCodesOffset = startCodesOffset;
            _idDeltaOffset = idDeltaOffset;
            _idRangeOffsetOffset = idRangeOffsetOffset;
            _glyphIdArrayOffset = glyphIdArrayOffset;
        }

        public ushort GetGlyphIndex(uint charCode)
        {
            // Binary search for the segment containing this charCode
            int lo = 0;
            int hi = _segCount - 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int endCodeOff = _endCodesOffset + mid * 2;
                ushort endCode = BigEndianReader.ReadUInt16(_data, ref endCodeOff);

                if (charCode > endCode)
                {
                    lo = mid + 1;
                }
                else
                {
                    int startCodeOff = _startCodesOffset + mid * 2;
                    ushort startCode = BigEndianReader.ReadUInt16(_data, ref startCodeOff);

                    if (charCode >= startCode)
                        return GetGlyphFromSegment(mid, charCode, startCode);

                    hi = mid - 1;
                }
            }

            return 0;
        }

        private ushort GetGlyphFromSegment(int segment, uint charCode, ushort startCode)
        {
            // OpenType spec formula:
            //   glyphIndex = *(idRangeOffset[i] + (c - startCode) + &idRangeOffset[i])
            // where &idRangeOffset[i] is the byte address OF the idRangeOffset[i] entry
            // (the start of the field, NOT the address after reading it).
            int rangeBase = _idRangeOffsetOffset + segment * 2;
            int rangeOff = rangeBase;
            ushort idRangeOffset = BigEndianReader.ReadUInt16(_data, ref rangeOff);

            int deltaOff = _idDeltaOffset + segment * 2;
            short idDelta = BigEndianReader.ReadInt16(_data, ref deltaOff);

            if (idRangeOffset == 0)
                return (ushort)((charCode + idDelta) & 0xFFFF);

            int effectiveOffset = rangeBase + idRangeOffset + 2 * (int)(charCode - startCode);

            if ((uint)(effectiveOffset + 1) >= (uint)_data.Length)
                return 0;

            ushort glyphIndex = BigEndianReader.ReadUInt16(_data, ref effectiveOffset);

            if (glyphIndex == 0)
                return 0;

            return (ushort)((glyphIndex + idDelta) & 0xFFFF);
        }
    }
    
    [DebuggerNonUserCode()]
    private sealed class CmapFormat12 : ICmapSubtable
    {
        private readonly byte[] _data;
        private readonly int _groupsOffset;
        private readonly int _numGroups;

        public CmapFormat12(byte[] data, int groupsOffset, int numGroups)
        {
            _data = data;
            _groupsOffset = groupsOffset;
            _numGroups = numGroups;
        }

        public ushort GetGlyphIndex(uint charCode)
        {
            int lo = 0;
            int hi = _numGroups - 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int groupOff = _groupsOffset + mid * 12;

                uint startCharCode = BigEndianReader.ReadUInt32(_data, ref groupOff);
                uint endCharCode = BigEndianReader.ReadUInt32(_data, ref groupOff);
                uint startGlyphId = BigEndianReader.ReadUInt32(_data, ref groupOff);

                if (charCode < startCharCode)
                    hi = mid - 1;
                else if (charCode > endCharCode)
                    lo = mid + 1;
                else
                    return (ushort)(startGlyphId + (charCode - startCharCode));
            }

            return 0;
        }
    }
}

internal readonly struct CmapData
{
    private readonly ICmapSubtable? _subtable;

    internal CmapData(ICmapSubtable? subtable)
    {
        _subtable = subtable;
    }

    public ushort GetGlyphIndex(uint charCode) =>
        _subtable?.GetGlyphIndex(charCode) ?? 0;
}
