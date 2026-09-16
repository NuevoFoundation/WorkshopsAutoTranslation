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

        if (!string.IsNullOrWhiteSpace(repoPath))
        {
            string englishRoot = GetEnglishRoot(repoPath);
            string relativePath = Path.GetRelativePath(englishRoot, fullSourcePath);
            if (!Path.IsPathRooted(relativePath)
                && !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                return Path.Combine(GetLanguageRoot(repoPath, targetLanguageFolder), relativePath);
            }
        }

        string normalizedEnglishSegment = $"{Path.DirectorySeparatorChar}{EnglishFolderName}{Path.DirectorySeparatorChar}";
        int segmentIndex = fullSourcePath.IndexOf(normalizedEnglishSegment, StringComparison.OrdinalIgnoreCase);

        if (segmentIndex >= 0)
        {
            string prefix = fullSourcePath[..segmentIndex];
            string suffix = fullSourcePath[(segmentIndex + normalizedEnglishSegment.Length)..];
            return Path.Combine(prefix, targetLanguageFolder, suffix);
        }

        return fullSourcePath.Replace("english", targetLanguageFolder, StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryResolveRepositoryRoot(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string searchPath = File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath) ?? fullPath
            : fullPath;

        for (var current = new DirectoryInfo(searchPath); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ContentFolderName, EnglishFolderName)))
            {
                return current.FullName;
            }
        }

        return null;
    }
}
