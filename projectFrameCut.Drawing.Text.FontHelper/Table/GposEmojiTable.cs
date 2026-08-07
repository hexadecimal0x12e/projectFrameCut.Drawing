namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal readonly record struct GlyphPosition(short XPlacement, short YPlacement, short XAdvance, short YAdvance)
{
    public GlyphPosition Add(GlyphPosition other) => new(
        (short)(XPlacement + other.XPlacement), (short)(YPlacement + other.YPlacement),
        (short)(XAdvance + other.XAdvance), (short)(YAdvance + other.YAdvance));
}

/// <summary>Executes the DFLT dist positioning used by Segoe UI Emoji compositions.</summary>
internal sealed class GposEmojiTable
{
    private readonly byte[] _data;
    private readonly int[] _lookups;
    public GposEmojiTable(byte[] data)
    {
        _data = data;
        _lookups = ReadFeatureLookups("dist").Concat(ReadFeatureLookups("mark")).Distinct().ToArray();
    }

    public GlyphPosition[] Position(IReadOnlyList<ushort> glyphs, IReadOnlyList<int> baseAdvances)
    {
        var result = new GlyphPosition[glyphs.Count];
        try
        {
            int list = U16(8), count = U16(list);
            foreach (int index in _lookups)
            {
                if ((uint)index >= (uint)count) continue;
                int lookup = list + U16(list + 2 + index * 2);
                ApplyLookup(lookup, glyphs, baseAdvances, result, null);
            }
        }
        catch { }
        return result;
    }

    private bool ApplyLookup(int lookup, IReadOnlyList<ushort> glyphs, IReadOnlyList<int> baseAdvances,
        GlyphPosition[] positions, int? target)
    {
        int type = U16(lookup), count = U16(lookup + 4); bool changed = false;
        if (type == 8 && target is null)
            return ApplyContextLookup(lookup, count, glyphs, baseAdvances, positions);
        for (int i = 0; i < count; i++)
        {
            int sub = lookup + U16(lookup + 6 + i * 2);
            if (type == 1) changed |= ApplySingle(sub, glyphs, positions, target);
            else if (type == 4) changed |= ApplyMarkToBase(sub, glyphs, baseAdvances, positions);
        }
        return changed;
    }

    private bool ApplySingle(int sub, IReadOnlyList<ushort> glyphs, GlyphPosition[] positions, int? target)
    {
        int format = U16(sub), valueFormat = U16(sub + 4);
        int first = target ?? 0, last = target ?? (glyphs.Count - 1); bool changed = false;
        for (int i = first; i <= last && i < glyphs.Count; i++)
        {
            int coverage = CoverageIndex(sub + U16(sub + 2), glyphs[i]); if (coverage < 0) continue;
            int valueSize = 2 * System.Numerics.BitOperations.PopCount((uint)valueFormat);
            int value = format == 1 ? sub + 6 : sub + 8 + coverage * valueSize;
            positions[i] = positions[i].Add(ReadValue(value, valueFormat)); changed = true;
        }
        return changed;
    }

    private bool ApplyContextLookup(int lookup, int subtableCount, IReadOnlyList<ushort> glyphs,
        IReadOnlyList<int> baseAdvances, GlyphPosition[] positions)
    {
        bool changed = false;
        for (int pos = 0; pos < glyphs.Count;)
        {
            bool matched = false;
            for (int i = 0; i < subtableCount; i++)
            {
                int sub = lookup + U16(lookup + 6 + i * 2);
                if (TryApplyContext3At(sub, pos, glyphs, baseAdvances, positions, out int consumed, out bool adjusted))
                {
                    matched = true; changed |= adjusted; pos += Math.Max(1, consumed); break;
                }
            }
            if (!matched) pos++;
        }
        return changed;
    }

    private bool TryApplyContext3At(int sub, int pos, IReadOnlyList<ushort> glyphs,
        IReadOnlyList<int> baseAdvances, GlyphPosition[] positions, out int consumed, out bool changed)
    {
        consumed = 1; changed = false;
        if (U16(sub) != 3) return false;
        int o = sub + 2, backCount = U16(o); o += 2;
        var back = ReadCoverageOffsets(sub, ref o, backCount);
        int inputCount = U16(o); o += 2; var input = ReadCoverageOffsets(sub, ref o, inputCount);
        int lookCount = U16(o); o += 2; var look = ReadCoverageOffsets(sub, ref o, lookCount);
        int recordCount = U16(o); o += 2;
        var records = new (int Sequence, int Lookup)[recordCount];
        for (int i = 0; i < recordCount; i++, o += 4) records[i] = (U16(o), U16(o + 2));
        if (pos < backCount || pos + inputCount + lookCount > glyphs.Count) return false;
        bool match = true;
        for (int i = 0; i < backCount && match; i++) match = CoverageIndex(back[i], glyphs[pos - 1 - i]) >= 0;
        for (int i = 0; i < inputCount && match; i++) match = CoverageIndex(input[i], glyphs[pos + i]) >= 0;
        for (int i = 0; i < lookCount && match; i++) match = CoverageIndex(look[i], glyphs[pos + inputCount + i]) >= 0;
        if (!match) return false;
        int list = U16(8), count = U16(list);
        foreach (var record in records)
        {
            if ((uint)record.Lookup >= (uint)count) continue;
            int nested = list + U16(list + 2 + record.Lookup * 2);
            changed |= ApplyLookup(nested, glyphs, baseAdvances, positions, pos + record.Sequence);
        }
        consumed = inputCount;
        return true;
    }

    private bool ApplyMarkToBase(int sub, IReadOnlyList<ushort> glyphs, IReadOnlyList<int> advances,
        GlyphPosition[] positions)
    {
        if (U16(sub) != 1) return false;
        int markCoverage = sub + U16(sub + 2), baseCoverage = sub + U16(sub + 4);
        int classCount = U16(sub + 6), markArray = sub + U16(sub + 8), baseArray = sub + U16(sub + 10);
        bool changed = false;
        for (int markIndex = 1; markIndex < glyphs.Count; markIndex++)
        {
            int mi = CoverageIndex(markCoverage, glyphs[markIndex]); if (mi < 0) continue;
            int baseIndex = markIndex - 1, bi = -1;
            while (baseIndex >= 0 && (bi = CoverageIndex(baseCoverage, glyphs[baseIndex])) < 0) baseIndex--;
            if (bi < 0) continue;
            int markRecord = markArray + 2 + mi * 4, markClass = U16(markRecord);
            if (markClass >= classCount) continue;
            int markAnchor = markArray + U16(markRecord + 2);
            int baseAnchorRecord = baseArray + 2 + (bi * classCount + markClass) * 2;
            int baseAnchorOffset = U16(baseAnchorRecord); if (baseAnchorOffset == 0) continue;
            int baseAnchor = baseArray + baseAnchorOffset;
            int basePen = 0, markPen = 0;
            for (int i = 0; i < markIndex; i++)
            {
                if (i < baseIndex) basePen += advances[i] + positions[i].XAdvance;
                markPen += advances[i] + positions[i].XAdvance;
            }
            short dx = (short)(basePen + positions[baseIndex].XPlacement + S16(baseAnchor + 2)
                - markPen - S16(markAnchor + 2));
            short dy = (short)(positions[baseIndex].YPlacement + S16(baseAnchor + 4) - S16(markAnchor + 4));
            positions[markIndex] = positions[markIndex].Add(new(dx, dy, 0, 0)); changed = true;
        }
        return changed;
    }

    private int[] ReadFeatureLookups(string wanted)
    {
        try
        {
            int list = U16(6), count = U16(list); var result = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int r = list + 2 + i * 6;
                if (System.Text.Encoding.ASCII.GetString(_data, r, 4) != wanted) continue;
                int f = list + U16(r + 4), n = U16(f + 2);
                for (int j = 0; j < n; j++) { int v = U16(f + 4 + j * 2); if (!result.Contains(v)) result.Add(v); }
            }
            return result.ToArray();
        }
        catch { return []; }
    }

    private int[] ReadCoverageOffsets(int root, ref int o, int count)
    {
        var a = new int[count]; for (int i = 0; i < count; i++, o += 2) a[i] = root + U16(o); return a;
    }

    private GlyphPosition ReadValue(int o, int format)
    {
        short[] v = new short[4];
        for (int bit = 0; bit < 16; bit++) if ((format & (1 << bit)) != 0)
        { short value = S16(o); o += 2; if (bit < 4) v[bit] = value; }
        return new(v[0], v[1], v[2], v[3]);
    }

    private int CoverageIndex(int p, ushort glyph)
    {
        int format = U16(p), count = U16(p + 2);
        if (format == 1) for (int i = 0; i < count; i++) { ushort g = U16(p + 4 + i * 2); if (g == glyph) return i; if (g > glyph) break; }
        else if (format == 2) for (int i = 0; i < count; i++) { int r = p + 4 + i * 6; ushort a = U16(r), z = U16(r + 2); if (glyph >= a && glyph <= z) return U16(r + 4) + glyph - a; }
        return -1;
    }

    private ushort U16(int o) => (ushort)((_data[o] << 8) | _data[o + 1]);
    private short S16(int o) => unchecked((short)U16(o));
}
