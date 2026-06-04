namespace projectFrameCut.Drawing.Text.FontHelper;

public sealed class InvalidFontFileException : IOException
{
    public InvalidFontFileException(string message) : base(message) { }

    public InvalidFontFileException(string message, Exception inner) : base(message, inner) { }
}

public sealed class GlyphNotFoundException : KeyNotFoundException
{
    public char? Glyph { get; init; }

    public string? Font { get; init; }

    public GlyphNotFoundException() { }

    public GlyphNotFoundException(string message) : base(message) { }

    public GlyphNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }

    public GlyphNotFoundException(char glyph, string? font) : base($"The character '{(char.IsControl(glyph) ? $"Control char 0x{(int)glyph:x2)}" : $"{glyph} (0x{(int)glyph:x6})")}' was not found in the font '{font ?? "<Unknown font>"}'.")
    {
        Glyph = glyph;
        Font = font;
    }
}
