using projectFrameCut.Drawing.Text.FontHelper.Reader;
using projectFrameCut.Drawing.Text.FontHelper.Table;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace projectFrameCut.Drawing.Text.FontHelper;

/// <summary>Represents a loaded TrueType/OpenType/CFF font face with glyph access and variable font support.</summary>
[DebuggerDisplay("{DisplayName} ({UniqueName})")]
public sealed class FontFace : IDisposable
{
    /// <summary>
    /// Optional process-wide font used exclusively for Emoji shaping and colour rendering.
    /// The caller owns the instance and must not dispose it while layout is in progress.
    /// </summary>
    public static FontFace? EmojiFont { get; set; }
    private static readonly object s_registryLock = new();
    private static readonly List<WeakReference<FontFace>> s_registry = [];
    private static readonly Timer s_autoDisposeTimer = new(
        static _ => SweepAutoDisposeCandidates(), null,
        TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(120));
    static FontFace()
    {
        _ = s_autoDisposeTimer;
    }

    private SfntReader? _sfnt;
    private readonly object _stateLock = new();
    private DateTime _lastAccessUtc;
    private readonly string? _sourcePath;
    private readonly int _sfntOffset;
    private readonly HeadData _head;
    private readonly MaxpData _maxp;
    private readonly HmtxData _hmtx;
    private readonly NameData _name;
    private readonly Os2Data _os2;
    private readonly CmapData _cmap;
    private readonly LocaData? _loca;
    private readonly bool _hasGlyf;
    private readonly bool _hasCff;
    private CffTable? _cff;
    private readonly bool _hasFvar;
    private VariationEngine? _variation;
    private bool _variationInitialized;
    private bool _isVariableFont;
    private readonly IReadOnlySet<TargetLanguage> _targetLanguages;
    private readonly IReadOnlyDictionary<TargetLanguage, string> _localizedNames;
    private bool _disposed;
    private ColorEmojiTables? _colorEmoji;
    private GsubEmojiTable? _emojiGsub;
    private GposEmojiTable? _emojiGpos;
    private bool _emojiTablesInitialized;

    /// <summary>Global switch for inactivity-based automatic unloading.</summary>
    public static bool GlobalAutoDisposeEnabled { get; set; } = true;

    /// <summary>Idle duration before automatic unloading is triggered.</summary>
    public static TimeSpan AutoDisposeIdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Per-font switch for inactivity-based automatic unloading.</summary>
    public bool AutoDisposeEnabled { get; set { if(!SupportsAutoReloadFromDisk) throw new InvalidOperationException("AutoDispose can only be enabled for fonts that support auto-reload from disk."); field = value; } } = true;

    /// <summary>Whether this font instance can reload itself from disk after auto-unload.</summary>
    public bool SupportsAutoReloadFromDisk => _sourcePath is not null;

    /// <summary>Whether this font uses CFF outlines (PostScript-based OTF).</summary>
    public bool IsCff => _hasCff;

    internal FontFace(SfntReader sfnt)
    {
        _sfnt = sfnt;
        _sourcePath = sfnt.SourcePath;
        _sfntOffset = sfnt.SfntOffset;
        _lastAccessUtc = DateTime.UtcNow;
        RegisterForAutoDispose(this);

        // Parse all tables (required ones throw if missing)
        _head = HeadTable.Parse(sfnt.GetTableData("head"));
        _maxp = MaxpTable.Parse(sfnt.GetTableData("maxp"));

        HheaData hhea = HheaTable.Parse(sfnt.GetTableData("hhea"));
        _hmtx = HmtxTable.Parse(
            sfnt.GetTableData("hmtx"),
            hhea.NumOfLongHorMetrics,
            _maxp.NumGlyphs);

        // CFF-based OpenType fonts (OTTO) use "CFF " instead of "loca"+"glyf"
        _hasGlyf = sfnt.HasTable("glyf");
        _hasCff = sfnt.HasTable("CFF ");

        _loca = sfnt.HasTable("loca")
            ? LocaTable.Parse(sfnt.GetTableData("loca"), _maxp.NumGlyphs, _head.IndexToLocFormat)
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
        _hasFvar = sfnt.HasTable("fvar");


    }

    /// <summary>Load a font face from a file path.</summary>
    public static FontFace Load(string path) =>
        new(SfntReader.Load(path));

    /// <summary>Load a font face from raw font data.</summary>
    public static FontFace Load(byte[] data) =>
        new(SfntReader.Load(data));

    // ── TrueType Collection (.ttc) support ──

    /// <summary>Open a TrueType Collection (.ttc) from a file path.</summary>
    public static FontCollection OpenTtcCollection(string path) =>
        FontCollection.Load(path);

    /// <summary>Open a TrueType Collection (.ttc) from raw data.</summary>
    public static FontCollection OpenTtcCollection(byte[] data) =>
        FontCollection.Load(data);

    /// <summary>Auto-detect and load fonts from a file path (supports .ttf, .otf, .ttc).</summary>
    public static FontFace[] AutoLoad(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        bool ttfSeen = false, ttcSeen = false;
        if (extension == ".ttc") goto ttc;
        else if (extension == ".otf" || extension == ".ttf") goto ttf;

    ttf:
        try
        {
            ttfSeen = true;
            return [Load(path)];
        }
        catch (Exception ex)
        {
            if (!ttcSeen) goto ttc;
            else if (!ttfSeen) throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.", ex);
        }

    ttc:
        try
        {
            ttcSeen = true;
            return FontCollection.Load(path).Select(c => c.Load()).ToArray();
        }
        catch (Exception ex)
        {
            if (!ttfSeen) goto ttf;
            else if (!ttcSeen) throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.", ex);
        }

        throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.");
    }

    /// <summary>Auto-detect and load fonts from raw data (supports TTF, OTF, TTC).</summary>
    /// <param name="preferExtension">The preferred file extension to prioritize during auto-detection. Keep empty to auto-detect.</param>
    public static FontFace[] AutoLoad(byte[] data, string preferExtension = "")
    {
        bool ttfSeen = false, ttcSeen = false;
        if (preferExtension == ".ttc") goto ttc;
        else if (preferExtension == ".otf" || preferExtension == ".ttf") goto ttf;
    ttf:
        try
        {
            ttfSeen = true;
            var f = Load(data);
            return [f];
        }
        catch (Exception ex)
        {
            if (!ttcSeen) goto ttc;
            else if (!ttfSeen) throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.", ex);
        }
    ttc:
        try
        {
            ttcSeen = true;
            return FontCollection.Load(data).Select(C => C.Load()).ToArray();
        }
        catch (Exception ex)
        {
            if (!ttfSeen) goto ttf;
            else if (!ttcSeen) throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.", ex);
        }

        throw new InvalidDataException("Data is not a valid TTF or TTC or OTF font.");
    }


    // ── Font metadata ──

    /// <summary>The font family name (e.g., "Arial").</summary>
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

    /// <summary>A unique identifier for this font (Name ID 3), e.g. "Monotype:Arial Bold:1990".</summary>
    public string? UniqueName => _name.GetName(3);

    /// <summary>The font subfamily name (e.g., "Regular", "Bold").</summary>
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

    /// <summary>
    /// The BCP-47 language tag of the font's primary target language.
    /// Follows the same non-English preference logic as <see cref="DisplayName"/>.
    /// Returns "und" (undetermined) when no specific language can be determined.
    /// </summary>
    public string PrimaryLanguageTag
    {
        get
        {
            foreach (var (lang, _) in _localizedNames)
            {
                if (lang.PlatformId == 3 && lang.LanguageId == 0x0409) continue;
                if (lang.PlatformId == 1 && lang.LanguageId == 0) continue;
                return lang.ToBcp47Tag();
            }
            return "und";
        }
    }

    /// <summary>Number of font units per EM square.</summary>
    public ushort UnitsPerEm => _head.UnitsPerEm;
    /// <summary>Total number of glyphs in the font.</summary>
    public int GlyphCount => _maxp.NumGlyphs;
    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic => _os2.IsItalic || _head.IsItalic;
    /// <summary>Font weight class (100 = Thin, 400 = Regular, 700 = Bold).</summary>
    public ushort WeightClass => _os2.WeightClass;

    // ── Variable font support ──

    /// <summary>Whether this font supports OpenType Font Variations (variable font).</summary>
    public bool IsVariableFont
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            TouchAccess();
            var variation = EnsureVariationEngineLoaded();
            return variation?.IsVariable ?? false;
        }
    }

    /// <summary>The variation axes declared by this font (if variable).</summary>
    public IReadOnlyList<VariationAxis> VariationAxes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            TouchAccess();
            return EnsureVariationEngineLoaded()?.Axes ?? Array.Empty<VariationAxis>();
        }
    }

    /// <summary>The named instances declared by this font (if variable).</summary>
    public IReadOnlyList<NamedInstance> NamedInstances
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            TouchAccess();
            return EnsureVariationEngineLoaded()?.Instances ?? Array.Empty<NamedInstance>();
        }
    }

    /// <summary>Set a single variation axis by tag.</summary>
    public bool TrySetVariationAxis(string axisTag, float value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return EnsureVariationEngineLoaded()?.TrySetAxis(axisTag, value) ?? false;
    }

    /// <summary>Set multiple variation axes at once. Resets unlisted axes to defaults.</summary>
    public void SetVariationAxes(IReadOnlyDictionary<string, float> axes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        EnsureVariationEngineLoaded()?.SetAxes(axes);
    }

    /// <summary>Get the current value of a variation axis (denormalized).</summary>
    public float GetVariationAxis(string axisTag)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return EnsureVariationEngineLoaded()?.GetAxis(axisTag) ?? 0f;
    }

    /// <summary>
    /// Get a glyph with current variations applied.
    /// For non-variable fonts or default axis values, returns the base glyph.
    /// </summary>
    public Glyph? GetVariedGlyph(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var variation = EnsureVariationEngineLoaded();
        if (variation == null)
            return GetGlyph(glyphIndex);

        return variation.GetVariedGlyph(glyphIndex);
    }

    /// <summary>
    /// Get the advance width for a glyph with current variations applied.
    /// </summary>
    public ushort GetVariedAdvanceWidth(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var variation = EnsureVariationEngineLoaded();
        if (variation == null)
            return GetAdvanceWidth(glyphIndex);

        return variation.GetVariedAdvanceWidth(glyphIndex);
    }

    // ── Glyph access ──

    /// <summary>Get the glyph index for a Unicode codepoint.</summary>
    public ushort GetGlyphIndex(char unicodeCodepoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _cmap.GetGlyphIndex((uint)unicodeCodepoint);
    }

    /// <summary>Get the glyph index for a Unicode scalar value.</summary>
    public ushort GetGlyphIndex(Rune unicodeCodepoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _cmap.GetGlyphIndex((uint)unicodeCodepoint.Value);
    }

    /// <summary>Get a variation-selector glyph, or zero when the sequence is unsupported.</summary>
    public ushort GetGlyphIndex(Rune unicodeCodepoint, Rune variationSelector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _cmap.GetGlyphIndex((uint)unicodeCodepoint.Value, (uint)variationSelector.Value);
    }

    /// <summary>Checks whether the font can actually display the specified character
    /// (i.e., it maps to a real glyph rather than the .notdef glyph).</summary>
    public bool CanDisplayTheChar(char ch)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _cmap.GetGlyphIndex(ch) != 0;
    }

    /// <summary>Checks whether this font maps the specified Unicode scalar to a real glyph.</summary>
    public bool CanDisplayTheChar(Rune rune)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _cmap.GetGlyphIndex((uint)rune.Value) != 0;
    }

    internal bool HasTable(string tag) => EnsureSfntLoaded().HasTable(tag);
    internal byte[] GetTableBytes(string tag) => EnsureSfntLoaded().GetTableData(tag).ToArray();

    internal bool TryShapeColorEmoji(string textElement, ColorValue foreground,
        out ushort glyphIndex, out ColorGlyphLayer[] layers, out int shapedAdvanceWidth)
    {
        glyphIndex = 0; layers = []; shapedAdvanceWidth = 0;
        EnsureEmojiTables();
        if (_colorEmoji is null || string.IsNullOrEmpty(textElement)) return false;

        var glyphs = new List<ushort>();
        Rune? pending = null;
        foreach (var rune in textElement.EnumerateRunes())
        {
            if (rune.Value is 0xFE0E or 0xFE0F)
            {
                if (pending.HasValue)
                {
                    ushort varied = GetGlyphIndex(pending.Value, rune);
                    glyphs.Add(varied != 0 ? varied : GetGlyphIndex(pending.Value));
                    pending = null;
                }
                continue;
            }
            if (pending.HasValue) glyphs.Add(GetGlyphIndex(pending.Value));
            pending = rune;
        }
        if (pending.HasValue) glyphs.Add(GetGlyphIndex(pending.Value));
        if (glyphs.Count == 0 || glyphs.Any(g => g == 0))
        {
            Debug.WriteLine($"[Emoji] cmap failed for cluster '{textElement}': [{string.Join(",", glyphs)}]");
            return false;
        }

        ushort[] shaped;
        if (glyphs.Count == 1) shaped = [glyphs[0]];
        else if (_emojiGsub is null || !_emojiGsub.TryShape(glyphs, out shaped))
        {
            Debug.WriteLine($"[Emoji] GSUB could not compose cluster '{textElement}', input glyphs=[{string.Join(",", glyphs)}].");
            return false;
        }
        glyphIndex = shaped[0];
        var combined = new List<ColorGlyphLayer>();
        float cursor = 0;
        int[] baseAdvances = shaped.Select(g => (int)GetVariedAdvanceWidth(g)).ToArray();
        GlyphPosition[] positions = _emojiGpos?.Position(shaped, baseAdvances) ?? new GlyphPosition[shaped.Length];
        for (int shapedIndex = 0; shapedIndex < shaped.Length; shapedIndex++)
        {
            ushort shapedGlyph = shaped[shapedIndex];
            if (!_colorEmoji.TryGetLayers(shapedGlyph, foreground, out var glyphLayers))
            {
                Debug.WriteLine($"[Emoji] COLR has no renderable paint graph for cluster '{textElement}', glyph={shapedGlyph}.");
                return false;
            }
            GlyphPosition position = positions[shapedIndex];
            var translation = System.Numerics.Matrix3x2.CreateTranslation(
                cursor + position.XPlacement, -position.YPlacement);
            foreach (var layer in glyphLayers)
                combined.Add(layer with
                {
                    GlyphTransform = layer.GlyphTransform * translation,
                    BrushTransform = layer.BrushTransform * translation,
                });
            int advance = baseAdvances[shapedIndex] + position.XAdvance;
            shapedAdvanceWidth += advance;
            cursor += advance;
        }
        if (combined.Count == 0)
        {
            Debug.WriteLine($"[Emoji] COLR produced no layers for cluster '{textElement}'.");
            return false;
        }
        layers = combined.ToArray();
        return true;
    }

    internal bool TryGetColorLayers(ushort glyphIndex, ColorValue foreground, out ColorGlyphLayer[] layers)
    {
        EnsureEmojiTables();
        if (_colorEmoji is not null) return _colorEmoji.TryGetLayers(glyphIndex, foreground, out layers);
        layers = []; return false;
    }

    private void EnsureEmojiTables()
    {
        if (_emojiTablesInitialized) return;
        lock (_stateLock)
        {
            if (_emojiTablesInitialized) return;
            var sfnt = EnsureSfntLoaded();
            if (sfnt.HasTable("COLR") && sfnt.HasTable("CPAL"))
                _colorEmoji = new ColorEmojiTables(sfnt.GetTableData("COLR").ToArray(), sfnt.GetTableData("CPAL").ToArray());
            if (sfnt.HasTable("GSUB"))
                _emojiGsub = new GsubEmojiTable(sfnt.GetTableData("GSUB").ToArray());
            if (sfnt.HasTable("GPOS"))
                _emojiGpos = new GposEmojiTable(sfnt.GetTableData("GPOS").ToArray());
            _emojiTablesInitialized = true;
        }
    }

    /// <summary>Parse and return the glyph at the given index.</summary>
    public Glyph? GetGlyph(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();

        if (glyphIndex >= _maxp.NumGlyphs)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        // CFF-based font
        if (_hasCff)
        {
            Glyph? glyph = GetOrLoadCff().ParseGlyph(glyphIndex);
            if (glyph != null)
            {
                glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
                glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
            }
            return glyph;
        }

        // TrueType-based font
        if (_loca == null || !_hasGlyf)
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
        TouchAccess();

        if (glyphIndex >= _maxp.NumGlyphs)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        // CFF fonts don't support gvar-style variations (use CFF2 instead)
        if (_hasCff)
        {
            Glyph? glyph = GetOrLoadCff().ParseGlyph(glyphIndex);
            if (glyph != null)
            {
                glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
                glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
            }
            return glyph;
        }

        if (_loca == null || !_hasGlyf)
            return null;

        return ParseAndMeasureGlyph(glyphIndex, variationApplier);
    }

    private Glyph? ParseAndMeasureGlyph(
        ushort glyphIndex,
        GlyfTable.VariationApplier? variationApplier)
    {
        LocaData loca = _loca!.Value;
        SfntReader sfnt = EnsureSfntLoaded();
        ReadOnlySpan<byte> glyfData = sfnt.GetTableData("glyf");
        Glyph? glyph = GlyfTable.ParseGlyph(
            glyfData, loca, glyphIndex, sfnt, _maxp, 0,
            variationApplier);

        if (glyph != null)
        {
            glyph.AdvanceWidth = _hmtx.GetAdvanceWidth(glyphIndex);
            glyph.LeftSideBearing = _hmtx.GetLeftSideBearing(glyphIndex);
        }

        return glyph;
    }

    /// <summary>Get the advance width for a glyph.</summary>
    public ushort GetAdvanceWidth(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();
        return _hmtx.GetAdvanceWidth(glyphIndex);
    }

    /// <summary>
    /// Fast retrieval of a glyph's bounding box (in font design units) without
    /// parsing the full contour data.  Returns <c>false</c> for empty or missing
    /// glyphs.
    /// </summary>
    /// <remarks>
    /// For TrueType outlines the bounding box is read directly from the 10-byte
    /// glyph header.  For CFF-based fonts the full glyph is parsed because CFF
    /// charstrings do not carry a header-level bbox.
    /// </remarks>
    public bool TryGetGlyphBounds(
        ushort glyphIndex,
        out short xMin, out short yMin, out short xMax, out short yMax)
    {
        TouchAccess();
        xMin = yMin = xMax = yMax = 0;

        if (glyphIndex >= _maxp.NumGlyphs)
            return false;

        // CFF-based font — no header-level bbox; fall back to full parse
        if (_hasCff)
        {
            Glyph? glyph = GetOrLoadCff().ParseGlyph(glyphIndex);
            if (glyph is null || glyph.IsEmpty)
                return false;
            xMin = glyph.XMin;
            yMin = glyph.YMin;
            xMax = glyph.XMax;
            yMax = glyph.YMax;
            return true;
        }

        // TrueType-based font — read the 10-byte glyph header
        if (_loca is null || !_hasGlyf)
            return false;

        ReadOnlySpan<byte> glyfData = EnsureSfntLoaded().GetTableData("glyf");

        uint offset = _loca.Value.GetGlyphOffset(glyphIndex);
        uint length = _loca.Value.GetGlyphLength(glyphIndex);
        if (length < 10 || offset + length > (uint)glyfData.Length)
            return false;

        int headerOffset = (int)offset;
        short numberOfContours = BigEndianReader.ReadInt16(glyfData, ref headerOffset);
        if (numberOfContours == 0)
            return false;

        xMin = BigEndianReader.ReadInt16(glyfData, ref headerOffset);
        short rawYMin = BigEndianReader.ReadInt16(glyfData, ref headerOffset);
        xMax = BigEndianReader.ReadInt16(glyfData, ref headerOffset);
        short rawYMax = BigEndianReader.ReadInt16(glyfData, ref headerOffset);

        // Y-flip to image space (Y-down)
        yMin = (short)(-rawYMax);
        yMax = (short)(-rawYMin);

        return true;
    }


    public ushort GetAdvanceWidthViaBounds(ushort glyphIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchAccess();

        if (TryGetGlyphBounds(glyphIndex, out var xMin, out _, out var xMax, out _))
        {
            return (ushort)Math.Max(0, xMax - xMin);
        }
        else
        {
            return GetAdvanceWidth(glyphIndex);
        }
    }


    private CffTable GetOrLoadCff()
    {
        if (_cff is not null)
            return _cff;

        if (!_hasCff)
            throw new InvalidOperationException("Current font does not contain a CFF table.");

        _cff = CffTable.Load(EnsureSfntLoaded().GetTableData("CFF ").ToArray(), _maxp.NumGlyphs);
        return _cff;
    }

    private SfntReader EnsureSfntLoaded()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _lastAccessUtc = DateTime.UtcNow;

            if (_sfnt is not null)
                return _sfnt;

            if (_sourcePath is null)
                throw new InvalidOperationException(
                    "Font data was loaded from memory and cannot be automatically reloaded from disk.");

            _sfnt = SfntReader.ReloadFromFile(_sourcePath, _sfntOffset);
            return _sfnt;
        }
    }

    private void TouchAccess()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _lastAccessUtc = DateTime.UtcNow;
        }
    }

    private VariationEngine? EnsureVariationEngineLoaded()
    {
        if (_variationInitialized)
            return _isVariableFont ? _variation : null;

        _variationInitialized = true;
        if (!_hasFvar)
        {
            _isVariableFont = false;
            return null;
        }

        _variation = new VariationEngine(this, EnsureSfntLoaded());
        _isVariableFont = _variation.IsVariable;
        return _isVariableFont ? _variation : null;
    }

    private static void RegisterForAutoDispose(FontFace font)
    {
        lock (s_registryLock)
        {
            s_registry.Add(new WeakReference<FontFace>(font));
        }
    }

    internal static void SweepAutoDisposeCandidates(bool ignoreAllConditions = false)
    {
        if (!ignoreAllConditions && (!GlobalAutoDisposeEnabled || AutoDisposeIdleTimeout <= TimeSpan.Zero))
            return;

        lock (s_registryLock)
        {
            for (int i = s_registry.Count - 1; i >= 0; i--)
            {
                if (!s_registry[i].TryGetTarget(out var font))
                {
                    s_registry.RemoveAt(i);
                    continue;
                }

                font.TryAutoUnload();
            }
        }
    }

    /// <summary>Immediately runs one auto-dispose sweep across all tracked font faces.</summary>
    public static void RunAutoDisposeSweep() => SweepAutoDisposeCandidates();

    private void TryAutoUnload()
    {
        lock (_stateLock)
        {
            if (_disposed || !AutoDisposeEnabled || _sourcePath is null || _sfnt is null)
                return;

            if (DateTime.UtcNow - _lastAccessUtc < AutoDisposeIdleTimeout)
                return;

            _sfnt.Dispose();
            _sfnt = null;
            _cff = null;
            _variation = null;
            _variationInitialized = false;
            _colorEmoji = null;
            _emojiGsub = null;
            _emojiGpos = null;
            _emojiTablesInitialized = false;
        }
    }


    // ── IDisposable ──

    /// <summary>Release all resources held by this font face.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            lock (_stateLock)
            {
                _sfnt?.Dispose();
                _sfnt = null;
                _cff = null;
                _variation = null;
                _variationInitialized = false;
                _colorEmoji = null;
                _emojiGsub = null;
                _emojiGpos = null;
                _emojiTablesInitialized = false;
            }
        }
    }
}
