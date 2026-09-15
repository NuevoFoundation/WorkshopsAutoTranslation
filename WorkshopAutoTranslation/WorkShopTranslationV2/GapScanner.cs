namespace WorkShopTranslationV2;

internal sealed class GapScanner
{
    public GapReport Scan(string repoPath, string? languageFilter = null)
    {
        var report = new GapReport
        {
            RepositoryPath = Path.GetFullPath(repoPath),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        if (!Directory.Exists(report.RepositoryPath))
        {
            report.Errors.Add($"Repository path does not exist: {report.RepositoryPath}");
            return report;
        }

        string englishRoot = WorkshopPaths.GetEnglishRoot(report.RepositoryPath);
        if (!Directory.Exists(englishRoot))
        {
            report.Errors.Add($"Missing english source root: {englishRoot}");
            return report;
        }

        IEnumerable<LanguageDefinition> targets;
        try
        {
            targets = LanguageCatalog.GetTranslationTargets(languageFilter);
        }
        catch (Exception ex)
        {
            report.Errors.Add(ex.Message);
            return report;
        }

        var targetLanguages = targets.ToList();
        var englishWorkshopDirectories = Directory.GetDirectories(englishRoot)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string workshopDirectory in englishWorkshopDirectories)
        {
            string workshopName = Path.GetFileName(workshopDirectory);
            foreach (string sourceFile in Directory.GetFiles(workshopDirectory, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var language in targetLanguages)
                {
                    string targetPath = WorkshopPaths.GetTranslatedFilePath(sourceFile, language.FolderName, report.RepositoryPath);
                    if (!File.Exists(targetPath))
                    {
                        report.Gaps.Add(new GapEntry
                        {
                            Language = language.LanguageKey,
                            LanguageFolder = language.FolderName,
                            Workshop = workshopName,
                            SourcePath = sourceFile,
                            TargetPath = targetPath
                        });
                    }
                }
            }
        }

        var englishWorkshops = new HashSet<string>(
            englishWorkshopDirectories.Select(Path.GetFileName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var language in targetLanguages)
        {
            string languageRoot = WorkshopPaths.GetLanguageRoot(report.RepositoryPath, language.FolderName);
            if (!Directory.Exists(languageRoot))
            {
                continue;
            }

            foreach (string workshopDirectory in Directory.GetDirectories(languageRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string workshopName = Path.GetFileName(workshopDirectory);
                if (!englishWorkshops.Contains(workshopName))
                {
                    report.OrphanWarnings.Add(new OrphanWorkshopWarning
                    {
                        Language = language.LanguageKey,
                        LanguageFolder = language.FolderName,
                        Workshop = workshopName,
                        LanguageWorkshopPath = workshopDirectory,
                        ExpectedEnglishPath = Path.Combine(englishRoot, workshopName)
                    });
                }
            }
        }

        return report;
    }
}

internal sealed class GapReport
{
    public string RepositoryPath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public List<GapEntry> Gaps { get; } = [];
    public List<OrphanWorkshopWarning> OrphanWarnings { get; } = [];
    public List<string> Errors { get; } = [];
}

internal sealed class GapEntry
{
    public string Language { get; init; } = string.Empty;
    public string LanguageFolder { get; init; } = string.Empty;
    public string Workshop { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
}

internal sealed class OrphanWorkshopWarning
{
    public string Language { get; init; } = string.Empty;
    public string LanguageFolder { get; init; } = string.Empty;
    public string Workshop { get; init; } = string.Empty;
    public string LanguageWorkshopPath { get; init; } = string.Empty;
    public string ExpectedEnglishPath { get; init; } = string.Empty;
}

internal static class GapReportFormatter
{
    public static string Format(GapReport report)
    {
        var lines = new List<string>
        {
            $"Gap scan report for: {report.RepositoryPath}",
            $"Generated at (UTC): {report.GeneratedAt:O}"
        };

        if (report.Errors.Count > 0)
        {
            lines.Add("Errors:");
            lines.AddRange(report.Errors.Select(error => $"  - {error}"));
        }

        var groupedGaps = report.Gaps
            .GroupBy(gap => gap.Language, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lines.Add($"Total gaps: {report.Gaps.Count}");
        foreach (var group in groupedGaps)
        {
            lines.Add($"  - {group.Key}: {group.Count()} missing files");
        }

        if (report.Gaps.Count > 0)
        {
            lines.Add("Missing files:");
            foreach (var gap in report.Gaps.OrderBy(gap => gap.Language, StringComparer.OrdinalIgnoreCase).ThenBy(gap => gap.SourcePath, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"  - [{gap.Language}] {gap.SourcePath} -> {gap.TargetPath}");
            }
        }

        lines.Add($"Orphan workshop warnings: {report.OrphanWarnings.Count}");
        foreach (var orphan in report.OrphanWarnings.OrderBy(orphan => orphan.Language, StringComparer.OrdinalIgnoreCase).ThenBy(orphan => orphan.Workshop, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"  - [{orphan.Language}] {orphan.LanguageWorkshopPath} has no english counterpart at {orphan.ExpectedEnglishPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
