using projectFrameCut.Drawing.Text.FontHelper.Reader;
using projectFrameCut.Drawing.Text.FontHelper.Table;

namespace projectFrameCut.Drawing.Text.FontHelper;

/// <summary>
/// 轻量级字体元数据 —— 仅通过读取字体文件的 'name' 表获得，
/// 不加载完整的字形数据、度量信息等，避免大内存占用。
/// </summary>
public readonly struct FontFaceMetadata
{
    /// <summary>字体家族名，如 "Arial"。</summary>
    public string FamilyName { get; init; }

    /// <summary>子家族名，如 "Regular" / "Bold"。</summary>
    public string SubfamilyName { get; init; }

    /// <summary>唯一标识名（Name ID 3 或 6），如 "Monotype:Arial Bold:1990"。</summary>
    public string? UniqueName { get; init; }

    /// <summary>本地化显示名称，优先使用非英语名称。</summary>
    public string DisplayName { get; init; }

    /// <summary>BCP-47 语言标签，如 "zh-CN"、"ja-JP"。</summary>
    public string? PrimaryLanguageTag { get; init; }
}

/// <summary>
/// 从字体文件中仅读取名称表元数据，不加载完整 <see cref="FontFace"/>。
/// 支持 .ttf、.otf 和 .ttc 格式。
/// </summary>
public static class FontMetadataReader
{
    /// <summary>从 .ttf / .otf 文件中读取字体元数据。</summary>
    public static FontFaceMetadata ReadFromFile(string path)
    {
        var data = File.ReadAllBytes(path);
        return ReadFromBytes(data, 0, path);
    }

    /// <summary>从 .ttc 文件中读取所有子字体的元数据。</summary>
    public static FontFaceMetadata[] ReadFromTtc(string path)
    {
        var collection = FontCollection.Load(path);
        var result = new FontFaceMetadata[collection.Count];
        int i = 0;
        foreach (var info in collection)
        {
            result[i++] = new FontFaceMetadata
            {
                FamilyName = info.FamilyName,
                SubfamilyName = info.SubfamilyName,
                UniqueName = info.UniqueName,
                DisplayName = info.DisplayName,
                PrimaryLanguageTag = info.PrimaryLanguageTag,
            };
        }
        return result;
    }

    /// <summary>从内存中的字体数据（TTF/OTF 子字体，非 TTC）读取元数据。</summary>
    internal static FontFaceMetadata ReadFromBytes(byte[] data, int sfntOffset, string? sourcePath)
    {
        using var reader = new SfntReader(data, sfntOffset, sourcePath);

        // 使用内部重载以获取完整的 NameData（含 records），
        // 这样才能获取本地化名称和语言标签。
        if (!reader.TryGetTableEntry("name", out var fontData, out var nameOffset, out var nameLength))
            throw new InvalidFontFileException("Required 'name' table not found.");

        var nameData = NameTable.Parse(fontData, (int)nameOffset, (int)nameLength);

        var familyName = nameData.GetName(16) ?? nameData.GetName(1) ?? "Unknown";
        var subfamilyName = nameData.GetName(17) ?? nameData.GetName(2) ?? "Regular";
        var uniqueName = nameData.GetName(3) ?? nameData.GetName(6);

        var targetLanguages = nameData.GetTargetLanguages().ToArray();
        var localizedNames = new Dictionary<TargetLanguage, string>();
        foreach (var lang in targetLanguages)
        {
            var localizedName = nameData.GetLocalizedName(16, lang)
                             ?? nameData.GetLocalizedName(1, lang);
            if (localizedName != null)
                localizedNames[lang] = localizedName;
        }

        // 计算 DisplayName：优先取非英语的本地化名称
        var displayName = familyName;
        foreach (var (lang, name) in localizedNames)
        {
            if (lang.PlatformId == 3 && lang.LanguageId == 0x0409) continue;
            if (lang.PlatformId == 1 && lang.LanguageId == 0) continue;
            displayName = name;
            break;
        }

        // 计算 PrimaryLanguageTag
        string? primaryLanguageTag = null;
        foreach (var lang in targetLanguages)
        {
            if (lang.PlatformId == 3 && lang.LanguageId == 0x0409) continue;
            if (lang.PlatformId == 1 && lang.LanguageId == 0) continue;
            primaryLanguageTag = lang.ToBcp47Tag();
            break;
        }

        return new FontFaceMetadata
        {
            FamilyName = familyName,
            SubfamilyName = subfamilyName,
            UniqueName = uniqueName,
            DisplayName = displayName,
            PrimaryLanguageTag = primaryLanguageTag,
        };
    }
}
