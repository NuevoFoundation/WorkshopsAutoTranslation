namespace WorkShopTranslationV2;

internal static class WorkshopPaths
{
    private const string ContentFolderName = "content";
    private const string EnglishFolderName = "english";

    public static string GetEnglishRoot(string repoPath) =>
        Path.Combine(Path.GetFullPath(repoPath), ContentFolderName, EnglishFolderName);

    public static string GetLanguageRoot(string repoPath, string languageFolder) =>
        Path.Combine(Path.GetFullPath(repoPath), ContentFolderName, languageFolder);

    public static string GetTranslatedFilePath(string sourceFilePath, string targetLanguageFolder, string? repoPath = null)
    {
        string fullSourcePath = Path.GetFullPath(sourceFilePath);
        string normalizedEnglishSegment = $"{Path.DirectorySeparatorChar}{EnglishFolderName}{Path.DirectorySeparatorChar}";
        int segmentIndex = fullSourcePath.IndexOf(normalizedEnglishSegment, StringComparison.OrdinalIgnoreCase);

        if (segmentIndex >= 0)
        {
            string prefix = fullSourcePath[..segmentIndex];
            string suffix = fullSourcePath[(segmentIndex + normalizedEnglishSegment.Length)..];
            return Path.Combine(prefix, targetLanguageFolder, suffix);
        }

        if (!string.IsNullOrWhiteSpace(repoPath))
        {
            string englishRoot = GetEnglishRoot(repoPath);
            if (fullSourcePath.StartsWith(englishRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = Path.GetRelativePath(englishRoot, fullSourcePath);
                return Path.Combine(GetLanguageRoot(repoPath, targetLanguageFolder), relativePath);
            }
        }

        return fullSourcePath.Replace("english", targetLanguageFolder, StringComparison.OrdinalIgnoreCase);
    }
}
