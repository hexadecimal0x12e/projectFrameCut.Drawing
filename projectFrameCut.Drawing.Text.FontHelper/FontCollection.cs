using projectFrameCut.Drawing.Text.FontHelper.Reader;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using System.Collections;

namespace projectFrameCut.Drawing.Text.FontHelper;

/// <summary>
/// Represents all fonts available in a TrueType Collection (.ttc) file.
/// Each entry provides metadata and a <see cref="FontFaceInfo.Load"/> method
/// to obtain the full <see cref="FontFace"/>.
/// </summary>
public sealed class FontCollection : IEnumerable<FontFaceInfo>
{
    private readonly FontFaceInfo[] _fonts;

    internal FontCollection(FontFaceInfo[] fonts)
    {
        _fonts = fonts;
    }

    /// <summary>Load a TTC file from disk.</summary>
    public static FontCollection Load(string path) =>
        Load(File.ReadAllBytes(path), path);

    /// <summary>Load a TTC file from a byte array.</summary>
    public static FontCollection Load(byte[] data)
        => Load(data, null);

    private static FontCollection Load(byte[] data, string? sourcePath)
    {
        int offset = 0;
        uint tag = BigEndianReader.ReadUInt32(data, ref offset);
        if (tag != 0x74746366) // "ttcf"
            throw new InvalidFontFileException("Not a TrueType Collection (.ttc) file.");

        // Skip major/minor version
        offset += 4;
        uint numFonts = BigEndianReader.ReadUInt32(data, ref offset);

        if (numFonts == 0 || numFonts > 0xFFFF)
            throw new InvalidFontFileException("Invalid font count in TTC header.");

        int fontsDataSize = (int)numFonts * 4;
        if (offset + fontsDataSize > data.Length)
            throw new InvalidFontFileException("TTC offset table exceeds file bounds.");

        var fontOffsets = new uint[numFonts];
        for (int i = 0; i < numFonts; i++)
            fontOffsets[i] = BigEndianReader.ReadUInt32(data, ref offset);

        var fonts = new FontFaceInfo[numFonts];
        for (int i = 0; i < numFonts; i++)
            fonts[i] = new FontFaceInfo(data, (int)fontOffsets[i], i, sourcePath);

        return new FontCollection(fonts);
    }

    public IEnumerator<FontFaceInfo> GetEnumerator()
    {
        return ((IEnumerable<FontFaceInfo>)_fonts).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _fonts.GetEnumerator();
    }

    /// <summary>Number of fonts in this collection.</summary>
    public int Count => _fonts.Length;

    /// <summary>Get metadata for the font at the given index.</summary>
    public FontFaceInfo this[int index] => _fonts[index];
}

/// <summary>
/// Provides metadata for a single font within a TrueType Collection.
/// Call <see cref="Load"/> to obtain the full <see cref="FontFace"/>.
/// </summary>
public sealed class FontFaceInfo
{
    private readonly byte[] _data;
    private readonly int _sfntOffset;
    private readonly string? _sourcePath;

    internal FontFaceInfo(byte[] data, int sfntOffset, int index, string? sourcePath)
    {
        _data = data;
        _sfntOffset = sfntOffset;
        _sourcePath = sourcePath;
        Index = index;

        // Eagerly parse the "name" table to populate metadata
        // without loading the entire font.
        string? family = null, subfamily = null;
        var targetLanguages = new HashSet<TargetLanguage>();
        var localizedNames = new Dictionary<TargetLanguage, string>();
        int off = sfntOffset + 4; // skip SFNT version

        if (off + 2 <= data.Length)
        {
            ushort numTables = BigEndianReader.ReadUInt16(data, ref off);
            off += 6; // skip searchRange, entrySelector, rangeShift

            for (int i = 0; i < numTables; i++)
            {
                if (off + 16 > data.Length) break;

                string tag = BigEndianReader.ReadTag(data, ref off);
                off += 4; // skip checksum
                uint tblOffset = BigEndianReader.ReadUInt32(data, ref off);
                uint tblLength = BigEndianReader.ReadUInt32(data, ref off);

                if (tag == "name")
                {
                    // TTC table offsets are absolute from the beginning of the file
                    if (tblOffset + tblLength <= (uint)data.Length)
                    {
                        var names = NameTable.Parse(
                            data, (int)tblOffset, (int)tblLength);
                        family = names.GetName(16) ?? names.GetName(1);
                        subfamily = names.GetName(17) ?? names.GetName(2);

                        foreach (var lang in names.GetTargetLanguages())
                        {
                            targetLanguages.Add(lang);
                            string? localizedName = names.GetLocalizedName(16, lang)
                                                 ?? names.GetLocalizedName(1, lang);
                            if (localizedName != null)
                                localizedNames[lang] = localizedName;
                        }
                    }
                    break;
                }
            }
        }

        FamilyName = family ?? "Unknown";
        SubfamilyName = subfamily ?? "Regular";
        TargetLanguages = targetLanguages;
        LocalizedNames = localizedNames;
    }

    /// <summary>Zero-based index of this font in the TTC.</summary>
    public int Index { get; }

    /// <summary>Font family name (Name ID 1 or 16).</summary>
    public string FamilyName { get; }

    /// <summary>Font subfamily name (Name ID 2 or 17).</summary>
    public string SubfamilyName { get; }

    /// <summary>
    /// All distinct target languages found in the font's name table,
    /// identified by their platform and language identifier.
    /// </summary>
    public IReadOnlySet<TargetLanguage> TargetLanguages { get; }

    /// <summary>
    /// Maps each <see cref="TargetLanguage"/> present in the font to
    /// its localized family name (Name ID 16 or 1).
    /// </summary>
    public IReadOnlyDictionary<TargetLanguage, string> LocalizedNames { get; }

    /// <summary>
    /// The localized family name in this font's primary language (non-English if available),
    /// or <see cref="FamilyName"/> as fallback.
    /// </summary>
    public string DisplayName
    {
        get
        {
            // Prefer Windows platform entries (proper UTF-16BE encoding)
            foreach (var (lang, name) in LocalizedNames)
                if (lang.PlatformId == 3 && lang.LanguageId != 0x0409)
                    return name;
            // Fall back to other non-English entries
            foreach (var (lang, name) in LocalizedNames)
            {
                if (lang.PlatformId == 3 && lang.LanguageId == 0x0409) continue;
                if (lang.PlatformId == 1 && lang.LanguageId == 0) continue;
                return name;
            }
            return FamilyName;
        }
    }

    /// <summary>Load the full <see cref="FontFace"/> for this font.</summary>
    public FontFace Load()
    {
        var reader = new SfntReader(_data, _sfntOffset, _sourcePath);
        return new FontFace(reader);
    }
}
