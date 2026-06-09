using System.Diagnostics;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

/// <summary>
/// Parses the 'CFF ' (Compact Font Format) table and provides glyph outline access
/// for OpenType fonts that use PostScript/CFF outlines instead of TrueType glyf outlines.
/// </summary>
[DebuggerNonUserCode()]
internal sealed class CffTable
{
    private readonly byte[][] _charStrings;
    private readonly byte[][] _globalSubrs;
    private readonly byte[][] _localSubrs;
    private readonly short _defaultWidthX;
    private readonly short _nominalWidthX;
    private readonly int _numGlyphs;

    // CID-keyed font support
    private readonly bool _isCID;
    private readonly byte[][][]? _fontDictSubrs;
    private readonly short[]? _fdDefaultWidthX;
    private readonly short[]? _fdNominalWidthX;
    private readonly int[]? _fdSelect;

    private CffTable(
        byte[][] charStrings,
        byte[][] globalSubrs,
        byte[][] localSubrs,
        short defaultWidthX,
        short nominalWidthX,
        int numGlyphs,
        bool isCID,
        byte[][][]? fontDictSubrs = null,
        short[]? fdDefaultWidthX = null,
        short[]? fdNominalWidthX = null,
        int[]? fdSelect = null)
    {
        _charStrings = charStrings;
        _globalSubrs = globalSubrs;
        _localSubrs = localSubrs;
        _defaultWidthX = defaultWidthX;
        _nominalWidthX = nominalWidthX;
        _numGlyphs = numGlyphs;
        _isCID = isCID;
        _fontDictSubrs = fontDictSubrs;
        _fdDefaultWidthX = fdDefaultWidthX;
        _fdNominalWidthX = fdNominalWidthX;
        _fdSelect = fdSelect;
    }

    public static CffTable Load(byte[] cffData, ushort numGlyphs)
    {
        int offset = 0;

        // ── CFF Header ──
        if (cffData.Length < 4)
            throw new InvalidFontFileException("CFF table too small.");
        byte hdrSize = cffData[2];
        if (hdrSize < 4)
            throw new InvalidFontFileException("CFF header size too small.");
        offset = hdrSize;

        // ── Name INDEX ──
        SkipIndex(cffData, ref offset);

        // ── Top DICT INDEX ──
        byte[][] topDictItems = ReadIndex(cffData, ref offset);
        if (topDictItems.Length == 0)
            throw new InvalidFontFileException("CFF Top DICT INDEX is empty.");

        var topDict = ParseDict(topDictItems[0]);

        // ── String INDEX ──
        SkipIndex(cffData, ref offset);

        // ── Global Subr INDEX ──
        _ = ReadIndex(cffData, ref offset, out byte[][] globalSubrs);
        // globalSubrs is passed to ReadIndex via out

        // ── Parse Top DICT ──
        int charStringsOffset = GetDictOffset(topDict, 17); // CharStrings operator
        bool isCID = topDict.ContainsKey(86); // FDArray operator (CID-keyed)

        // Parse CharStrings INDEX
        byte[][] charStrings = ParseIndexAt(cffData, charStringsOffset, numGlyphs);

        if (!isCID)
        {
            short defaultWidthX = 0;
            short nominalWidthX = 0;
            byte[][] localSubrs;

            if (topDict.TryGetValue(18, out var privateData) && privateData.Count >= 2)
            {
                int privateSize = (int)privateData[0];
                int privateOffset = (int)privateData[1];
                localSubrs = ParsePrivateDict(cffData, privateOffset, privateSize,
                    out defaultWidthX, out nominalWidthX);
            }
            else
            {
                localSubrs = [];
            }

            return new CffTable(charStrings, globalSubrs, localSubrs,
                defaultWidthX, nominalWidthX, numGlyphs, false);
        }
        else
        {
            // CID-keyed font
            if (!topDict.TryGetValue(36, out var fdArrayData) || fdArrayData.Count < 1)
                throw new InvalidFontFileException("CID-keyed CFF font missing FDArray.");
            int fdArrayOffset = (int)fdArrayData[0];

            if (!topDict.TryGetValue(37, out var fdSelectData) || fdSelectData.Count < 1)
                throw new InvalidFontFileException("CID-keyed CFF font missing FDSelect.");
            int fdSelectOffset = (int)fdSelectData[0];

            // Parse FDArray (INDEX of Font Dicts)
            byte[][] fontDicts = ParseIndexAt(cffData, fdArrayOffset);

            int numFDs = fontDicts.Length;
            var fdSubrs = new byte[numFDs][][];
            var fdDefaultW = new short[numFDs];
            var fdNominalW = new short[numFDs];

            for (int i = 0; i < numFDs; i++)
            {
                var fdDict = ParseDict(fontDicts[i]);
                short dw = 0, nw = 0;
                if (fdDict.TryGetValue(18, out var fdPriv) && fdPriv.Count >= 2)
                {
                    int privSize = (int)fdPriv[0];
                    int privOff = (int)fdPriv[1];
                    fdSubrs[i] = ParsePrivateDict(cffData, privOff, privSize, out dw, out nw);
                }
                else
                {
                    fdSubrs[i] = [];
                }
                fdDefaultW[i] = dw;
                fdNominalW[i] = nw;
            }

            int[] fdSelect = ParseFDSelect(cffData, fdSelectOffset, numGlyphs);

            return new CffTable(charStrings, globalSubrs, [],
                defaultWidthX: 0, nominalWidthX: 0, numGlyphs, true,
                fdSubrs, fdDefaultW, fdNominalW, fdSelect);
        }
    }

    public Glyph? ParseGlyph(ushort glyphIndex)
    {
        if (glyphIndex >= _numGlyphs || glyphIndex >= _charStrings.Length)
            return null;

        byte[] charString = _charStrings[glyphIndex];
        if (charString.Length == 0)
            return null;

        byte[][] localSubrs;
        short defaultWidthX, nominalWidthX;

        if (_isCID && _fdSelect != null && _fontDictSubrs != null)
        {
            int fdIndex = glyphIndex < _fdSelect.Length ? _fdSelect[glyphIndex] : 0;
            fdIndex = Math.Clamp(fdIndex, 0, _fontDictSubrs.Length - 1);
            localSubrs = _fontDictSubrs[fdIndex];
            defaultWidthX = _fdDefaultWidthX![fdIndex];
            nominalWidthX = _fdNominalWidthX![fdIndex];
        }
        else
        {
            localSubrs = _localSubrs;
            defaultWidthX = _defaultWidthX;
            nominalWidthX = _nominalWidthX;
        }

        return InterpretCharstring(charString, localSubrs, _globalSubrs,
            defaultWidthX, nominalWidthX);
    }

    // ──────────────────────────────────────────────
    //  Private: Type 2 charstring interpreter
    // ──────────────────────────────────────────────

    private static Glyph? InterpretCharstring(
        byte[] charString,
        byte[][] localSubrs,
        byte[][] globalSubrs,
        short defaultWidthX,
        short nominalWidthX)
    {
        var interp = new CharstringInterpreter(localSubrs, globalSubrs,
            defaultWidthX, nominalWidthX);
        return interp.Interpret(charString);
    }

    private sealed class CharstringInterpreter
    {
        private readonly byte[][] _localSubrs;
        private readonly byte[][] _globalSubrs;
        private readonly int _localBias;
        private readonly int _globalBias;

        private List<double> _stack = new();
        private List<List<GlyphPoint>> _contours = new();
        private List<GlyphPoint>? _currentContour;
        private double _x, _y;
        private double _startX, _startY;
        private bool _widthParsed;
        private int _hintCount;
        private int _subrDepth;
        private bool _done;

        private const int MaxSubrDepth = 20;
        private const int MaxSubrCalls = 5000;
        private int _subrCallCount;

        public CharstringInterpreter(
            byte[][] localSubrs,
            byte[][] globalSubrs,
            short defaultWidthX,
            short nominalWidthX)
        {
            _localSubrs = localSubrs;
            _globalSubrs = globalSubrs;
            _localBias = CalcSubrBias(localSubrs.Length);
            _globalBias = CalcSubrBias(globalSubrs.Length);
            _ = defaultWidthX;
            _ = nominalWidthX;
        }

        public Glyph? Interpret(byte[] charString)
        {
            _stack.Clear();
            _contours.Clear();
            _currentContour = null;
            _x = _y = _startX = _startY = 0;
            _widthParsed = false;
            _hintCount = 0;
            _subrDepth = 0;
            _subrCallCount = 0;
            _done = false;

            int ip = 0;
            int safetyCounter = 0;
            const int maxIterations = 100000;
            while (ip < charString.Length && !_done)
            {
                if (++safetyCounter > maxIterations)
                {
                    // Safety: prevent runaway interpreter
                    break;
                }
                int b0 = charString[ip++];

                if (b0 == 28)
                {
                    if (ip + 1 >= charString.Length) break;
                    int val = (short)((charString[ip] << 8) | charString[ip + 1]);
                    ip += 2;
                    _stack.Add(val);
                }
                else if (b0 == 29)
                {
                    // 29 = callgsubr in Type 2 charstrings
                    if (_stack.Count == 0) break;
                    int subrNum = PopIntAt(_stack.Count - 1);
                    int subrIdx = subrNum + _globalBias;
                    if (subrIdx >= 0 && subrIdx < _globalSubrs.Length && _subrCallCount < MaxSubrCalls)
                    {
                        _subrDepth++;
                        _subrCallCount++;
                        if (_subrDepth <= MaxSubrDepth)
                            InterpSubr(_globalSubrs[subrIdx]);
                        _subrDepth--;
                    }
                }
                else if (b0 >= 32 && b0 <= 246)
                {
                    _stack.Add(b0 - 139);
                }
                else if (b0 >= 247 && b0 <= 250)
                {
                    if (ip >= charString.Length) break;
                    int b1 = charString[ip++];
                    _stack.Add((b0 - 247) * 256 + b1 + 108);
                }
                else if (b0 >= 251 && b0 <= 254)
                {
                    if (ip >= charString.Length) break;
                    int b1 = charString[ip++];
                    _stack.Add(-(b0 - 251) * 256 - b1 - 108);
                }
                else if (b0 == 12)
                {
                    if (ip >= charString.Length) break;
                    int b1 = charString[ip++];
                    ProcessEscapeOp(b1);
                }
                else if (b0 == 18 || b0 == 19)
                {
                    // hintmask (18) / cntrmask (19)
                    // These operators consume the stacked hints and are followed by mask bytes.
                    // The mask size depends on total hint count.
                    if (!_widthParsed)
                    {
                        _widthParsed = true;
                        if (_stack.Count > 0 && (_stack.Count % 2 == 1))
                            _stack.RemoveAt(0);
                    }
                    _hintCount += _stack.Count / 2;
                    _stack.Clear();
                    // Skip the mask bytes: (hintCount + 7) / 8 bytes
                    int maskBytes = (_hintCount + 7) / 8;
                    ip = Math.Min(ip + maskBytes, charString.Length);
                }
                else
                {
                    ProcessOp(b0);
                }
            }

            CloseContour();

            if (_contours.Count == 0)
                return Glyph.Empty;

            var resultContours = new GlyphPoint[_contours.Count][];
            short xMin = short.MaxValue, yMin = short.MaxValue;
            short xMax = short.MinValue, yMax = short.MinValue;

            for (int i = 0; i < _contours.Count; i++)
            {
                var contour = _contours[i];
                resultContours[i] = contour.ToArray();
                foreach (var pt in contour)
                {
                    if (pt.X < xMin) xMin = pt.X;
                    if (pt.Y < yMin) yMin = pt.Y;
                    if (pt.X > xMax) xMax = pt.X;
                    if (pt.Y > yMax) yMax = pt.Y;
                }
            }

            if (xMin > xMax)
                return Glyph.Empty;

            return new Glyph(0, resultContours, xMin, yMin, xMax, yMax);
        }

        private void ProcessOp(int op)
        {
            switch (op)
            {
                case 1: // hstem
                    if (!_widthParsed) { _widthParsed = true; if (_stack.Count % 2 == 1) _stack.RemoveAt(0); }
                    _hintCount += _stack.Count / 2; _stack.Clear(); break;
                case 3: // vstem
                    if (!_widthParsed) { _widthParsed = true; if (_stack.Count % 2 == 1) _stack.RemoveAt(0); }
                    _hintCount += _stack.Count / 2; _stack.Clear(); break;

                case 4: // vmoveto
                    CheckWidth(1);
                    _y += PopInt();
                    MoveTo();
                    break;

                case 5: // rlineto
                    CheckWidthMod(_stack.Count, 2);
                    while (_stack.Count >= 2) { _x += PopIntAt(0); _y += PopIntAt(0); AddLinePoint(); }
                    break;

                case 6: // hlineto
                    {
                        CheckWidthAllowOdd(_stack.Count);
                        bool horizontal = true;
                        while (_stack.Count > 0)
                        {
                            int val = PopIntAt(0);
                            if (horizontal) _x += val; else _y += val;
                            horizontal = !horizontal;
                            AddLinePoint();
                        }
                    }
                    break;

                case 7: // vlineto
                    {
                        CheckWidthAllowOdd(_stack.Count);
                        bool vertical = true;
                        while (_stack.Count > 0)
                        {
                            int val = PopIntAt(0);
                            if (vertical) _y += val; else _x += val;
                            vertical = !vertical;
                            AddLinePoint();
                        }
                    }
                    break;

                case 8: // rrcurveto
                    CheckWidthMod(_stack.Count, 6);
                    EnsureContour();
                    while (_stack.Count >= 6)
                    {
                        int dxa = PopIntAt(0), dya = PopIntAt(0);
                        int dxb = PopIntAt(0), dyb = PopIntAt(0);
                        int dxc = PopIntAt(0), dyc = PopIntAt(0);
                        double x1 = _x + dxa, y1 = _y + dya;
                        double x2 = x1 + dxb, y2 = y1 + dyb;
                        double x3 = x2 + dxc, y3 = y2 + dyc;
                        FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                        _x = x3; _y = y3;
                    }
                    break;

                case 9: // closepath
                    CloseContour(); _x = _startX; _y = _startY;
                    break;

                case 10: // callsubr
                    if (_stack.Count == 0) break;
                    { int subrNum = PopIntAt(_stack.Count - 1); int subrIdx = subrNum + _localBias; if (subrIdx >= 0 && subrIdx < _localSubrs.Length && _subrCallCount < MaxSubrCalls) { _subrDepth++; _subrCallCount++; if (_subrDepth <= MaxSubrDepth) InterpSubr(_localSubrs[subrIdx]); _subrDepth--; } }
                    break;

                case 11: break;

                case 14: // endchar
                    CloseContour(); _done = true; break;

                case 20: // rmoveto
                    CheckWidth(2); _x += PopIntAt(0); _y += PopIntAt(0); MoveTo();
                    break;

                case 21: // hmoveto
                    CheckWidth(1); _x += PopInt(); MoveTo();
                    break;

                case 22: // vstemhm
                    if (!_widthParsed) { _widthParsed = true; if (_stack.Count % 2 == 1) _stack.RemoveAt(0); }
                    _hintCount += _stack.Count / 2; _stack.Clear();
                    break;

                case 23: // rcurveline
                    {
                        int cnt = _stack.Count;
                        // rcurveline: 6n+2 operands (no width) or 6n+3 (with width).
                        // The previous check "(cnt - 1) % 6 == 1" was inverted and matched
                        // the *no-width* case, wrongly consuming dx1 of the first curve as
                        // if it were a width and shifting the whole contour.
                        if (cnt > 0 && !_widthParsed && cnt % 6 == 3)
                        { _widthParsed = true; _stack.RemoveAt(0); cnt--; }
                        int nCurves = cnt / 6;
                        EnsureContour();
                        for (int i = 0; i < nCurves; i++)
                        {
                            int dxa = PopIntAt(0), dya = PopIntAt(0);
                            int dxb = PopIntAt(0), dyb = PopIntAt(0);
                            int dxc = PopIntAt(0), dyc = PopIntAt(0);
                            double x1 = _x + dxa, y1 = _y + dya;
                            double x2 = x1 + dxb, y2 = y1 + dyb;
                            double x3 = x2 + dxc, y3 = y2 + dyc;
                            FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                            _x = x3; _y = y3;
                        }
                        if (_stack.Count >= 2) { _x += PopIntAt(0); _y += PopIntAt(0); AddLinePoint(); }
                    }
                    break;

                case 24: // rlinecurve
                    {
                        int cnt = _stack.Count;
                        if (cnt > 6 && !_widthParsed && (cnt - 7) % 2 == 0)
                        { _widthParsed = true; _stack.RemoveAt(0); cnt--; }
                        int nLines = (cnt - 6) / 2;
                        EnsureContour();
                        for (int i = 0; i < nLines; i++) { _x += PopIntAt(0); _y += PopIntAt(0); AddLinePoint(); }
                        if (_stack.Count >= 6)
                        {
                            int dxa = PopIntAt(0), dya = PopIntAt(0);
                            int dxb = PopIntAt(0), dyb = PopIntAt(0);
                            int dxc = PopIntAt(0), dyc = PopIntAt(0);
                            double cx1 = _x + dxa, cy1 = _y + dya;
                            double cx2 = cx1 + dxb, cy2 = cy1 + dyb;
                            double cx3 = cx2 + dxc, cy3 = cy2 + dyc;
                            FlattenCubicTo(cx1, cy1, cx2, cy2, cx3, cy3);
                            _x = cx3; _y = cy3;
                        }
                    }
                    break;

                case 25: // vvcurveto
                    {
                        int cnt = _stack.Count;
                        if (cnt > 0 && !_widthParsed && (cnt % 2 == 1))
                        { _widthParsed = true; _stack.RemoveAt(0); }
                        EnsureContour();
                        while (_stack.Count >= 4)
                        {
                            int dy1 = PopIntAt(0), dy2 = PopIntAt(0), dx3 = PopIntAt(0), dy3 = PopIntAt(0);
                            double x1 = _x, y1 = _y + dy1;
                            double x2 = x1, y2 = y1 + dy2;
                            double x3 = x2 + dx3, y3 = y2 + dy3;
                            FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                            _x = x3; _y = y3;
                        }
                    }
                    break;

                case 26: // hvcurveto
                    {
                        int cnt = _stack.Count;
                        if (cnt > 0 && !_widthParsed && (cnt % 2 == 1))
                        { _widthParsed = true; _stack.RemoveAt(0); }
                        bool hzFirst = true;
                        EnsureContour();
                        while (_stack.Count >= 4)
                        {
                            int a = PopIntAt(0), b = PopIntAt(0), c = PopIntAt(0), d = PopIntAt(0);
                            if (hzFirst)
                            {
                                double x1 = _x + a, y1 = _y;
                                double x2 = x1, y2 = y1 + b;
                                double x3 = x2 + c, y3 = y2 + d;
                                FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                                _x = x3; _y = y3;
                            }
                            else
                            {
                                double x1 = _x, y1 = _y + a;
                                double x2 = x1 + b, y2 = y1;
                                double x3 = x2 + c, y3 = y2 + d;
                                FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                                _x = x3; _y = y3;
                            }
                            hzFirst = !hzFirst;
                        }
                    }
                    break;

                case 27: // hhcurveto
                    {
                        int cnt = _stack.Count;
                        if (cnt > 0 && !_widthParsed && (cnt % 2 == 1))
                        { _widthParsed = true; _stack.RemoveAt(0); }
                        EnsureContour();
                        while (_stack.Count >= 4)
                        {
                            int dx1 = PopIntAt(0), dx2 = PopIntAt(0), dx3 = PopIntAt(0), dy3 = PopIntAt(0);
                            double x1 = _x + dx1, y1 = _y;
                            double x2 = x1 + dx2, y2 = y1;
                            double x3 = x2 + dx3, y3 = y2 + dy3;
                            FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                            _x = x3; _y = y3;
                        }
                    }
                    break;

                case 30: // vhcurveto
                    {
                        int cnt = _stack.Count;
                        if (cnt > 0 && !_widthParsed && (cnt % 2 == 1))
                        { _widthParsed = true; _stack.RemoveAt(0); }
                        bool vtFirst = true;
                        EnsureContour();
                        while (_stack.Count >= 4)
                        {
                            int a = PopIntAt(0), b = PopIntAt(0), c = PopIntAt(0), d = PopIntAt(0);
                            if (vtFirst)
                            {
                                double x1 = _x, y1 = _y + a;
                                double x2 = x1 + b, y2 = y1;
                                double x3 = x2, y3 = y2 + c;
                                double x4 = x3 + d, y4 = y3;
                                FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                                _x = x4; _y = y4;
                            }
                            else
                            {
                                double x1 = _x + a, y1 = _y;
                                double x2 = x1, y2 = y1 + b;
                                double x3 = x2 + c, y3 = y2;
                                double x4 = x3, y4 = y3 + d;
                                FlattenCubicTo(x1, y1, x2, y2, x3, y3);
                                _x = x4; _y = y4;
                            }
                            vtFirst = !vtFirst;
                        }
                    }
                    break;

                case 31: // hflex
                    {
                        int cnt = _stack.Count;
                        if (cnt == 7) { _widthParsed = true; _stack.RemoveAt(0); }
                        if (_stack.Count >= 6)
                        {
                            int dx1 = PopIntAt(0), dx2 = PopIntAt(0), dx3 = PopIntAt(0);
                            int dx4 = PopIntAt(0), dx5 = PopIntAt(0), dx6 = PopIntAt(0);
                            EnsureContour();
                            double x1 = _x + dx1, x2 = x1 + dx2, x3 = x2 + dx3;
                            FlattenCubicTo(x1, _y, x2, _y, x3, _y); _x = x3;
                            double x4 = _x + dx4, x5 = x4 + dx5, x6 = x5 + dx6;
                            FlattenCubicTo(x4, _y, x5, _y, x6, _y); _x = x6;
                        }
                        else { _stack.Clear(); }
                    }
                    break;
            }
        }

        private void ProcessEscapeOp(int b1)
        {
            switch (b1)
            {
                case 0: break; // dotsection
                case 1:
                case 2: // vstem3 / hstem3
                    if (!_widthParsed) { _widthParsed = true; if (_stack.Count % 2 == 1) _stack.RemoveAt(0); }
                    _hintCount += _stack.Count / 2; _stack.Clear(); break;

                case 4: // flex
                    {
                        int cnt = _stack.Count;
                        if (cnt == 14) { _widthParsed = true; _stack.RemoveAt(0); }
                        if (_stack.Count >= 12)
                        {
                            double dx1 = PopIntAt(0), dy1 = PopIntAt(0);
                            double dx2 = PopIntAt(0), dy2 = PopIntAt(0);
                            double dx3 = PopIntAt(0), dy3 = PopIntAt(0);
                            double dx4 = PopIntAt(0), dy4 = PopIntAt(0);
                            double dx5 = PopIntAt(0), dy5 = PopIntAt(0);
                            double dx6 = PopIntAt(0), dy6 = PopIntAt(0);
                            PopIntAt(0); // fd
                            EnsureContour();
                            double x1 = _x + dx1, y1 = _y + dy1, x2 = x1 + dx2, y2 = y1 + dy2, x3 = x2 + dx3, y3 = y2 + dy3;
                            FlattenCubicTo(x1, y1, x2, y2, x3, y3); _x = x3; _y = y3;
                            double x4 = _x + dx4, y4 = _y + dy4, x5 = x4 + dx5, y5 = y4 + dy5, x6 = x5 + dx6, y6 = y5 + dy6;
                            FlattenCubicTo(x4, y4, x5, y5, x6, y6); _x = x6; _y = y6;
                        }
                    }
                    break;

                case 5: // hflex1
                    {
                        int cnt = _stack.Count;
                        if (cnt == 10) { _widthParsed = true; _stack.RemoveAt(0); }
                        if (_stack.Count >= 9)
                        {
                            double dx1 = PopIntAt(0), dy1 = PopIntAt(0);
                            double dx2 = PopIntAt(0), dy2 = PopIntAt(0);
                            double dx3 = PopIntAt(0), dx4 = PopIntAt(0);
                            double dx5 = PopIntAt(0), dy5 = PopIntAt(0), dx6 = PopIntAt(0);
                            EnsureContour();
                            double x1 = _x + dx1, y1 = _y + dy1, x2 = x1 + dx2, y2 = y1 + dy2, x3 = x2 + dx3;
                            FlattenCubicTo(x1, y1, x2, y2, x3, _y); _x = x3;
                            double x4 = _x + dx4, x5 = x4 + dx5, y5 = _y + dy5, x6 = x5 + dx6;
                            FlattenCubicTo(x4, _y, x5, y5, x6, _y); _x = x6;
                        }
                        else { _stack.Clear(); }
                    }
                    break;

                case 6: // flex1
                    {
                        int cnt = _stack.Count;
                        if (cnt == 12) { _widthParsed = true; _stack.RemoveAt(0); }
                        if (_stack.Count >= 11)
                        {
                            double dx1 = PopIntAt(0), dy1 = PopIntAt(0);
                            double dx2 = PopIntAt(0), dy2 = PopIntAt(0);
                            double dx3 = PopIntAt(0), dy3 = PopIntAt(0);
                            double dx4 = PopIntAt(0), dy4 = PopIntAt(0);
                            double dx5 = PopIntAt(0), dy5 = PopIntAt(0);
                            double dx6 = PopIntAt(0);
                            double dy6 = -(dy1 + dy2 + dy3 + dy4 + dy5);
                            EnsureContour();
                            double x1 = _x + dx1, y1 = _y + dy1, x2 = x1 + dx2, y2 = y1 + dy2, x3 = x2 + dx3, y3 = y2 + dy3;
                            FlattenCubicTo(x1, y1, x2, y2, x3, y3); _x = x3; _y = y3;
                            double x4 = _x + dx4, y4 = _y + dy4, x5 = x4 + dx5, y5 = y4 + dy5, x6 = x5 + dx6, y6f = _y + dy6;
                            FlattenCubicTo(x4, y4, x5, y5, x6, y6f); _x = x6; _y = y6f;
                        }
                        else { _stack.Clear(); }
                    }
                    break;
            }
        }

        // ── Helper methods ──

        private void CheckWidth(int expectedStack)
        {
            if (_widthParsed) return;
            _widthParsed = true;
            if (_stack.Count == expectedStack + 1) _stack.RemoveAt(0);
        }

        private void CheckWidthAllowOdd(int count)
        {
            if (_widthParsed) return;
            _widthParsed = true;
            if (count > 0 && (count & 1) == 1) _stack.RemoveAt(0);
        }

        private void CheckWidthMod(int count, int mod)
        {
            if (_widthParsed) return;
            _widthParsed = true;
            if (count > 0 && count % mod == 1) _stack.RemoveAt(0);
        }

        private void MoveTo() { CloseContour(); _startX = _x; _startY = _y; }

        private void AddLinePoint() { EnsureContour(); _currentContour!.Add(MakePoint(_x, _y, true)); }

        private void EnsureContour()
        {
            // The first draw operation after a moveto must implicitly start
            // its contour at the moveto's destination. Without seeding the
            // contour with the current point, every CFF contour would lose
            // its first vertex and render displaced / fragmented.
            if (_currentContour == null)
            {
                _currentContour = new List<GlyphPoint> { MakePoint(_x, _y, true) };
                _contours.Add(_currentContour);
            }
        }

        private void CloseContour()
        {
            if (_currentContour is { Count: > 0 })
            {
                var f = _currentContour[0];
                var l = _currentContour[^1];
                if (_currentContour.Count > 1 && Math.Abs(l.X - f.X) < 1 && Math.Abs(l.Y - f.Y) < 1)
                    _currentContour.RemoveAt(_currentContour.Count - 1);
                if (_currentContour.Count == 0)
                {
                    _contours.RemoveAt(_contours.Count - 1);
                }
                _currentContour = null;
            }
        }

        private void FlattenCubicTo(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            EnsureContour();
            FlattenCubicRecursive(_x, _y, x1, y1, x2, y2, x3, y3, _currentContour!, 0);
        }

        private static void FlattenCubicRecursive(
            double x0, double y0, double x1, double y1,
            double x2, double y2, double x3, double y3,
            List<GlyphPoint> contour, int depth)
        {
            double dx = x3 - x0, dy = y3 - y0, len2 = dx * dx + dy * dy;

            // Depth limit: force-add endpoint to avoid gaps in the contour.
            if (depth > 10)
            {
                contour.Add(MakePoint(x3, y3, true));
                return;
            }

            // Near-zero length: treat as a point.
            if (len2 < 0.5)
            {
                contour.Add(MakePoint(x3, y3, true));
                return;
            }

            double d = Math.Abs((x1 - x0) * dy - (y1 - y0) * dx)
                     + Math.Abs((x2 - x0) * dy - (y2 - y0) * dx);

            // Flat enough: approximate with a straight line segment to the endpoint.
            if (d * d < len2 * 0.5)
            {
                contour.Add(MakePoint(x3, y3, true));
                return;
            }

            double mx01 = (x0 + x1) * 0.5, my01 = (y0 + y1) * 0.5;
            double mx12 = (x1 + x2) * 0.5, my12 = (y1 + y2) * 0.5;
            double mx23 = (x2 + x3) * 0.5, my23 = (y2 + y3) * 0.5;
            double mx012 = (mx01 + mx12) * 0.5, my012 = (my01 + my12) * 0.5;
            double mx123 = (mx12 + mx23) * 0.5, my123 = (my12 + my23) * 0.5;
            double mx0123 = (mx012 + mx123) * 0.5, my0123 = (my012 + my123) * 0.5;

            // Left half terminates by adding mx0123 as its endpoint.
            // Right half terminates by adding (x3,y3) as its endpoint.
            // No manual midpoint insertion needed — avoids duplicate vertices.
            FlattenCubicRecursive(x0, y0, mx01, my01, mx012, my012, mx0123, my0123, contour, depth + 1);
            FlattenCubicRecursive(mx0123, my0123, mx123, my123, mx23, my23, x3, y3, contour, depth + 1);
        }

        private static GlyphPoint MakePoint(double x, double y, bool onCurve)
            => new((short)Math.Round(x), (short)Math.Round(-y), onCurve);

        private int PopInt() { if (_stack.Count == 0) return 0; double v = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); return (int)Math.Round(v); }
        private int PopIntAt(int idx) { if (idx < 0 || idx >= _stack.Count) return 0; double v = _stack[idx]; _stack.RemoveAt(idx); return (int)Math.Round(v); }

        private void InterpSubr(byte[] subrData)
        {
            int ip = 0;
            while (ip < subrData.Length && !_done)
            {
                int b0 = subrData[ip++];
                if (b0 == 11) return;
                if (b0 == 28) { if (ip + 1 >= subrData.Length) break; _stack.Add((short)((subrData[ip] << 8) | subrData[ip + 1])); ip += 2; }
                else if (b0 == 29) { if (_stack.Count == 0) break; int sn = PopIntAt(_stack.Count - 1); int si = sn + _globalBias; if (si >= 0 && si < _globalSubrs.Length && _subrCallCount < MaxSubrCalls) { _subrDepth++; _subrCallCount++; if (_subrDepth <= MaxSubrDepth) InterpSubr(_globalSubrs[si]); _subrDepth--; } }
                else if (b0 >= 32 && b0 <= 246) _stack.Add(b0 - 139);
                else if (b0 >= 247 && b0 <= 250) { if (ip >= subrData.Length) break; _stack.Add((b0 - 247) * 256 + subrData[ip++] + 108); }
                else if (b0 >= 251 && b0 <= 254) { if (ip >= subrData.Length) break; _stack.Add(-(b0 - 251) * 256 - subrData[ip++] - 108); }
                else if (b0 == 10) { if (_stack.Count == 0) break; int sn = PopIntAt(_stack.Count - 1); int si = sn + _localBias; if (si >= 0 && si < _localSubrs.Length && _subrCallCount < MaxSubrCalls) { _subrDepth++; _subrCallCount++; if (_subrDepth <= MaxSubrDepth) InterpSubr(_localSubrs[si]); _subrDepth--; } }
                else if (b0 == 12)
                {
                    // Escape prefix: subroutines may legally contain flex/hflex/hflex1/flex1
                    // (escape 4/5/6) and hstem3/vstem3 (escape 1/2). Without this branch those
                    // operators would fall through to ProcessOp (which has no case 12), and the
                    // escape parameter byte would be pushed onto the operand stack as a number,
                    // desyncing all subsequent interpretation and producing garbled glyphs.
                    if (ip >= subrData.Length) break;
                    int b1 = subrData[ip++];
                    ProcessEscapeOp(b1);
                }
                else if (b0 == 14) { CloseContour(); _done = true; return; }
                else if (b0 == 18 || b0 == 19)
                {
                    if (!_widthParsed)
                    {
                        _widthParsed = true;
                        if (_stack.Count > 0 && (_stack.Count % 2 == 1))
                            _stack.RemoveAt(0);
                    }
                    _hintCount += _stack.Count / 2;
                    _stack.Clear();
                    int maskBytes = (_hintCount + 7) / 8;
                    ip = Math.Min(ip + maskBytes, subrData.Length);
                }
                else if (b0 is >= 0 and <= 31) ProcessOp(b0);
                else _stack.Add(b0 - 139);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Private: CFF INDEX parsing utilities
    // ──────────────────────────────────────────────

    /// <summary>Read the count field of an INDEX (2 bytes BE). Advances offset by 2.</summary>
    private static int ReadIndexCount(byte[] data, ref int offset)
    {
        if (offset + 2 > data.Length)
            throw new InvalidFontFileException("CFF INDEX truncated.");
        return (data[offset++] << 8) | data[offset++];
    }

    /// <summary>Read the offSize field of an INDEX (1 byte). Advances offset by 1.</summary>
    private static int ReadIndexOffSize(byte[] data, ref int offset)
    {
        if (offset >= data.Length)
            throw new InvalidFontFileException("CFF INDEX truncated.");
        int offSize = data[offset++];
        if (offSize < 1 || offSize > 4)
            throw new InvalidFontFileException($"CFF invalid offSize: {offSize}");
        return offSize;
    }

    /// <summary>Read a multi-byte offset value at the given position.</summary>
    private static int ReadOffsetAt(byte[] data, int pos, int offSize)
    {
        return offSize switch
        {
            1 => data[pos],
            2 => (data[pos] << 8) | data[pos + 1],
            3 => (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2],
            4 => (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3],
            _ => throw new InvalidFontFileException($"CFF bad offSize: {offSize}")
        };
    }

    /// <summary>
    /// Read all objects from a CFF INDEX. Advances offset past the entire INDEX.
    /// </summary>
    private static byte[][] ReadIndex(byte[] data, ref int offset, out byte[][] items)
    {
        items = ReadIndex(data, ref offset);
        return items;
    }

    private static byte[][] ReadIndex(byte[] data, ref int offset)
    {
        int count = ReadIndexCount(data, ref offset);
        if (count == 0) return [];

        int offSize = ReadIndexOffSize(data, ref offset);
        int offsetArrayPos = offset - 1; // point back to offSize byte for clarity

        // Read offsets
        int total = count + 1;
        int[] offsets = new int[total];
        for (int i = 0; i < total; i++)
            offsets[i] = ReadOffsetAt(data, offset + i * offSize, offSize);

        // Data starts right after the offset array
        int offsetArraySize = total * offSize;
        int dataStart = offsetArrayPos + 1 + offsetArraySize;

        // Items
        var items = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            int len = offsets[i + 1] - offsets[i];
            int itemStart = dataStart + offsets[i] - 1;
            items[i] = new byte[len];
            if (len > 0)
                Array.Copy(data, itemStart, items[i], 0, len);
        }

        // Advance offset past the data
        offset = dataStart + offsets[count] - 1;
        return items;
    }

    /// <summary>
    /// Parse an INDEX at an arbitrary offset (used for CharStrings, FDArray).
    /// </summary>
    private static byte[][] ParseIndexAt(byte[] data, int indexOffset, int? maxItems = null)
    {
        if (indexOffset + 2 > data.Length)
            throw new InvalidFontFileException("CFF INDEX truncated.");

        int count = (data[indexOffset] << 8) | data[indexOffset + 1];
        if (count == 0) return [];

        int offSize = data[indexOffset + 2];
        if (offSize < 1 || offSize > 4)
            throw new InvalidFontFileException($"CFF invalid offSize: {offSize}");

        int offsetArrayPos = indexOffset + 3;
        int total = count + 1;
        int[] offsets = new int[total];
        for (int i = 0; i < total; i++)
            offsets[i] = ReadOffsetAt(data, offsetArrayPos + i * offSize, offSize);

        int offsetArraySize = total * offSize;
        int dataStart = indexOffset + 3 + offsetArraySize;

        int actualCount = maxItems.HasValue ? Math.Min(count, maxItems.Value) : count;
        var items = new byte[actualCount][];
        for (int i = 0; i < actualCount; i++)
        {
            int len = offsets[i + 1] - offsets[i];
            int itemStart = dataStart + offsets[i] - 1;
            items[i] = new byte[len];
            if (len > 0 && itemStart + len <= data.Length)
                Array.Copy(data, itemStart, items[i], 0, len);
        }

        return items;
    }

    /// <summary>Skip over a CFF INDEX entirely. Advances offset past the INDEX.</summary>
    private static void SkipIndex(byte[] data, ref int offset)
    {
        int count = ReadIndexCount(data, ref offset);
        if (count == 0) return;
        int offSize = ReadIndexOffSize(data, ref offset);
        int offsetArraySize = (count + 1) * offSize;
        // Read the last offset to compute data size
        int lastOffset = ReadOffsetAt(data, offset + count * offSize, offSize);
        int dataStart = offset + offsetArraySize;
        offset = dataStart + lastOffset - 1;
    }

    // ──────────────────────────────────────────────
    //  Private: DICT parser
    // ──────────────────────────────────────────────

    private static Dictionary<int, List<double>> ParseDict(byte[] dictData)
    {
        var result = new Dictionary<int, List<double>>();
        var operands = new List<double>();

        int i = 0;
        while (i < dictData.Length)
        {
            int b0 = dictData[i++];
            if (b0 <= 21)
            {
                if (operands.Count > 0) { result[b0] = new List<double>(operands); operands.Clear(); }
            }
            else if (b0 == 12)
            {
                if (i >= dictData.Length) break;
                int b1 = dictData[i++];
                if (operands.Count > 0) { result[1200 + b1] = new List<double>(operands); operands.Clear(); }
            }
            else if (b0 == 28) { if (i + 1 >= dictData.Length) break; operands.Add((short)((dictData[i] << 8) | dictData[i + 1])); i += 2; }
            else if (b0 == 29) { if (i + 3 >= dictData.Length) break; int v = (dictData[i] << 24) | (dictData[i + 1] << 16) | (dictData[i + 2] << 8) | dictData[i + 3]; i += 4; operands.Add(v); }
            else if (b0 >= 32 && b0 <= 246) operands.Add(b0 - 139);
            else if (b0 >= 247 && b0 <= 250) { if (i >= dictData.Length) break; operands.Add((b0 - 247) * 256 + dictData[i++] + 108); }
            else if (b0 >= 251 && b0 <= 254) { if (i >= dictData.Length) break; operands.Add(-(b0 - 251) * 256 - dictData[i++] - 108); }
        }
        return result;
    }

    private static int GetDictOffset(Dictionary<int, List<double>> dict, int op)
    {
        if (!dict.TryGetValue(op, out var data) || data.Count == 0)
            throw new InvalidFontFileException($"CFF Top DICT missing operator {op}.");
        return (int)data[^1];
    }

    // ──────────────────────────────────────────────
    //  Private: Private DICT / FDSelect
    // ──────────────────────────────────────────────

    private static byte[][] ParsePrivateDict(byte[] data, int offset, int size,
        out short defaultWidthX, out short nominalWidthX)
    {
        defaultWidthX = 0; nominalWidthX = 0;
        if (size == 0) return [];

        var privBytes = new byte[size];
        Array.Copy(data, offset, privBytes, 0, size);
        var dict = ParseDict(privBytes);

        if (dict.TryGetValue(20, out var dw) && dw.Count >= 1) defaultWidthX = (short)dw[0];
        if (dict.TryGetValue(21, out var nw) && nw.Count >= 1) nominalWidthX = (short)nw[0];

        if (dict.TryGetValue(19, out var subrData) && subrData.Count >= 1)
        {
            int subrOffset = (int)subrData[0];
            int absOffset = offset + subrOffset;
            if (absOffset >= 0 && absOffset < data.Length)
            {
                int count = (data[absOffset] << 8) | data[absOffset + 1];
                if (count == 0) return [];
                int offSize = data[absOffset + 2];
                if (offSize < 1 || offSize > 4) return [];
                int offArrayPos = absOffset + 3;
                int total = count + 1;
                int[] offsets = new int[total];
                for (int i = 0; i < total; i++)
                    offsets[i] = ReadOffsetAt(data, offArrayPos + i * offSize, offSize);
                int dataStart = absOffset + 3 + total * offSize;
                var items = new byte[count][];
                for (int i = 0; i < count; i++)
                {
                    int len = offsets[i + 1] - offsets[i];
                    int start = dataStart + offsets[i] - 1;
                    items[i] = new byte[len];
                    if (len > 0 && start + len <= data.Length)
                        Array.Copy(data, start, items[i], 0, len);
                }
                return items;
            }
        }
        return [];
    }

    private static int[] ParseFDSelect(byte[] data, int offset, int numGlyphs)
    {
        int format = data[offset];
        var fdSelect = new int[numGlyphs];

        if (format == 0)
        {
            for (int i = 0; i < numGlyphs; i++)
                fdSelect[i] = data[offset + 1 + i];
        }
        else if (format == 3)
        {
            int pos = offset + 1;
            int nRanges = (data[pos] << 8) | data[pos + 1];
            pos += 2;
            int prevGlyph = 0, prevFD = 0;
            for (int r = 0; r < nRanges; r++)
            {
                int firstGlyph = (data[pos] << 8) | data[pos + 1]; pos += 2;
                int fd = (data[pos] << 8) | data[pos + 1]; pos += 2;
                for (int g = prevGlyph; g < firstGlyph && g < numGlyphs; g++) fdSelect[g] = prevFD;
                prevGlyph = firstGlyph; prevFD = fd;
            }
            for (int g = prevGlyph; g < numGlyphs; g++) fdSelect[g] = prevFD;
        }
        else throw new InvalidFontFileException($"Unsupported FDSelect format: {format}");

        return fdSelect;
    }

    private static int CalcSubrBias(int count)
        => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;
}
