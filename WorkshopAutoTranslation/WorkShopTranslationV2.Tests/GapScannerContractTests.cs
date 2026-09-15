using System.Reflection;

namespace WorkShopTranslationV2.Tests;

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
    public void PrAutomation_dry_run_honors_max_files_per_pr_with_deterministic_order()
    {
        using var fixture = new ContentFixture();

        fixture.WriteMarkdown(@"content\english\workshop-z\02-second.md");
        fixture.WriteMarkdown(@"content\english\workshop-a\01-first.md");
        fixture.WriteMarkdown(@"content\english\workshop-z\03-third.md");
        fixture.InitializeGitRepository();

        var result = WorkShopTranslationApi.RunDryPrAutomation(fixture.RootPath, "spanish", maxFilesPerPr: 2);
        Assert.True(result.Errors.Count == 0, $"Automation errors: {string.Join(" | ", result.Errors)}");
        var language = Assert.Single(result.Languages);

        Assert.Equal("spanish", language.Language);
        Assert.Equal(3, language.MissingBefore);
        Assert.Equal(2, language.SelectedForTranslation);
        Assert.Equal(3, language.RemainingAfter);
        Assert.Equal(
            new[]
            {
                @"content\espanol\workshop-a\01-first.md",
                @"content\espanol\workshop-z\02-second.md",
            },
            language.FilesTouched
                .Select(path => Normalize(Path.GetRelativePath(fixture.RootPath, path)))
                .ToArray());
    }

    private static string Normalize(string path) => path.Replace('/', '\\');
}

internal sealed class ContentFixture : IDisposable
{
    public ContentFixture()
    {
        RootPath = Path.Combine(AppContext.BaseDirectory, "test-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        RemotePath = $"{RootPath}-remote.git";
    }

    public string RootPath { get; }

    public string RemotePath { get; }

    public void WriteMarkdown(string relativePath, string contents = "# sample")
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
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

    public void Dispose()
    {
        DeleteDirectoryIfPresent(RootPath);
        DeleteDirectoryIfPresent(RemotePath);
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
    IReadOnlyList<string> FilesTouched,
    IReadOnlyList<string> OrphanWarnings,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Failures);

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

    public static AutomationRunResultView RunDryPrAutomation(string repoPath, string languageFilter, int maxFilesPerPr)
    {
        var options = ParseGapCliOptions(repoPath, languageFilter, maxFilesPerPr);
        var automationType = RequireType("PrAutomation");
        var scanner = Activator.CreateInstance(RequireType("GapScanner")) ?? throw new InvalidOperationException("Could not construct GapScanner.");
        var translationService = Activator.CreateInstance(RequireType("TranslationService"), [null]) ?? throw new InvalidOperationException("Could not construct TranslationService.");
        var processRunner = Activator.CreateInstance(RequireType("ProcessRunner")) ?? throw new InvalidOperationException("Could not construct ProcessRunner.");
        var automation = Activator.CreateInstance(automationType, [scanner, translationService, processRunner])
            ?? throw new InvalidOperationException("Could not construct PrAutomation.");

        var execute = automationType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("PrAutomation.Execute was not found.");

        var result = execute.Invoke(automation, [options])
            ?? throw new InvalidOperationException("PrAutomation.Execute returned null.");

        return ReadAutomationResult(result);
    }

    private static object ParseGapCliOptions(string repoPath, string languageFilter, int maxFilesPerPr)
    {
        var optionsType = RequireType("GapCliOptions");
        var tryParse = optionsType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GapCliOptions.TryParse was not found.");

        object?[] args =
        [
            new[]
            {
                "--scan-gaps",
                repoPath,
                "--create-prs",
                "--dry-run",
                "--language",
                languageFilter,
                "--max-files-per-pr",
                maxFilesPerPr.ToString(),
                "--repo",
                "NuevoFoundation/workshops"
            },
            null,
            string.Empty
        ];

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

    private static int ReadInt(object owner, string propertyName) =>
        (int)(RequireProperty(owner, propertyName).GetValue(owner)
            ?? throw new InvalidOperationException($"Property '{propertyName}' on '{owner.GetType().Name}' was null."));

    private static PropertyInfo RequireProperty(object owner, string propertyName) =>
        owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{owner.GetType().FullName}'.");

    private static Type RequireType(string name) =>
        TargetAssembly.GetTypes().FirstOrDefault(type => type.Name == name)
        ?? throw new InvalidOperationException($"Type '{name}' was not found in WorkShopTranslationV2.");
}
