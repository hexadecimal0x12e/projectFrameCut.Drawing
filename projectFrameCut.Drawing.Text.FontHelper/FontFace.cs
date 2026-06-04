using projectFrameCut.Drawing.Text.FontHelper.Reader;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Text.FontHelper;

public sealed class FontFace : IDisposable
{
    private readonly SfntReader _sfnt;
    private readonly HeadData _head;
    private readonly MaxpData _maxp;
    private readonly HmtxData _hmtx;
    private readonly NameData _name;
    private readonly Os2Data _os2;
    private readonly CmapData _cmap;
    private readonly LocaData? _loca;
    private readonly byte[]? _glyfData;
    private readonly IReadOnlySet<TargetLanguage> _targetLanguages;
    private readonly IReadOnlyDictionary<TargetLanguage, string> _localizedNames;
    private bool _disposed;

    internal FontFace(SfntReader sfnt)
    {
        _sfnt = sfnt;

        // Parse all tables (required ones throw if missing)
        _head = HeadTable.Parse(sfnt.GetTableData("head"));
        _maxp = MaxpTable.Parse(sfnt.GetTableData("maxp"));

        HheaData hhea = HheaTable.Parse(sfnt.GetTableData("hhea"));
        _hmtx = HmtxTable.Parse(
            sfnt.GetTableData("hmtx"),
            hhea.NumOfLongHorMetrics,
            _maxp.NumGlyphs);

        // CFF-based OpenType fonts (OTTO) use "CFF " instead of "loca"+"glyf"
        _loca = sfnt.HasTable("loca")
            ? LocaTable.Parse(sfnt.GetTableData("loca"), _maxp.NumGlyphs, _head.IndexToLocFormat)
            : null;

        _glyfData = sfnt.HasTable("glyf") ? sfnt.GetTableDataCopy("glyf") : null;
        _cmap = CmapTable.Parse(sfnt.GetTableData("cmap"));

        // Optional tables
        _name = sfnt.TryGetTableEntry("name", out var fontData, out var nameOffset, out var nameLength)
            ? NameTable.Parse(fontData, (int)nameOffset, (int)nameLength)
            : new NameData(new Dictionary<ushort, string>());

        var targetLanguages = new HashSet<TargetLanguage>();
        var localizedNames = new Dictionary<TargetLanguage, string>();
        foreach (var lang in _name.GetTargetLanguages())
        {
            targetLanguages.Add(lang);
            string? localizedName = _name.GetLocalizedName(16, lang)
                                 ?? _name.GetLocalizedName(1, lang);
            if (localizedName != null)
                localizedNames[lang] = localizedName;
        }
        _targetLanguages = targetLanguages;
        _localizedNames = localizedNames;

        _os2 = sfnt.HasTable("OS/2")
            ? Os2Table.Parse(sfnt.GetTableData("OS/2"))
            : new Os2Data(400, 5, 0, 0, 0, 0);
    }

    public static FontFace Load(string path) =>
        new(SfntReader.Load(path));

    public static FontFace Load(byte[] data) =>
        new(SfntReader.Load(data));

    // ── TrueType Collection (.ttc) support ──

    public static FontCollection OpenTtcCollection(string path) =>
        FontCollection.Load(path);

    public static FontCollection OpenTtcCollection(byte[] data) =>
        FontCollection.Load(data);

    // ── Font metadata ──

    public string FamilyName
    {
        get
        {
            string? typoFamily = _name.GetName(16);  // Typographic Family
            if (!string.IsNullOrEmpty(typoFamily))
                return typoFamily;

            string? family = _name.GetName(1);  // Family
            return !string.IsNullOrEmpty(family) ? family : "Unknown";
        }
    }

    public string SubfamilyName
    {
        get
        {
            string? typoSubfamily = _name.GetName(17);  // Typographic Subfamily
            if (!string.IsNullOrEmpty(typoSubfamily))
                return typoSubfamily;

            string? subfamily = _name.GetName(2);  // Subfamily
            return !string.IsNullOrEmpty(subfamily) ? subfamily : "Regular";
        }
    }

    /// <summary>
    /// All distinct target languages found in the font's name table.
    /// </summary>
    public IReadOnlySet<TargetLanguage> TargetLanguages => _targetLanguages;

    /// <summary>
    /// Maps each <see cref="TargetLanguage"/> present in the font to
    /// its localized family name (Name ID 16 or 1).
    /// </summary>
    public IReadOnlyDictionary<TargetLanguage, string> LocalizedNames => _localizedNames;

    /// <summary>
    /// The localized family name in this font's primary language (non-English if available),
    /// or <see cref="FamilyName"/> as fallback.
    /// </summary>
    public string DisplayName
    {
        get
        {
            foreach (var (lang, name) in _localizedNames)
            {
                if (lang.PlatformId == 3 && lang.LanguageId == 0x0409) continue;
                if (lang.PlatformId == 1 && lang.LanguageId == 0) continue;
                return name;
            }
            return FamilyName;
        }
    }

    public ushort UnitsPerEm => _head.UnitsPerEm;
    public int GlyphCount => _maxp.NumGlyphs;
    public bool IsItalic => _os2.IsItalic || _head.IsItalic;
    public ushort WeightClass => _os2.WeightClass;

    // ── Glyph access ──

    public ushort GetGlyphIndex(char unicodeCodepoint) =>
        _cmap.GetGlyphIndex((uint)unicodeCodepoint);

    public Glyph? GetGlyph(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (glyphIndex >= _maxp.NumGlyphs)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        if (_loca == null || _glyfData == null)
            throw new InvalidFontFileException(
                "This font uses CFF outlines and does not support glyph-level access. " +
                "Use GetAdvanceWidth() for metrics instead.");

        Glyph? glyph = GlyfTable.ParseGlyph(
            _glyfData, _loca.Value, glyphIndex, _sfnt, _maxp, 0);

        if (glyph != null)
        {
            glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
            glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
        }

        return glyph;
    }

    public ushort GetAdvanceWidth(ushort glyphIndex) =>
        _hmtx.GetAdvanceWidth(glyphIndex);

    // ── IDisposable ──

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _sfnt.Dispose();
        }
    }
}
