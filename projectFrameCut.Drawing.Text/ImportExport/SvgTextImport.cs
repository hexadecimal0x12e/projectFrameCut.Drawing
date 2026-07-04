using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;
using projectFrameCut.Drawing.Vector;
using projectFrameCut.Drawing.Vector.ImportExport;

namespace projectFrameCut.Drawing.Text.ImportExport;

/// <summary>
/// Event arguments for <see cref="SvgTextImport.FontResolve"/>.
/// </summary>
/// <remarks>
/// The subscriber should set <see cref="ResultFont"/> to a <see cref="FontFace"/>
/// instance matching <see cref="FamilyName"/>, or leave it <c>null</c> to let the
/// handler try the next fallback family.
/// </remarks>
public sealed class FontResolveEventArgs : EventArgs
{
    /// <summary>The raw CSS font-family value (e.g. <c>"Arial, sans-serif"</c>).</summary>
    public string FontFamily { get; }

    /// <summary>
    /// Individual family name being tried in this invocation
    /// (e.g. <c>"Arial"</c>, then <c>"sans-serif"</c>).
    /// </summary>
    public string FamilyName { get; }

    /// <summary>
    /// Set this to the resolved <see cref="FontFace"/> to stop fallback
    /// iteration. Leave <c>null</c> to skip this family and try the next.
    /// </summary>
    public FontFace? ResultFont { get; set; }

    public FontResolveEventArgs(string fontFamily, string familyName)
    {
        FontFamily = fontFamily ?? throw new ArgumentNullException(nameof(fontFamily));
        FamilyName = familyName ?? throw new ArgumentNullException(nameof(familyName));
    }
}

/// <summary>
/// Provides font resolution and handler creation for importing SVG
/// <c>&lt;text&gt;</c> elements through <see cref="SVGToVectorElement.TextImportHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// Font resolution uses a two-tier system:
/// <list type="number">
///   <item><term><see cref="FontResolve"/> event</term>
///        <description>Fired once per family name in the CSS font-family list.
///        Subscribe to load and return the appropriate <see cref="FontFace"/>.</description></item>
///   <item><term><see cref="FallbackFontResolver"/> property</term>
///        <description>A simple <c>Func</c> tried after the event if no handler
///        provided a font. Useful for the common single-resolver case.</description></item>
/// </list>
/// </para>
/// <para>If neither tier resolves a font for any family, the text element is
/// silently skipped during SVG import.</para>
/// <para>Usage:
/// <code>
/// // Subscribe to the font resolve event
/// SvgTextImport.FontResolve += (sender, e) =>
/// {
///     if (e.FamilyName == "Arial")
///         e.ResultFont = FontFace.Load("arial.ttf");
/// };
///
/// // Register the default handler with the SVG importer
/// SVGToVectorElement.TextImportHandler = SvgTextImport.DefaultHandler;
///
/// // Import SVG — &lt;text&gt; elements are now rendered
/// var picture = SVGToVectorElement.ImportFromSvg(svgContent);
/// </code>
/// </para>
/// <para>For simple cases where only one font resolver is needed, you can
/// skip the event and set <see cref="FallbackFontResolver"/> instead:
/// <code>
/// SvgTextImport.FallbackFontResolver = name => FontFace.Load(name + ".ttf");
/// SVGToVectorElement.TextImportHandler = SvgTextImport.DefaultHandler;
/// </code>
/// </para>
/// </remarks>
public static class SvgTextImport
{
    // ──────────────────────────────────────────────
    //  Events
    // ──────────────────────────────────────────────

    /// <summary>
    /// Raised when the SVG import handler needs to resolve a font family name
    /// to a <see cref="FontFace"/>. Set <see cref="FontResolveEventArgs.ResultFont"/>
    /// to return a font; leave it <c>null</c> to try the next fallback family.
    /// </summary>
    public static event EventHandler<FontResolveEventArgs>? FontResolve;

    /// <summary>
    /// Optional fallback resolver tried after <see cref="FontResolve"/> when
    /// no subscriber returned a font. Receives the individual family name
    /// (e.g. <c>"Arial"</c>) and should return a <see cref="FontFace"/>
    /// or <c>null</c>.
    /// </summary>
    public static Func<string, FontFace?>? FallbackFontResolver { get; set; }

    // ──────────────────────────────────────────────
    //  Handler creation
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets the default handler that uses <see cref="FontResolve"/> and
    /// <see cref="FallbackFontResolver"/> for font resolution.
    /// </summary>
    /// <remarks>
    /// The handler is not cached — each call evaluates the current event
    /// subscribers, so late subscriptions are picked up correctly.
    /// </remarks>
    public static SvgTextImportHandler DefaultHandler => CreateHandler();

    /// <summary>
    /// Create a handler that resolves fonts via the <see cref="FontResolve"/>
    /// event followed by an optional per-instance fallback resolver.
    /// </summary>
    /// <param name="fallbackResolver">
    /// Optional function that receives an individual font family name
    /// (already stripped of quotes/whitespace) and returns a
    /// <see cref="FontFace"/>, or <c>null</c>. This is tried <em>after</em>
    /// the global <see cref="FontResolve"/> event but <em>before</em> the
    /// global <see cref="FallbackFontResolver"/>.
    /// </param>
    public static SvgTextImportHandler CreateHandler(Func<string, FontFace?>? fallbackResolver = null)
    {
        return ctx =>
        {
            // ── 1. Resolve font ─────────────────────────────────────────
            FontFace? font = ResolveFont(ctx.FontFamily, fallbackResolver);
            if (font is null)
                return null; // no font → skip this text element

            // ── 2. Convert SVG sizes to normalized canvas space ─────────
            // SVG font-size is in user units (px); TextEntry.FontSize is a
            // fraction of canvas height (0..1).
            float normalizedFontSize = ctx.FontSize / ctx.CanvasHeight;

            // Clamp to prevent degenerate layouts.
            if (normalizedFontSize <= 0f || !float.IsFinite(normalizedFontSize))
                normalizedFontSize = 0.05f;

            // Normalize stroke width the same way.
            float normalizedStrokeWidth = ctx.StrokeWidth / ctx.CanvasHeight;

            // ── 3. Build TextEntry ──────────────────────────────────────
            var entry = new TextEntry
            {
                Text = ctx.Text,
                FontName = ctx.FontFamily ?? font.FamilyName,
                FontSize = normalizedFontSize,
                X = 0f,                // positioned externally by the SVG importer
                Y = 0f,
                FillR = ctx.FillR,
                FillG = ctx.FillG,
                FillB = ctx.FillB,
                FillA = ctx.FillA,
                StrokeR = ctx.StrokeR,
                StrokeG = ctx.StrokeG,
                StrokeB = ctx.StrokeB,
                StrokeA = ctx.StrokeA,
                StrokeThickness = normalizedStrokeWidth,
                Alignment = ctx.TextAnchor switch
                {
                    "middle" => TextAlignment.Center,
                    "end"    => TextAlignment.Right,
                    _        => TextAlignment.Left,
                },
            };

            // Map font-weight to the Variation Axis if the font supports it.
            if (font.IsVariableFont && ctx.FontWeight is not null)
            {
                if (int.TryParse(ctx.FontWeight, out var weightNum))
                    entry.VariationAxes["wght"] = weightNum;
                else if (ctx.FontWeight.Equals("bold", StringComparison.OrdinalIgnoreCase))
                    entry.VariationAxes["wght"] = 700;
                else if (ctx.FontWeight.Equals("light", StringComparison.OrdinalIgnoreCase))
                    entry.VariationAxes["wght"] = 300;
            }

            // ── 4. Layout ──────────────────────────────────────────────
            var engine = new NormalTypesettingEngine();
            var picture = engine.Layout(entry, font);

            var children = new List<VectorCanvasElement>(picture.Elements.Count);
            foreach (var element in picture.Elements)
                children.Add(element);

            // ── 5. Wrap in composite element ────────────────────────────
            return new TextBlockCanvasElement(children.ToArray())
            {
                UseUniformScale = true,
            };
        };
    }

    // ──────────────────────────────────────────────
    //  Font resolution — private
    // ──────────────────────────────────────────────

    /// <summary>
    /// Walk the CSS font-family list and try each tier in order:
    /// 1) <see cref="FontResolve"/> event
    /// 2) per-instance <paramref name="localFallback"/>
    /// 3) global <see cref="FallbackFontResolver"/>
    /// Returns the first non-null result, or <c>null</c>.
    /// </summary>
    private static FontFace? ResolveFont(string? fontFamily, Func<string, FontFace?>? localFallback)
    {
        if (fontFamily is null)
            return null;

        foreach (var part in fontFamily.Split(','))
        {
            var name = part.Trim().Trim('\'', '"');
            if (name.Length == 0) continue;

            // Tier 1: Fire the FontResolve event.
            var eventTier = FontResolve;
            if (eventTier is not null)
            {
                var args = new FontResolveEventArgs(fontFamily, name);
                eventTier(null, args);
                if (args.ResultFont is not null)
                    return args.ResultFont;
            }

            // Tier 2: Per-instance fallback (from CreateHandler argument).
            if (localFallback is not null)
            {
                var font = localFallback(name);
                if (font is not null)
                    return font;
            }

            // Tier 3: Global fallback property.
            var globalFallback = FallbackFontResolver;
            if (globalFallback is not null)
            {
                var font = globalFallback(name);
                if (font is not null)
                    return font;
            }
        }

        return null;
    }
}
