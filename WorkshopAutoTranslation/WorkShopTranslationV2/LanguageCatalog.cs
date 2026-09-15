namespace WorkShopTranslationV2;

internal static class LanguageCatalog
{
    private static readonly IReadOnlyDictionary<string, string> SupportedLanguagesInternal =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["french"] = "francais",
            ["spanish"] = "espanol",
            ["english"] = "english",
            ["german"] = "german",
            ["portuguese"] = "brazilian-portuguese",
            ["kyrgyz"] = "kyrgyz",
            ["simplified-chinese"] = "simplified-chinese",
            ["traditional-chinese"] = "traditional-chinese"
        };

    public static IReadOnlyDictionary<string, string> SupportedLanguages => SupportedLanguagesInternal;

    public static IEnumerable<string> GetSupportedLanguageKeys() =>
        SupportedLanguagesInternal.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<LanguageDefinition> GetTranslationTargets(string? filter = null)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return SupportedLanguagesInternal
                .Where(entry => !entry.Key.Equals("english", StringComparison.OrdinalIgnoreCase))
                .Select(entry => new LanguageDefinition(entry.Key, entry.Value))
                .OrderBy(entry => entry.LanguageKey, StringComparer.OrdinalIgnoreCase);
        }

        if (!TryResolveLanguage(filter, out var language))
        {
            throw new ArgumentException($"Unsupported language '{filter}'.");
        }

        if (language.IsEnglish)
        {
            throw new ArgumentException("English is the source language and cannot be used as a translation target.");
        }

        return [language];
    }

    public static bool TryResolveLanguage(string input, out LanguageDefinition language)
    {
        if (SupportedLanguagesInternal.TryGetValue(input, out var folder))
        {
            language = new LanguageDefinition(input.ToLowerInvariant(), folder);
            return true;
        }

        var match = SupportedLanguagesInternal
            .FirstOrDefault(entry => entry.Value.Equals(input, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(match.Key))
        {
            language = new LanguageDefinition(match.Key, match.Value);
            return true;
        }

        language = default;
        return false;
    }
}

internal readonly record struct LanguageDefinition(string LanguageKey, string FolderName)
{
    public bool IsEnglish => LanguageKey.Equals("english", StringComparison.OrdinalIgnoreCase);
}
