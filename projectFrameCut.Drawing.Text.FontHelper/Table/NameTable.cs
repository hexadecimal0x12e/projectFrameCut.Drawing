using projectFrameCut.Drawing.Text.FontHelper.Reader;
using System.Text;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal static class NameTable
{
    public static NameData Parse(ReadOnlySpan<byte> data)
    {
        var result = ParseCore(data);
        return result != null
            ? new NameData(result.Names)
            : new NameData(new Dictionary<ushort, string>());
    }

    internal static NameData Parse(byte[] fontData, int tableOffset, int tableLength)
    {
        var result = ParseCore(fontData.AsSpan(tableOffset, tableLength));
        return result != null
            ? new NameData(result.Names, result.Records, fontData,
                           result.StringOffset, tableOffset, tableLength)
            : new NameData(new Dictionary<ushort, string>());
    }

    private static ParseResult? ParseCore(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6)
            return null;

        int offset = 0;
        ushort format = BigEndianReader.ReadUInt16(data, ref offset);

        // Only format 0 is supported (format 1 has language tags)
        if (format != 0)
            return null;

        ushort count = BigEndianReader.ReadUInt16(data, ref offset);
        ushort stringOffset = BigEndianReader.ReadUInt16(data, ref offset);

        if (count == 0)
            return null;

        int recordEnd = offset + count * 12;
        if (recordEnd > data.Length)
            return null;

        var records = new List<NameRecord>(count);
        for (int i = 0; i < count; i++)
        {
            ushort platformId = BigEndianReader.ReadUInt16(data, ref offset);
            ushort encodingId = BigEndianReader.ReadUInt16(data, ref offset);
            ushort languageId = BigEndianReader.ReadUInt16(data, ref offset);
            ushort nameId = BigEndianReader.ReadUInt16(data, ref offset);
            ushort length = BigEndianReader.ReadUInt16(data, ref offset);
            ushort offsetInStorage = BigEndianReader.ReadUInt16(data, ref offset);

            records.Add(new NameRecord(platformId, encodingId, languageId, nameId, length, offsetInStorage));
        }

        // Select best entries per nameId
        var names = new Dictionary<ushort, string>();
        var relevantNameIds = new ushort[] { 1, 2, 16, 17 };

        foreach (ushort nameId in relevantNameIds)
        {
            string? best = SelectBestName(data, stringOffset, records, nameId);
            if (best != null)
                names[nameId] = best;
        }

        return new ParseResult(names, records, stringOffset);
    }

    private sealed record ParseResult(
        Dictionary<ushort, string> Names,
        List<NameRecord> Records,
        ushort StringOffset);

    private static string? SelectBestName(
        ReadOnlySpan<byte> data, ushort stringOffset,
        List<NameRecord> records, ushort targetNameId)
    {
        for (int priority = 0; priority < 6; priority++)
        {
            foreach (var record in records)
            {
                if (record.NameId != targetNameId)
                    continue;

                if (!MatchesPriority(record, priority))
                    continue;

                string? decoded = DecodeNameEntry(data, stringOffset, record);
                if (decoded != null)
                    return decoded;
            }
        }

        return null;
    }

    private static bool MatchesPriority(NameRecord record, int priority) => priority switch
    {
        0 => record.PlatformId == 3 && record.EncodingId == 10 && record.LanguageId == 0x0409,
        1 => record.PlatformId == 3 && record.EncodingId == 1 && record.LanguageId == 0x0409,
        2 => record.PlatformId == 0 && record.EncodingId == 3,
        3 => record.PlatformId == 0 && record.EncodingId <= 2,
        4 => record.PlatformId == 1 && record.LanguageId == 0,
        5 => true,
        _ => false,
    };

    internal static string? DecodeNameEntry(
        ReadOnlySpan<byte> data, ushort stringOffset, NameRecord record)
    {
        int startOffset = stringOffset + record.OffsetInStorage;
        if (startOffset + record.Length > data.Length || record.Length == 0)
            return null;

        ReadOnlySpan<byte> entryData = data.Slice(startOffset, record.Length);

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Platform 3 (Windows): encoding depends on EncodingId
            //   1 = Unicode BMP (UTF-16BE), 2 = Shift-JIS, 10 = Full Unicode (UTF-16BE)
            if (record.PlatformId == 3)
            {
                if (record.EncodingId == 1 || record.EncodingId == 10)
                    return Encoding.BigEndianUnicode.GetString(entryData);
                if (record.EncodingId == 2)
                    return Encoding.GetEncoding(932).GetString(entryData);
                return Encoding.UTF8.GetString(entryData);
            }
            // Platform 0 (Unicode): UTF-16BE
            if (record.PlatformId == 0)
            {
                return Encoding.BigEndianUnicode.GetString(entryData);
            }
            // Platform 1 (Macintosh): encoding depends on EncodingId
            //   0 = MacRoman, 1 = Shift-JIS, 2 = Big5, 3 = EUC-KR, 25 = GB2312
            else if (record.PlatformId == 1)
            {
                return record.EncodingId switch
                {
                    0 => DecodeMacRoman(entryData),
                    1 => Encoding.GetEncoding(932).GetString(entryData),   // Shift-JIS
                    2 => Encoding.GetEncoding(950).GetString(entryData),   // Big5 (Traditional Chinese)
                    3 => Encoding.GetEncoding(949).GetString(entryData),   // EUC-KR (Korean)
                    25 => Encoding.GetEncoding(936).GetString(entryData),  // GB2312 (Simplified Chinese)
                    _ => Encoding.UTF8.GetString(entryData),
                };
            }
        }
        catch
        {
            // If decoding fails for any reason, skip this entry
        }

        return null;
    }

    private static string DecodeMacRoman(ReadOnlySpan<byte> data)
    {
        // Basic MacRoman to Unicode mapping for common characters
        var chars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            chars[i] = b < 0x80 ? (char)b : MacRomanToUnicode[b - 0x80];
        }
        return new string(chars);
    }

    // MacRoman encoding table for bytes 0x80-0xFF
    private static readonly char[] MacRomanToUnicode =
    [
        'Ä', 'Å', 'Ç', 'É', 'Ñ', 'Ö', 'Ü', 'á', // 80-87
        'à', 'â', 'ä', 'ã', 'å', 'ç', 'é', 'è', // 88-8F
        'ê', 'ë', 'í', 'ì', 'î', 'ï', 'ñ', 'ó', // 90-97
        'ò', 'ô', 'ö', 'õ', 'ú', 'ù', 'û', 'ü', // 98-9F
        '†', '°', '¢', '£', '§', '•', '¶', 'ß', // A0-A7
        '®', '©', '™', '´', '¨', '≠', 'Æ', 'Ø', // A8-AF
        '∞', '±', '≤', '≥', '¥', 'µ', '∂', '∑', // B0-B7
        '∏', 'π', '∫', 'ª', 'º', 'Ω', 'æ', 'ø', // B8-BF
        '¿', '¡', '¬', '√', 'ƒ', '≈', '∆', '«', // C0-C7
        '»', '…', ' ', 'À', 'Ã', 'Õ', 'Œ', 'œ', // C8-CF
        '–', '—', '“', '”', '‘', '’', '÷', '◊', // D0-D7
        'ÿ', 'Ÿ', '⁄', '€', '‹', '›', 'ﬁ', 'ﬂ', // D8-DF
        '‡', '·', '‚', '„', '‰', 'Â', 'Ê', 'Á', // E0-E7
        'Ë', 'È', 'Í', 'Î', 'Ï', 'Ì', 'Ó', 'Ô', // E8-EF
        '', 'Ò', 'Ú', 'Û', 'Ù', 'ı', 'ˆ', '˜', // F0-F7
        '¯', '˘', '˙', '˚', '¸', '˝', '˛', 'ˇ', // F8-FF
    ];
}

internal readonly record struct NameRecord(
    ushort PlatformId, ushort EncodingId, ushort LanguageId,
    ushort NameId, ushort Length, ushort OffsetInStorage);

internal readonly struct NameData
{
    private readonly Dictionary<ushort, string> _bestNames;
    private readonly List<NameRecord>? _records;
    private readonly byte[]? _fontData;
    private readonly ushort _stringOffset;
    private readonly int _tableOffset;
    private readonly int _tableLength;

    /// <summary>Creates a NameData with only best-name lookups (no localization).</summary>
    internal NameData(Dictionary<ushort, string> bestNames)
    {
        _bestNames = bestNames;
        _records = null;
        _fontData = null;
        _stringOffset = 0;
        _tableOffset = 0;
        _tableLength = 0;
    }

    /// <summary>Creates a NameData with full records for localized queries.</summary>
    internal NameData(
        Dictionary<ushort, string> bestNames,
        List<NameRecord> records,
        byte[] fontData,
        ushort stringOffset,
        int tableOffset,
        int tableLength)
    {
        _bestNames = bestNames;
        _records = records;
        _fontData = fontData;
        _stringOffset = stringOffset;
        _tableOffset = tableOffset;
        _tableLength = tableLength;
    }

    public string? GetName(ushort nameId) =>
        _bestNames.TryGetValue(nameId, out var name) ? name : null;

    /// <summary>
    /// Enumerates all distinct target languages present in the font's name table.
    /// Returns an empty sequence when raw record data is unavailable.
    /// </summary>
    public IEnumerable<TargetLanguage> GetTargetLanguages()
    {
        if (_records == null)
            yield break;

        var seen = new HashSet<TargetLanguage>();
        foreach (var r in _records)
        {
            var lang = new TargetLanguage(r.PlatformId, r.LanguageId);
            if (seen.Add(lang))
                yield return lang;
        }
    }

    /// <summary>
    /// Gets the localized name for the specified <paramref name="nameId"/>
    /// in the given <paramref name="language"/>, or <c>null</c> if no matching entry exists.
    /// </summary>
    public string? GetLocalizedName(ushort nameId, TargetLanguage language)
    {
        if (_fontData == null || _records == null)
            return null;

        var tableData = _fontData.AsSpan(_tableOffset, _tableLength);

        foreach (var record in _records)
        {
            if (record.NameId != nameId ||
                record.PlatformId != language.PlatformId ||
                record.LanguageId != language.LanguageId)
                continue;

            string? decoded = NameTable.DecodeNameEntry(tableData, _stringOffset, record);
            if (decoded != null)
                return decoded;
        }

        return null;
    }
}
