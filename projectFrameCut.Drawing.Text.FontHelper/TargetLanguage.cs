using System.Globalization;

namespace projectFrameCut.Drawing.Text.FontHelper;

/// <summary>
/// Identifies a language in a font's name table, scoped to a specific platform
/// (Windows, Macintosh, or Unicode) and its corresponding language identifier.
/// </summary>
public readonly record struct TargetLanguage(ushort PlatformId, ushort LanguageId)
{
    /// <summary>
    /// Gets a human-readable display name for this language,
    /// e.g. "English", "Chinese (Simplified)", "日本語".
    /// </summary>
    public string DisplayName => PlatformId switch
    {
        3 => GetWindowsLanguageName(LanguageId),
        1 => GetMacLanguageName(LanguageId),
        0 => "Unicode",
        _ => $"Platform={PlatformId}, Language=0x{LanguageId:X4}",
    };

    public override string ToString() => DisplayName;

    /// <summary>
    /// Converts this language identifier to a BCP-47 language tag,
    /// e.g. "en-US", "zh-CN", "ja-JP".
    /// </summary>
    public string ToBcp47Tag() => PlatformId switch
    {
        3 => GetWindowsBcp47Tag(LanguageId),
        1 => GetMacBcp47Tag(LanguageId),
        0 => "und",
        _ => "und",
    };

    private static string GetWindowsBcp47Tag(ushort langId)
    {
        try
        {
            return CultureInfo.GetCultureInfo(langId).Name;
        }
        catch
        {
            return "und";
        }
    }

    private static string GetMacBcp47Tag(ushort langId) =>
        MacBcp47.TryGetValue(langId, out var tag) ? tag : "und";

    private static string GetWindowsLanguageName(ushort langId)
    {
        try
        {
            return CultureInfo.GetCultureInfo(langId).NativeName;
        }
        catch
        {
            return WindowsLanguages.TryGetValue(langId, out var name) ? name : $"LCID 0x{langId:X4}";
        }
    }

    private static string GetMacLanguageName(ushort langId) =>
        MacLanguages.TryGetValue(langId, out var name) ? name : $"MacLang {langId}";

    public string PlatformName => PlatformId switch
    {
        3 => "Windows",
        1 => "Macintosh",
        0 => "Unicode",
        _ => $"Unknown platform {PlatformId}"
    };

    private static readonly Dictionary<ushort, string> MacBcp47 = new()
    {
        { 0, "en" },
        { 1, "fr" },
        { 2, "de" },
        { 3, "it" },
        { 4, "nl" },
        { 5, "sv" },
        { 6, "es" },
        { 7, "da" },
        { 8, "pt" },
        { 9, "no" },
        { 10, "he" },
        { 11, "ja" },
        { 12, "ar" },
        { 13, "fi" },
        { 14, "el" },
        { 15, "is" },
        { 17, "tr" },
        { 18, "hr" },
        { 19, "zh-Hant" },
        { 20, "ur" },
        { 21, "hi" },
        { 22, "th" },
        { 23, "ko" },
        { 24, "lt" },
        { 25, "pl" },
        { 26, "hu" },
        { 27, "et" },
        { 28, "lv" },
        { 32, "ru" },
        { 33, "zh-Hans" },
        { 34, "nl-BE" },
        { 37, "ro" },
        { 38, "cs" },
        { 39, "sk" },
        { 40, "sl" },
        { 41, "yi" },
        { 42, "sr" },
        { 43, "mk" },
        { 44, "bg" },
        { 45, "uk" },
        { 46, "be" },
        { 48, "kk" },
        { 52, "hy" },
        { 53, "ka" },
        { 54, "ro-MD" },
        { 58, "mn" },
        { 78, "my" },
        { 79, "km" },
        { 80, "lo" },
        { 81, "vi" },
        { 82, "id" },
        { 90, "sw" },
    };

    private static readonly Dictionary<ushort, string> WindowsLanguages = new()
    {
        { 0x0401, "Arabic (Saudi Arabia)" },
        { 0x0404, "Chinese (Traditional)" },
        { 0x0405, "Czech" },
        { 0x0406, "Danish" },
        { 0x0407, "German" },
        { 0x0408, "Greek" },
        { 0x0409, "English" },
        { 0x040B, "Finnish" },
        { 0x040C, "French" },
        { 0x040D, "Hebrew" },
        { 0x040E, "Hungarian" },
        { 0x040F, "Icelandic" },
        { 0x0410, "Italian" },
        { 0x0411, "Japanese" },
        { 0x0412, "Korean" },
        { 0x0413, "Dutch" },
        { 0x0414, "Norwegian (Bokmål)" },
        { 0x0415, "Polish" },
        { 0x0416, "Portuguese (Brazil)" },
        { 0x0417, "Romansh" },
        { 0x0418, "Romanian" },
        { 0x0419, "Russian" },
        { 0x041A, "Croatian" },
        { 0x041B, "Slovak" },
        { 0x041C, "Albanian" },
        { 0x041D, "Swedish" },
        { 0x041E, "Thai" },
        { 0x041F, "Turkish" },
        { 0x0420, "Urdu" },
        { 0x0421, "Indonesian" },
        { 0x0422, "Ukrainian" },
        { 0x0423, "Belarusian" },
        { 0x0424, "Slovenian" },
        { 0x0425, "Estonian" },
        { 0x0426, "Latvian" },
        { 0x0427, "Lithuanian" },
        { 0x0429, "Persian" },
        { 0x042A, "Vietnamese" },
        { 0x042B, "Armenian" },
        { 0x042D, "Basque" },
        { 0x042F, "Macedonian" },
        { 0x0436, "Afrikaans" },
        { 0x0437, "Georgian" },
        { 0x0438, "Faroese" },
        { 0x0439, "Hindi" },
        { 0x043E, "Malay" },
        { 0x043F, "Kazakh" },
        { 0x0441, "Swahili" },
        { 0x0443, "Turkmen" },
        { 0x0444, "Tatar" },
        { 0x0445, "Bengali" },
        { 0x0446, "Punjabi" },
        { 0x0447, "Gujarati" },
        { 0x0448, "Oriya" },
        { 0x0449, "Tamil" },
        { 0x044A, "Telugu" },
        { 0x044B, "Kannada" },
        { 0x044C, "Malayalam" },
        { 0x044D, "Assamese" },
        { 0x044E, "Marathi" },
        { 0x044F, "Sanskrit" },
        { 0x0450, "Mongolian" },
        { 0x0452, "Welsh" },
        { 0x0453, "Khmer" },
        { 0x0454, "Lao" },
        { 0x0456, "Galician" },
        { 0x045B, "Sinhala" },
        { 0x0461, "Nepali" },
        { 0x0463, "Pashto" },
        { 0x0464, "Filipino" },
        { 0x0804, "Chinese (Simplified)" },
        { 0x0807, "German (Switzerland)" },
        { 0x0809, "English (United Kingdom)" },
        { 0x080C, "French (Belgium)" },
        { 0x0810, "Italian (Switzerland)" },
        { 0x0813, "Dutch (Belgium)" },
        { 0x0814, "Norwegian (Nynorsk)" },
        { 0x0816, "Portuguese (Portugal)" },
        { 0x081A, "Serbian (Latin)" },
        { 0x0C04, "Chinese (Hong Kong)" },
        { 0x0C09, "English (Australia)" },
        { 0x1004, "Chinese (Singapore)" },
        { 0x1009, "English (Canada)" },
        { 0x1404, "Chinese (Macau)" },
        { 0x1409, "English (New Zealand)" },
    };

    private static readonly Dictionary<ushort, string> MacLanguages = new()
    {
        { 0, "English" },
        { 1, "French" },
        { 2, "German" },
        { 3, "Italian" },
        { 4, "Dutch" },
        { 5, "Swedish" },
        { 6, "Spanish" },
        { 7, "Danish" },
        { 8, "Portuguese" },
        { 9, "Norwegian" },
        { 10, "Hebrew" },
        { 11, "Japanese" },
        { 12, "Arabic" },
        { 13, "Finnish" },
        { 14, "Greek" },
        { 15, "Icelandic" },
        { 17, "Turkish" },
        { 18, "Croatian" },
        { 19, "Chinese (Traditional)" },
        { 20, "Urdu" },
        { 21, "Hindi" },
        { 22, "Thai" },
        { 23, "Korean" },
        { 24, "Lithuanian" },
        { 25, "Polish" },
        { 26, "Hungarian" },
        { 27, "Estonian" },
        { 28, "Latvian" },
        { 32, "Russian" },
        { 33, "Chinese (Simplified)" },
        { 34, "Flemish" },
        { 37, "Romanian" },
        { 38, "Czech" },
        { 39, "Slovak" },
        { 40, "Slovenian" },
        { 41, "Yiddish" },
        { 42, "Serbian" },
        { 43, "Macedonian" },
        { 44, "Bulgarian" },
        { 45, "Ukrainian" },
        { 46, "Belarusian" },
        { 48, "Kazakh" },
        { 52, "Armenian" },
        { 53, "Georgian" },
        { 54, "Moldavian" },
        { 58, "Mongolian" },
        { 78, "Burmese" },
        { 79, "Khmer" },
        { 80, "Lao" },
        { 81, "Vietnamese" },
        { 82, "Indonesian" },
        { 90, "Swahili" },
    };
}
