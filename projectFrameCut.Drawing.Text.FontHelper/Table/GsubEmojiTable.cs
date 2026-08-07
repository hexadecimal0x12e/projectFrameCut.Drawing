namespace projectFrameCut.Drawing.Text.FontHelper.Table;

/// <summary>Small, bounded GSUB executor focused on ligature substitutions used by Emoji fonts.</summary>
internal sealed class GsubEmojiTable
{
    private readonly byte[] _data;
    private readonly int[] _lookupIndices;

    public GsubEmojiTable(byte[] data)
    {
        _data = data;
        _lookupIndices = ReadEnabledEmojiLookups();
    }

    public bool TryShape(IReadOnlyList<ushort> input, out ushort[] output)
    {
        output = [];
        if (input.Count == 0 || _data.Length < 10) return false;
        var work = input.ToList();
        try
        {
            int lookupList = U16(8);
            int count = U16(lookupList);
            // Apply only lookups referenced by Emoji-relevant features. Running every
            // lookup in the font corrupts the input before the ccmp ligature lookup.
            for (int pass = 0; pass < 8 && work.Count > 1; pass++)
            {
                bool changed = false;
                foreach (int i in _lookupIndices)
                {
                    if ((uint)i >= (uint)count) continue;
                    int lookup = lookupList + U16(lookupList + 2 + i * 2);
                    changed |= ApplyLookup(lookup, work);
                }
                if (!changed) break;
            }
            if (work.Count > 0 && !work.SequenceEqual(input))
            {
                output = work.ToArray();
                return output.All(g => g != 0);
            }
        }
        catch { }
        return false;
    }

    private int[] ReadEnabledEmojiLookups()
    {
        try
        {
            int featureList = U16(6);
            int count = U16(featureList);
            var result = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int record = featureList + 2 + i * 6;
                string tag = System.Text.Encoding.ASCII.GetString(_data, record, 4);
                if (tag is not ("ccmp" or "rlig" or "liga")) continue;
                int feature = featureList + U16(record + 4);
                int lookupCount = U16(feature + 2);
                for (int j = 0; j < lookupCount; j++)
                {
                    int index = U16(feature + 4 + j * 2);
                    if (!result.Contains(index)) result.Add(index);
                }
            }
            return result.ToArray();
        }
        catch { return []; }
    }

    private bool ApplyLookup(int lookup, List<ushort> glyphs, int? onlyPosition = null)
    {
        int type = U16(lookup), count = U16(lookup + 4);
        bool changed = false;
        for (int s = 0; s < count; s++)
        {
            int sub = lookup + U16(lookup + 6 + s * 2);
            int actualType = type;
            if (type == 7)
            {
                actualType = U16(sub + 2);
                sub += I32(sub + 4);
            }
            if (actualType == 1) changed |= ApplySingle(sub, glyphs, onlyPosition);
            else if (actualType == 4) changed |= ApplyLigature(sub, glyphs, onlyPosition);
            else if (actualType == 6) changed |= ApplyChainedContext(sub, glyphs);
        }
        return changed;
    }

    private bool ApplySingle(int sub, List<ushort> glyphs, int? onlyPosition)
    {
        int format = U16(sub), coverage = sub + U16(sub + 2); bool changed = false;
        int first = onlyPosition ?? 0, last = onlyPosition ?? (glyphs.Count - 1);
        for (int i = first; i <= last && i < glyphs.Count; i++)
        {
            int ci = CoverageIndex(coverage, glyphs[i]); if (ci < 0) continue;
            ushort replacement = format == 1
                ? (ushort)(glyphs[i] + unchecked((short)U16(sub + 4)))
                : U16(sub + 6 + ci * 2);
            if (replacement != glyphs[i]) { glyphs[i] = replacement; changed = true; }
        }
        return changed;
    }

    private bool ApplyLigature(int sub, List<ushort> glyphs, int? onlyPosition)
    {
        if (U16(sub) != 1) return false;
        int coverage = sub + U16(sub + 2), setCount = U16(sub + 4);
        int first = onlyPosition ?? 0, last = onlyPosition ?? (glyphs.Count - 1);
        for (int pos = first; pos <= last && pos < glyphs.Count; pos++)
        {
            int ci = CoverageIndex(coverage, glyphs[pos]);
            if (ci < 0 || ci >= setCount) continue;
            int set = sub + U16(sub + 6 + ci * 2), n = U16(set);
            for (int j = 0; j < n; j++)
            {
                int lig = set + U16(set + 2 + j * 2), components = U16(lig + 2);
                if (components < 2 || pos + components > glyphs.Count) continue;
                bool match = true;
                for (int k = 1; k < components; k++)
                    if (glyphs[pos + k] != U16(lig + 2 + k * 2)) { match = false; break; }
                if (!match) continue;
                glyphs[pos] = U16(lig);
                glyphs.RemoveRange(pos + 1, components - 1);
                return true;
            }
        }
        return false;
    }

    private bool ApplyChainedContext(int sub, List<ushort> glyphs)
    {
        // Segoe UI Emoji uses format 3 contextual rules to select a ligature
        // lookup for complete ZWJ sequences.
        if (U16(sub) != 3) return false;
        int o = sub + 2;
        int backCount = U16(o); o += 2;
        var back = new int[backCount];
        for (int i = 0; i < backCount; i++, o += 2) back[i] = sub + U16(o);
        int inputCount = U16(o); o += 2;
        var input = new int[inputCount];
        for (int i = 0; i < inputCount; i++, o += 2) input[i] = sub + U16(o);
        int lookCount = U16(o); o += 2;
        var look = new int[lookCount];
        for (int i = 0; i < lookCount; i++, o += 2) look[i] = sub + U16(o);
        int recordCount = U16(o); o += 2;
        var records = new (int SequenceIndex, int LookupIndex)[recordCount];
        for (int i = 0; i < recordCount; i++, o += 4)
            records[i] = (U16(o), U16(o + 2));

        for (int pos = 0; pos < glyphs.Count; pos++)
        {
            if (pos + inputCount > glyphs.Count || pos < backCount || pos + inputCount + lookCount > glyphs.Count)
                continue;
            bool match = true;
            for (int i = 0; i < backCount && match; i++)
                match = CoverageIndex(back[i], glyphs[pos - 1 - i]) >= 0;
            for (int i = 0; i < inputCount && match; i++)
                match = CoverageIndex(input[i], glyphs[pos + i]) >= 0;
            for (int i = 0; i < lookCount && match; i++)
                match = CoverageIndex(look[i], glyphs[pos + inputCount + i]) >= 0;
            if (!match) continue;

            int lookupList = U16(8), lookupCount = U16(lookupList);
            bool changed = false;
            // Records are applied in table order. Sequence indices refer to the
            // input sequence before substitutions at this contextual position.
            foreach (var record in records)
            {
                if ((uint)record.LookupIndex >= (uint)lookupCount) continue;
                int target = pos + record.SequenceIndex;
                int lookup = lookupList + U16(lookupList + 2 + record.LookupIndex * 2);
                changed |= ApplyLookup(lookup, glyphs, target);
            }
            if (changed) return true;
        }
        return false;
    }

    private int CoverageIndex(int p, ushort glyph)
    {
        int format = U16(p), count = U16(p + 2);
        if (format == 1)
        {
            int lo = 0, hi = count - 1;
            while (lo <= hi) { int m = (lo + hi) / 2; ushort g = U16(p + 4 + m * 2); if (g < glyph) lo = m + 1; else if (g > glyph) hi = m - 1; else return m; }
        }
        else if (format == 2)
        {
            for (int i = 0; i < count; i++) { int r = p + 4 + i * 6; ushort a = U16(r), z = U16(r + 2); if (glyph >= a && glyph <= z) return U16(r + 4) + glyph - a; }
        }
        return -1;
    }

    private ushort U16(int o) => (ushort)((_data[o] << 8) | _data[o + 1]);
    private int I32(int o) => (_data[o] << 24) | (_data[o + 1] << 16) | (_data[o + 2] << 8) | _data[o + 3];
}
