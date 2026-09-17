using System.Reflection;

namespace WorkShopTranslationV2.Tests;

[CollectionDefinition("GapScannerContractTests", DisableParallelization = true)]
public sealed class GapScannerContractTestCollection
{
}

[Collection("GapScannerContractTests")]
public class GapScannerContractTests
{
    [Fact]
    public void GapScanner_scan_detects_missing_files_across_languages_and_skips_existing_translations()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\setup.md");
        fixture.WriteMarkdown(@"content\english\workshop-b\index.md");
        fixture.WriteMarkdown(@"content\francais\workshop-a\intro.md");
        fixture.WriteMarkdown(@"content\german\workshop-b\index.md");

        var report = WorkShopTranslationApi.ScanGaps(fixture.RootPath);

        Assert.Contains(report.Gaps, gap => gap.Language == "french"
                                            && gap.SourcePath.EndsWith(@"workshop-a\setup.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Gaps, gap => gap.Language == "spanish"
                                            && gap.SourcePath.EndsWith(@"workshop-b\index.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Gaps, gap => gap.Language == "french"
                                                  && gap.SourcePath.EndsWith(@"workshop-a\intro.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Gaps, gap => gap.Language == "german"
                                                  && gap.SourcePath.EndsWith(@"workshop-b\index.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GapScanner_scan_flags_orphan_workshops_without_matching_english_source()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        fixture.WriteMarkdown(@"content\espanol\orphan-workshop\index.md");
        fixture.WriteMarkdown(@"content\francais\workshop-a\intro.md");

        var report = WorkShopTranslationApi.ScanGaps(fixture.RootPath);

        Assert.Contains(report.OrphanWarnings, orphan => orphan.Language == "spanish"
                                                         && orphan.Workshop == "orphan-workshop");
        Assert.DoesNotContain(report.OrphanWarnings, orphan => orphan.Language == "french"
                                                               && orphan.Workshop == "workshop-a");
    }

    [Fact]
    public void GapScanner_scan_reports_missing_english_root_without_throwing()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\espanol\orphan-workshop\index.md");

        var report = WorkShopTranslationApi.ScanGaps(fixture.RootPath);

        Assert.Empty(report.Gaps);
        Assert.Contains(report.Errors, error => error.Contains("Missing english source root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GapScanner_scan_uses_current_branch_state_to_skip_already_present_translations()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\setup.md");
        fixture.WriteMarkdown(@"content\francais\workshop-a\setup.md");

        var report = WorkShopTranslationApi.ScanGaps(fixture.RootPath, languageFilter: "french");

        Assert.DoesNotContain(report.Gaps, gap => gap.SourcePath.EndsWith(@"workshop-a\setup.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Gaps, gap => gap.SourcePath.EndsWith(@"workshop-a\intro.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrAutomation_dry_run_selects_multiple_small_workshops_atomically_when_they_fit_under_the_workshop_cap()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\01-first.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\02-second.md");
        fixture.WriteMarkdown(@"content\english\workshop-b\01-third.md");
        fixture.WriteMarkdown(@"content\english\workshop-c\01-fourth.md");
        fixture.InitializeGitRepository();

        var result = WorkShopTranslationApi.RunDryPrAutomation(fixture.RootPath, "spanish", maxWorkshopsPerPr: 8);
        Assert.True(result.Errors.Count == 0, $"Automation errors: {string.Join(" | ", result.Errors)}");
        var language = Assert.Single(result.Languages);

        Assert.Equal("spanish", language.Language);
        Assert.Equal(4, language.MissingBefore);
        Assert.Equal(4, language.SelectedForTranslation);
        Assert.Equal(4, language.RemainingAfter);
        Assert.Equal(
            new[]
            {
                @"content\espanol\workshop-a\01-first.md",
                @"content\espanol\workshop-a\02-second.md",
                @"content\espanol\workshop-b\01-third.md",
                @"content\espanol\workshop-c\01-fourth.md",
            },
            language.FilesTouched
                .Select(path => Normalize(Path.GetRelativePath(fixture.RootPath, path)))
                .ToArray());
        Assert.Contains(language.Notes, note => note.Contains("Selected 3 whole workshop(s)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrAutomation_dry_run_selects_all_files_from_selected_workshops_even_when_one_workshop_is_large()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\01-first.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\02-second.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\03-third.md");
        fixture.WriteMarkdown(@"content\english\workshop-b\01-fourth.md");
        fixture.InitializeGitRepository();

        var result = WorkShopTranslationApi.RunDryPrAutomation(fixture.RootPath, "spanish", maxWorkshopsPerPr: 1);
        Assert.Empty(result.Errors);
        var language = Assert.Single(result.Languages);

        Assert.Equal(4, language.MissingBefore);
        Assert.Equal(3, language.SelectedForTranslation);
        Assert.Equal(4, language.RemainingAfter);
        Assert.Equal(
            new[]
            {
                @"content\espanol\workshop-a\01-first.md",
                @"content\espanol\workshop-a\02-second.md",
                @"content\espanol\workshop-a\03-third.md",
            },
            language.FilesTouched
                .Select(path => Normalize(Path.GetRelativePath(fixture.RootPath, path)))
                .ToArray());
        Assert.Contains(language.Notes, note => note.Contains("Selected 1 whole workshop(s) for translation and deferred 1 workshop(s) (1 file(s))", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrAutomation_dry_run_limits_selection_by_workshop_count_and_defers_the_rest()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\01-first.md");
        fixture.WriteMarkdown(@"content\english\workshop-b\01-second.md");
        fixture.WriteMarkdown(@"content\english\workshop-b\02-third.md");
        fixture.WriteMarkdown(@"content\english\workshop-c\01-fourth.md");
        fixture.WriteMarkdown(@"content\english\workshop-d\01-fifth.md");
        fixture.WriteMarkdown(@"content\english\workshop-e\01-sixth.md");
        fixture.InitializeGitRepository();

        var result = WorkShopTranslationApi.RunDryPrAutomation(fixture.RootPath, "spanish", maxWorkshopsPerPr: 2);
        Assert.Empty(result.Errors);
        var language = Assert.Single(result.Languages);

        Assert.Equal(6, language.MissingBefore);
        Assert.Equal(3, language.SelectedForTranslation);
        Assert.Equal(6, language.RemainingAfter);
        Assert.Equal(
            new[]
            {
                @"content\espanol\workshop-a\01-first.md",
                @"content\espanol\workshop-b\01-second.md",
                @"content\espanol\workshop-b\02-third.md",
            },
            language.FilesTouched
                .Select(path => Normalize(Path.GetRelativePath(fixture.RootPath, path)))
                .ToArray());
        Assert.DoesNotContain(language.FilesTouched, path => path.Contains(@"\workshop-c\", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(language.FilesTouched, path => path.Contains(@"\workshop-d\", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(language.FilesTouched, path => path.Contains(@"\workshop-e\", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(language.Notes, note => note.Contains("deferred 3 workshop(s) (3 file(s))", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrAutomation_dry_run_reuses_existing_open_pull_request_from_camel_case_gh_json()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        fixture.InitializeGitRepository();
        fixture.CreateAndPushBranch("auto-translate/espanol");

        var result = WorkShopTranslationApi.RunDryPrAutomation(
            fixture.RootPath,
            "spanish",
            maxWorkshopsPerPr: 5,
            (_, _) => """[{"number":42,"title":"Existing PR","url":"https://github.com/NuevoFoundation/workshops/pull/42","headRefName":"auto-translate/espanol","baseRefName":"master"}]""");

        Assert.Empty(result.Errors);
        var language = Assert.Single(result.Languages);
        Assert.True(language.ReusedExistingPullRequest);
        Assert.Equal(42, language.PullRequestNumber);
        Assert.Equal("https://github.com/NuevoFoundation/workshops/pull/42", language.PullRequestUrl);
        Assert.Contains(language.Notes, note => note.Contains("Dry run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrAutomation_dry_run_records_language_failure_and_continues_processing_remaining_languages()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        fixture.InitializeGitRepository();

        var result = WorkShopTranslationApi.RunDryPrAutomation(
            fixture.RootPath,
            languageFilter: null,
            maxWorkshopsPerPr: 5,
            (_, branchName) => branchName.Equals("auto-translate/espanol", StringComparison.OrdinalIgnoreCase)
                ? throw new InvalidOperationException("simulated gh failure for spanish")
                : "[]");

        Assert.Empty(result.Errors);

        var spanish = Assert.Single(result.Languages, language => language.Language == "spanish");
        Assert.Contains(spanish.Failures, failure => failure.Contains("simulated gh failure for spanish", StringComparison.OrdinalIgnoreCase));

        var french = Assert.Single(result.Languages, language => language.Language == "french");
        Assert.Empty(french.Failures);
        Assert.Contains(french.Notes, note => note.Contains("Dry run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkshopPaths_prefers_repo_anchored_english_root_when_repo_path_contains_english_segment()
    {
        using var fixture = new ContentFixture(includeEnglishSegmentInRoot: true);

        string sourcePath = fixture.WriteMarkdown(@"content\english\dotnet\intro.md");

        string translatedPath = WorkShopTranslationApi.GetTranslatedFilePath(sourcePath, "espanol", fixture.RootPath);

        Assert.Equal(
            Path.Combine(fixture.RootPath, @"content\espanol\dotnet\intro.md"),
            translatedPath);
    }

    [Fact]
    public void TranslationService_translate_file_if_missing_uses_repo_anchored_target_path()
    {
        using var fixture = new ContentFixture(includeEnglishSegmentInRoot: true);

        string sourcePath = fixture.WriteMarkdown(@"content\english\workshop-a\intro.md");
        string expectedTargetPath = fixture.WriteMarkdown(@"content\espanol\workshop-a\intro.md");

        var result = WorkShopTranslationApi.TranslateFileIfMissing(sourcePath, "gpt-4o", "spanish", fixture.RootPath);

        Assert.True(result.SkippedExisting);
        Assert.Equal(expectedTargetPath, result.TargetPath);
        Assert.Contains(expectedTargetPath, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('/', '\\');
}

internal sealed class ContentFixture : IDisposable
{
    private readonly List<string> _cleanupPaths = [];

    public ContentFixture(bool includeEnglishSegmentInRoot = false)
    {
        string fixtureRoot = Path.Combine(AppContext.BaseDirectory, "test-fixtures");
        RootPath = includeEnglishSegmentInRoot
            ? Path.Combine(fixtureRoot, "english", Guid.NewGuid().ToString("N"))
            : Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        RemotePath = $"{RootPath}-remote.git";
        _cleanupPaths.Add(RemotePath);
    }

    public string RootPath { get; }

    public string RemotePath { get; }

    public string WriteMarkdown(string relativePath, string contents = "# sample")
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    public void InitializeGitRepository()
    {
        RunProcess("git", "init -b master");
        RunProcess("git", "config user.email tester@example.com");
        RunProcess("git", "config user.name Tester");
        RunProcess("git", "add .");
        RunProcess("git", "commit -m \"initial fixture\"");
        RunProcess("git", $"init --bare \"{RemotePath}\"", RootPath);
        RunProcess("git", $"remote add origin \"{RemotePath}\"");
        RunProcess("git", "push -u origin master");
    }

    public void CreateAndPushBranch(string branchName)
    {
        RunProcess("git", $"checkout -b \"{branchName}\"");
        RunProcess("git", $"push -u origin \"{branchName}\"");
        RunProcess("git", "checkout master");
    }

    public void Dispose()
    {
        DeleteDirectoryIfPresent(RootPath);

        foreach (string cleanupPath in _cleanupPaths)
        {
            DeleteDirectoryIfPresent(cleanupPath);
        }
    }

    private void RunProcess(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed: {fileName} {arguments}{Environment.NewLine}" +
                $"StdOut: {standardOutput}{Environment.NewLine}" +
                $"StdErr: {standardError}");
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}

internal sealed record GapReportView(
    IReadOnlyList<GapEntryView> Gaps,
    IReadOnlyList<OrphanWarningView> OrphanWarnings,
    IReadOnlyList<string> Errors);

internal sealed record GapEntryView(string Language, string Workshop, string SourcePath, string TargetPath);

internal sealed record OrphanWarningView(string Language, string Workshop, string LanguageWorkshopPath, string ExpectedEnglishPath);

internal sealed record AutomationRunResultView(IReadOnlyList<LanguageAutomationResultView> Languages, IReadOnlyList<string> Errors);

internal sealed record LanguageAutomationResultView(
    string Language,
    int MissingBefore,
    int SelectedForTranslation,
    int RemainingAfter,
    bool ReusedExistingPullRequest,
    int? PullRequestNumber,
    string? PullRequestUrl,
    IReadOnlyList<string> FilesTouched,
    IReadOnlyList<string> OrphanWarnings,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Failures);

internal sealed record TranslationResultView(bool Created, bool SkippedExisting, string? TargetPath, string Message);

internal static class WorkShopTranslationApi
{
    private static readonly Assembly TargetAssembly = Assembly.Load("WorkShopTranslationV2");

    public static GapReportView ScanGaps(string repoPath, string? languageFilter = null)
    {
        var scannerType = RequireType("GapScanner");
        var scanner = Activator.CreateInstance(scannerType) ?? throw new InvalidOperationException("Could not construct GapScanner.");
        var method = scannerType.GetMethod("Scan", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("GapScanner.Scan was not found.");

        var report = method.Invoke(scanner, [repoPath, languageFilter])
            ?? throw new InvalidOperationException("GapScanner.Scan returned null.");

        return ReadGapReport(report);
    }

    public static AutomationRunResultView RunDryPrAutomation(
        string repoPath,
        string? languageFilter,
        int maxWorkshopsPerPr,
        Func<string, string, string>? openPullRequestJsonProvider = null)
    {
        var options = ParseGapCliOptions(repoPath, languageFilter, maxWorkshopsPerPr);
        var automationType = RequireType("PrAutomation");
        var scanner = Activator.CreateInstance(RequireType("GapScanner")) ?? throw new InvalidOperationException("Could not construct GapScanner.");
        var translationService = Activator.CreateInstance(RequireType("TranslationService"), [null]) ?? throw new InvalidOperationException("Could not construct TranslationService.");
        var processRunner = Activator.CreateInstance(RequireType("ProcessRunner")) ?? throw new InvalidOperationException("Could not construct ProcessRunner.");
        var automation = Activator.CreateInstance(automationType, [scanner, translationService, processRunner, openPullRequestJsonProvider])
            ?? throw new InvalidOperationException("Could not construct PrAutomation.");

        var execute = automationType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("PrAutomation.Execute was not found.");

        var result = execute.Invoke(automation, [options])
            ?? throw new InvalidOperationException("PrAutomation.Execute returned null.");

        return ReadAutomationResult(result);
    }

    public static string GetTranslatedFilePath(string sourceFilePath, string targetLanguageFolder, string repoPath)
    {
        var workshopPathsType = RequireType("WorkshopPaths");
        var method = workshopPathsType.GetMethod("GetTranslatedFilePath", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("WorkshopPaths.GetTranslatedFilePath was not found.");

        return method.Invoke(null, [sourceFilePath, targetLanguageFolder, repoPath])?.ToString()
            ?? throw new InvalidOperationException("WorkshopPaths.GetTranslatedFilePath returned null.");
    }

    public static TranslationResultView TranslateFileIfMissing(string sourceFilePath, string model, string languageInput, string repoPath)
    {
        if (!TryResolveLanguage(languageInput, out var language))
        {
            throw new InvalidOperationException($"Unsupported language '{languageInput}'.");
        }

        var translationServiceType = RequireType("TranslationService");
        var translationService = Activator.CreateInstance(translationServiceType, [null])
            ?? throw new InvalidOperationException("Could not construct TranslationService.");

        var method = translationServiceType.GetMethod("TranslateFileIfMissing", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("TranslationService.TranslateFileIfMissing was not found.");

        var result = method.Invoke(translationService, [sourceFilePath, model, language, repoPath])
            ?? throw new InvalidOperationException("TranslationService.TranslateFileIfMissing returned null.");

        return new TranslationResultView(
            ReadBool(result, "Created"),
            ReadBool(result, "SkippedExisting"),
            RequireProperty(result, "TargetPath").GetValue(result)?.ToString(),
            ReadString(result, "Message"));
    }

    private static object ParseGapCliOptions(string repoPath, string? languageFilter, int maxWorkshopsPerPr)
    {
        var optionsType = RequireType("GapCliOptions");
        var tryParse = optionsType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GapCliOptions.TryParse was not found.");

        var commandLineArgs = new List<string>
        {
            "--scan-gaps",
            repoPath,
            "--create-prs",
            "--dry-run",
            "--max-workshops-per-pr",
            maxWorkshopsPerPr.ToString(),
            "--repo",
            "NuevoFoundation/workshops"
        };

        if (!string.IsNullOrWhiteSpace(languageFilter))
        {
            commandLineArgs.Add("--language");
            commandLineArgs.Add(languageFilter);
        }

        object?[] args = [commandLineArgs.ToArray(), null, string.Empty];

        var parsed = (bool)(tryParse.Invoke(null, args) ?? false);
        if (!parsed)
        {
            throw new InvalidOperationException($"GapCliOptions.TryParse failed: {args[2]}");
        }

        return args[1] ?? throw new InvalidOperationException("GapCliOptions.TryParse did not return options.");
    }

    private static GapReportView ReadGapReport(object report)
    {
        return new GapReportView(
            ReadObjectList(report, "Gaps", item => new GapEntryView(
                ReadString(item, "Language"),
                ReadString(item, "Workshop"),
                ReadString(item, "SourcePath"),
                ReadString(item, "TargetPath"))),
            ReadObjectList(report, "OrphanWarnings", item => new OrphanWarningView(
                ReadString(item, "Language"),
                ReadString(item, "Workshop"),
                ReadString(item, "LanguageWorkshopPath"),
                ReadString(item, "ExpectedEnglishPath"))),
            ReadStringList(report, "Errors"));
    }

    private static AutomationRunResultView ReadAutomationResult(object result)
    {
        return new AutomationRunResultView(
            ReadObjectList(result, "Languages", item => new LanguageAutomationResultView(
                ReadString(item, "Language"),
                ReadInt(item, "MissingBefore"),
                ReadInt(item, "SelectedForTranslation"),
                ReadInt(item, "RemainingAfter"),
                ReadBool(item, "ReusedExistingPullRequest"),
                ReadNullableInt(item, "PullRequestNumber"),
                RequireProperty(item, "PullRequestUrl").GetValue(item)?.ToString(),
                ReadStringList(item, "FilesTouched"),
                ReadStringList(item, "OrphanWarnings"),
                ReadStringList(item, "Notes"),
                ReadStringList(item, "Failures"))),
            ReadStringList(result, "Errors"));
    }

    private static IReadOnlyList<T> ReadObjectList<T>(object owner, string propertyName, Func<object, T> map)
    {
        var raw = RequireProperty(owner, propertyName).GetValue(owner) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException($"Property '{propertyName}' on '{owner.GetType().Name}' is not enumerable.");

        return raw.Cast<object>().Select(map).ToList();
    }

    private static IReadOnlyList<string> ReadStringList(object owner, string propertyName)
    {
        var raw = RequireProperty(owner, propertyName).GetValue(owner) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException($"Property '{propertyName}' on '{owner.GetType().Name}' is not enumerable.");

        return raw.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToList();
    }

    private static string ReadString(object owner, string propertyName) =>
        RequireProperty(owner, propertyName).GetValue(owner)?.ToString() ?? string.Empty;

    private static bool ReadBool(object owner, string propertyName) =>
        (bool)(RequireProperty(owner, propertyName).GetValue(owner)
            ?? throw new InvalidOperationException($"Property '{propertyName}' on '{owner.GetType().Name}' was null."));

    private static int ReadInt(object owner, string propertyName) =>
        (int)(RequireProperty(owner, propertyName).GetValue(owner)
            ?? throw new InvalidOperationException($"Property '{propertyName}' on '{owner.GetType().Name}' was null."));

    private static int? ReadNullableInt(object owner, string propertyName) =>
        (int?)RequireProperty(owner, propertyName).GetValue(owner);

    private static PropertyInfo RequireProperty(object owner, string propertyName) =>
        owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{owner.GetType().FullName}'.");

    private static Type RequireType(string name) =>
        TargetAssembly.GetTypes().FirstOrDefault(type => type.Name == name)
        ?? throw new InvalidOperationException($"Type '{name}' was not found in WorkShopTranslationV2.");

    private static bool TryResolveLanguage(string input, out object language)
    {
        var catalogType = RequireType("LanguageCatalog");
        var method = catalogType.GetMethod("TryResolveLanguage", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("LanguageCatalog.TryResolveLanguage was not found.");

        object?[] args = [input, null];
        bool resolved = (bool)(method.Invoke(null, args) ?? false);
        language = args[1] ?? throw new InvalidOperationException("LanguageCatalog.TryResolveLanguage returned null language.");
        return resolved;
    }
}
