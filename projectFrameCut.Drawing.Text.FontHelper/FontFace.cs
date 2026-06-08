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
    private readonly CffTable? _cff;
    private readonly VariationEngine? _variation;
    private readonly IReadOnlySet<TargetLanguage> _targetLanguages;
    private readonly IReadOnlyDictionary<TargetLanguage, string> _localizedNames;
    private bool _disposed;

    /// <summary>Whether this font uses CFF outlines (PostScript-based OTF).</summary>
    public bool IsCff => _cff != null;

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

        // Parse CFF table if present (CFF-based OTFs)
        _cff = sfnt.HasTable("CFF ")
            ? CffTable.Load(sfnt.GetTableDataCopy("CFF "), _maxp.NumGlyphs)
            : null;

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

        // Variable font support: initialize VariationEngine if fvar table exists
        _variation = sfnt.HasTable("fvar")
            ? new VariationEngine(this, sfnt)
            : null;
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

    public static FontFace[] AutoLoad(string path)
        => AutoLoad(File.ReadAllBytes(path));

    public static FontFace[] AutoLoad(byte[] data)
    {
        List<FontFace> fonts = new();
        bool ttfSeen = false, ttcSeen = false;
    ttf:
        try
        {
            ttfSeen = true;
            return new[] { Load(data) };
        }
        catch
        {
            if (!ttcSeen) goto ttc;
            else if(!ttfSeen) throw;
        }
    ttc:
        try
        {
            ttcSeen = true;
            return FontCollection.Load(data).Select(C => C.Load()).ToArray();
        }
        catch
        {
            if (!ttfSeen) goto ttf;
            else if(!ttcSeen) throw;
        }
        throw new InvalidDataException("Data is not a valid TTF or TTC font.");
    }


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

    // ── Variable font support ──

    /// <summary>Whether this font supports OpenType Font Variations (variable font).</summary>
    public bool IsVariableFont => _variation?.IsVariable ?? false;

    /// <summary>The variation axes declared by this font (if variable).</summary>
    public IReadOnlyList<VariationAxis> VariationAxes =>
        _variation?.Axes ?? Array.Empty<VariationAxis>();

    /// <summary>The named instances declared by this font (if variable).</summary>
    public IReadOnlyList<NamedInstance> NamedInstances =>
        _variation?.Instances ?? Array.Empty<NamedInstance>();

    /// <summary>Set a single variation axis by tag.</summary>
    public bool TrySetVariationAxis(string axisTag, float value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _variation?.TrySetAxis(axisTag, value) ?? false;
    }

    /// <summary>Set multiple variation axes at once. Resets unlisted axes to defaults.</summary>
    public void SetVariationAxes(IReadOnlyDictionary<string, float> axes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _variation?.SetAxes(axes);
    }

    /// <summary>Get the current value of a variation axis (denormalized).</summary>
    public float GetVariationAxis(string axisTag) =>
        _variation?.GetAxis(axisTag) ?? 0f;

    /// <summary>
    /// Get a glyph with current variations applied.
    /// For non-variable fonts or default axis values, returns the base glyph.
    /// </summary>
    public Glyph? GetVariedGlyph(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_variation == null)
            return GetGlyph(glyphIndex);

        return _variation.GetVariedGlyph(glyphIndex);
    }

    /// <summary>
    /// Get the advance width for a glyph with current variations applied.
    /// </summary>
    public ushort GetVariedAdvanceWidth(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_variation == null)
            return GetAdvanceWidth(glyphIndex);

        return _variation.GetVariedAdvanceWidth(glyphIndex);
    }

    // ── Glyph access ──

    public ushort GetGlyphIndex(char unicodeCodepoint) =>
        _cmap.GetGlyphIndex((uint)unicodeCodepoint);

    public Glyph? GetGlyph(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (glyphIndex >= _maxp.NumGlyphs)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        // CFF-based font
        if (_cff != null)
        {
            Glyph? glyph = _cff.ParseGlyph(glyphIndex);
            if (glyph != null)
            {
                glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
                glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
            }
            return glyph;
        }

        // TrueType-based font
        if (_loca == null || _glyfData == null)
            return null;

        return ParseAndMeasureGlyph(glyphIndex, null);
    }

    /// <summary>
    /// Parse a glyph with an optional variation applier callback.
    /// For composite glyphs, the callback is invoked for each component glyph
    /// during recursive decomposition, ensuring variable font variations are
    /// applied at the component level.
    /// </summary>
    internal Glyph? GetGlyphWithVariation(
        ushort glyphIndex,
        GlyfTable.VariationApplier variationApplier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (glyphIndex >= _maxp.NumGlyphs)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        // CFF fonts don't support gvar-style variations (use CFF2 instead)
        if (_cff != null)
        {
            Glyph? glyph = _cff.ParseGlyph(glyphIndex);
            if (glyph != null)
            {
                glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
                glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
            }
            return glyph;
        }

        if (_loca == null || _glyfData == null)
            return null;

        return ParseAndMeasureGlyph(glyphIndex, variationApplier);
    }

    private Glyph? ParseAndMeasureGlyph(
        ushort glyphIndex,
        GlyfTable.VariationApplier? variationApplier)
    {
        LocaData loca = _loca!.Value;
        Glyph? glyph = GlyfTable.ParseGlyph(
            _glyfData!, loca, glyphIndex, _sfnt, _maxp, 0,
            variationApplier);

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
