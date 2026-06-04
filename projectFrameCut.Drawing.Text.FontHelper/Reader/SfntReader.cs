namespace projectFrameCut.Drawing.Text.FontHelper.Reader;

internal sealed class SfntReader : IDisposable
{
    private readonly byte[] _data;
    private readonly Dictionary<string, TableEntry> _tableDirectory;
    private bool _disposed;

    private SfntReader(byte[] data) : this(data, 0) { }

    internal SfntReader(byte[] data, int sfntOffset)
    {
        _data = data;

        if (sfntOffset + 12 > data.Length)
            throw new InvalidFontFileException("File too small to be a valid font.");

        int offset = sfntOffset;
        uint sfVersion = BigEndianReader.ReadUInt32(data, ref offset);
        ushort numTables = BigEndianReader.ReadUInt16(data, ref offset);

        // Validate the SFNT version against known values
        switch (sfVersion)
        {
            case 0x00010000:  // TrueType v1.0
            case 0x4F54544F:  // "OTTO" — OpenType with CFF
            case 0x74727565:  // "true" — older TrueType
            case 0x74797031:  // "typ1" — older PostScript
                break;

            case 0x74746366:  // "ttcf" — TrueType Collection
                if (sfntOffset == 0)
                    throw new InvalidFontFileException(
                        "TrueType Collection (.ttc) files are not supported. " +
                        "Please use a single-font .ttf or .otf file.");
                // If a sub-font starts with "ttcf", the TTC is corrupt
                goto default;

            case 0x774F4646:  // "wOFF" — WOFF
            case 0x774F4632:  // "wOF2" — WOFF2
                throw new InvalidFontFileException(
                    "WOFF/WOFF2 files are not supported. " +
                    "Please use a .ttf or .otf file.");

            default:
                throw new InvalidFontFileException(
                    "Unrecognized font format. Ensure the file is a valid .ttf or .otf file.");
        }

        if (numTables == 0)
            throw new InvalidFontFileException("Font contains zero tables.");

        // Skip searchRange, entrySelector, rangeShift
        offset += 6;

        int directorySize = numTables * 16;
        if (offset + directorySize > data.Length)
            throw new InvalidFontFileException(
                "Table directory exceeds file bounds.");

        _tableDirectory = new Dictionary<string, TableEntry>(numTables);
        for (int i = 0; i < numTables; i++)
        {
            string tag = BigEndianReader.ReadTag(data, ref offset);
            uint checksum = BigEndianReader.ReadUInt32(data, ref offset);
            uint tableOffset = BigEndianReader.ReadUInt32(data, ref offset);
            uint tableLength = BigEndianReader.ReadUInt32(data, ref offset);

            // TTC table offsets are absolute from the beginning of the file
            uint absOffset = tableOffset;
            if (absOffset + tableLength > (uint)data.Length
                || absOffset + tableLength < absOffset)
                throw new InvalidFontFileException(
                    $"Table '{tag}' data exceeds file bounds.");

            _tableDirectory[tag] = new TableEntry(absOffset, tableLength);
        }
    }

    public static SfntReader Load(string path) =>
        new(File.ReadAllBytes(path));

    public static SfntReader Load(byte[] data) => new(data);

    public ReadOnlySpan<byte> GetTableData(string tag)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_tableDirectory.TryGetValue(tag, out var entry))
            throw new InvalidFontFileException($"Required table '{tag}' not found.");

        return _data.AsSpan((int)entry.Offset, (int)entry.Length);
    }

    public bool HasTable(string tag)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tableDirectory.ContainsKey(tag);
    }

    public byte[] GetTableDataCopy(string tag)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_tableDirectory.TryGetValue(tag, out var entry))
            throw new InvalidFontFileException($"Required table '{tag}' not found.");

        byte[] result = new byte[entry.Length];
        Array.Copy(_data, (int)entry.Offset, result, 0, (int)entry.Length);
        return result;
    }

    internal bool TryGetTableEntry(string tag, out byte[] data, out uint offset, out uint length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_tableDirectory.TryGetValue(tag, out var entry))
        {
            data = _data;
            offset = entry.Offset;
            length = entry.Length;
            return true;
        }
        data = null!;
        offset = 0;
        length = 0;
        return false;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private readonly record struct TableEntry(uint Offset, uint Length);
}
