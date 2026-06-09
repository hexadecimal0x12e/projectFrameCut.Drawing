namespace projectFrameCut.Drawing.Text.FontHelper;

/// <summary>Exception thrown when a font file is invalid or corrupted.</summary>
public sealed class InvalidFontFileException : IOException
{
    /// <inheritdoc/>
    public InvalidFontFileException(string message) : base(message) { }

    /// <inheritdoc/>
    public InvalidFontFileException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Exception thrown when a glyph is not found in the font.</summary>
public sealed class GlyphNotFoundException : KeyNotFoundException
{
    /// <summary>The missing glyph character, if known.</summary>
    public char? Glyph { get; init; }

    /// <summary>The font name where the glyph was not found, if known.</summary>
    public string? Font { get; init; }

    /// <inheritdoc/>
    public GlyphNotFoundException() { }

    /// <inheritdoc/>
    public GlyphNotFoundException(string message) : base(message) { }

    /// <inheritdoc/>
    public GlyphNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>Create an exception for a specific missing glyph.</summary>
    public GlyphNotFoundException(char glyph, string? font) : base($"The character '{(char.IsControl(glyph) ? $"Control char 0x{(int)glyph:x2)}" : $"{glyph} (0x{(int)glyph:x6})")}' was not found in the font '{font ?? "<Unknown font>"}'.")
    {
        Glyph = glyph;
        Font = font;
    }
}
