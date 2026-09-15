using System.Text;
using System.Text.Json;

namespace WorkShopTranslationV2;

internal sealed class PrAutomation
{
    private readonly GapScanner _scanner;
    private readonly TranslationService _translationService;
    private readonly ProcessRunner _processRunner;

    public PrAutomation(GapScanner scanner, TranslationService translationService, ProcessRunner processRunner)
    {
        _scanner = scanner;
        _translationService = translationService;
        _processRunner = processRunner;
    }

    public AutomationRunResult Execute(GapCliOptions options)
    {
        var result = new AutomationRunResult
        {
            RepositoryPath = Path.GetFullPath(options.RepoPath),
            DryRun = options.DryRun
        };

        if (!Directory.Exists(result.RepositoryPath))
        {
            result.Errors.Add($"Repository path does not exist: {result.RepositoryPath}");
            result.ExitCode = 1;
            return result;
        }

        try
        {
            EnsureGitRepository(result.RepositoryPath);
            EnsureCleanWorkingTree(result.RepositoryPath);

            string repository = string.IsNullOrWhiteSpace(options.RepoOverride)
                ? ResolveRepositoryFromOrigin(result.RepositoryPath)
                : options.RepoOverride!;

            result.Repository = repository;

            string originalBranch = GetCurrentBranch(result.RepositoryPath);
            try
            {
                foreach (var language in LanguageCatalog.GetTranslationTargets(options.LanguageFilter))
                {
                    var languageResult = ProcessLanguage(options, repository, language, result.RepositoryPath);
                    result.Languages.Add(languageResult);
                }
            }
            finally
            {
                RestoreBranch(result.RepositoryPath, originalBranch, result);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            result.ExitCode = 1;
        }

        return result;
    }

    private LanguageAutomationResult ProcessLanguage(GapCliOptions options, string repository, LanguageDefinition language, string repoPath)
    {
        string branchName = $"{options.BranchPrefix}{language.FolderName}";
        var languageResult = new LanguageAutomationResult
        {
            Language = language.LanguageKey,
            LanguageFolder = language.FolderName,
            BranchName = branchName
        };

        PullRequestInfo? existingPr = GetOpenPullRequest(repository, branchName);
        languageResult.PullRequestNumber = existingPr?.Number;
        languageResult.PullRequestUrl = existingPr?.Url;
        languageResult.ReusedExistingPullRequest = existingPr is not null;

        bool remoteBranchExists = existingPr is not null || RemoteBranchExists(repoPath, branchName);
        PrepareBranch(repoPath, branchName, existingPr?.BaseRefName ?? options.BaseBranch, remoteBranchExists);

        var branchReport = _scanner.Scan(repoPath, language.LanguageKey);
        languageResult.OrphanWarnings.AddRange(branchReport.OrphanWarnings.Select(orphan => orphan.LanguageWorkshopPath));
        languageResult.MissingBefore = branchReport.Gaps.Count;

        if (branchReport.Errors.Count > 0)
        {
            languageResult.Failures.AddRange(branchReport.Errors);
            return languageResult;
        }

        if (branchReport.Gaps.Count == 0)
        {
            languageResult.Notes.Add(existingPr is not null
                ? "No remaining gaps on the existing PR branch."
                : "No gaps found for this language.");
            return languageResult;
        }

        var selectedGaps = branchReport.Gaps
            .OrderBy(gap => gap.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxFilesPerPr)
            .ToList();

        languageResult.SelectedForTranslation = selectedGaps.Count;

        if (options.DryRun)
        {
            languageResult.Notes.Add($"Dry run: would translate {selectedGaps.Count} files on branch {branchName}.");
            languageResult.FilesTouched.AddRange(selectedGaps.Select(gap => gap.TargetPath));
            languageResult.RemainingAfter = languageResult.MissingBefore;
            return languageResult;
        }

        var translatedTargetPaths = new List<string>();

        foreach (var gap in selectedGaps)
        {
            if (!File.Exists(gap.SourcePath))
            {
                languageResult.Failures.Add($"Missing english source, skipping: {gap.SourcePath}");
                continue;
            }

            try
            {
                var translationResult = _translationService.TranslateFileIfMissing(gap.SourcePath, options.Model, language);
                if (translationResult.Created && !string.IsNullOrWhiteSpace(translationResult.TargetPath))
                {
                    translatedTargetPaths.Add(translationResult.TargetPath);
                    languageResult.FilesTouched.Add(translationResult.TargetPath);
                    languageResult.TranslatedCount++;
                }
                else
                {
                    languageResult.Notes.Add(translationResult.Message);
                }
            }
            catch (Exception ex)
            {
                languageResult.Failures.Add($"{gap.SourcePath}: {ex.Message}");
            }
        }

        if (translatedTargetPaths.Count == 0)
        {
            languageResult.Notes.Add("No new files were created for this language.");
            languageResult.RemainingAfter = _scanner.Scan(repoPath, language.LanguageKey).Gaps.Count;
            return languageResult;
        }

        StageFiles(repoPath, translatedTargetPaths);
        if (!HasStagedChanges(repoPath))
        {
            languageResult.Notes.Add("No staged changes detected after translation.");
            languageResult.RemainingAfter = _scanner.Scan(repoPath, language.LanguageKey).Gaps.Count;
            return languageResult;
        }

        CommitChanges(repoPath, language, translatedTargetPaths.Count);
        PushBranch(repoPath, branchName);

        var updatedReport = _scanner.Scan(repoPath, language.LanguageKey);
        languageResult.RemainingAfter = updatedReport.Gaps.Count;

        string title = $"Auto-translate missing {language.LanguageKey} workshop content";
        string body = BuildPullRequestBody(languageResult, options, existingPr?.BaseRefName ?? options.BaseBranch);

        if (existingPr is null)
        {
            var createdPr = CreatePullRequest(repository, branchName, existingPr?.BaseRefName ?? options.BaseBranch, title, body);
            languageResult.PullRequestNumber = createdPr.Number;
            languageResult.PullRequestUrl = createdPr.Url;
        }
        else
        {
            UpdatePullRequest(repository, existingPr.Number, title, body);
        }

        return languageResult;
    }

    private void EnsureGitRepository(string repoPath)
    {
        _processRunner.Run("git", ["rev-parse", "--show-toplevel"], repoPath);
    }

    private void EnsureCleanWorkingTree(string repoPath)
    {
        var status = _processRunner.Run("git", ["status", "--porcelain"], repoPath);
        if (!string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            throw new InvalidOperationException("The workshops repo has uncommitted changes. Please start from a clean working tree.");
        }
    }

    private string ResolveRepositoryFromOrigin(string repoPath)
    {
        var remote = _processRunner.Run("git", ["remote", "get-url", "origin"], repoPath);
        string remoteUrl = remote.StandardOutput.Trim();

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            throw new InvalidOperationException("Unable to determine the origin remote URL.");
        }

        string normalized = remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? remoteUrl[..^4]
            : remoteUrl;

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["git@github.com:".Length..];
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.Trim('/').Trim();
        }

        throw new InvalidOperationException($"Could not parse GitHub repository from origin URL: {remoteUrl}");
    }

    private string GetCurrentBranch(string repoPath)
    {
        var branchResult = _processRunner.Run("git", ["rev-parse", "--abbrev-ref", "HEAD"], repoPath);
        return branchResult.StandardOutput.Trim();
    }

    private void RestoreBranch(string repoPath, string originalBranch, AutomationRunResult result)
    {
        if (string.IsNullOrWhiteSpace(originalBranch))
        {
            return;
        }

        try
        {
            _processRunner.Run("git", ["checkout", originalBranch], repoPath);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to restore original branch '{originalBranch}': {ex.Message}");
            result.ExitCode = 1;
        }
    }

    private PullRequestInfo? GetOpenPullRequest(string repository, string branchName)
    {
        var prList = _processRunner.Run(
            "gh",
            ["pr", "list", "--repo", repository, "--head", branchName, "--state", "open", "--json", "number,title,url,headRefName,baseRefName"],
            Directory.GetCurrentDirectory());

        var pullRequests = JsonSerializer.Deserialize<List<PullRequestInfo>>(prList.StandardOutput) ?? [];
        return pullRequests.FirstOrDefault(pr => pr.HeadRefName.Equals(branchName, StringComparison.OrdinalIgnoreCase));
    }

    private bool RemoteBranchExists(string repoPath, string branchName)
    {
        var result = _processRunner.Run("git", ["ls-remote", "--heads", "origin", branchName], repoPath, throwOnError: false);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private void PrepareBranch(string repoPath, string branchName, string baseBranch, bool remoteBranchExists)
    {
        if (remoteBranchExists)
        {
            _processRunner.Run("git", ["fetch", "origin", branchName], repoPath);
            _processRunner.Run("git", ["checkout", "-B", branchName, $"origin/{branchName}"], repoPath);
            return;
        }

        _processRunner.Run("git", ["fetch", "origin", baseBranch], repoPath);
        _processRunner.Run("git", ["checkout", "-B", branchName, $"origin/{baseBranch}"], repoPath);
    }

    private void StageFiles(string repoPath, IEnumerable<string> targetFiles)
    {
        var relativePaths = targetFiles
            .Select(path => Path.GetRelativePath(repoPath, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var arguments = new List<string> { "add", "--" };
        arguments.AddRange(relativePaths);
        _processRunner.Run("git", arguments, repoPath);
    }

    private bool HasStagedChanges(string repoPath)
    {
        var result = _processRunner.Run("git", ["diff", "--cached", "--name-only"], repoPath);
        return !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private void CommitChanges(string repoPath, LanguageDefinition language, int fileCount)
    {
        string commitMessage = $"Add {fileCount} {language.LanguageKey} workshop translations";
        _processRunner.Run("git", ["commit", "-m", commitMessage], repoPath);
    }

    private void PushBranch(string repoPath, string branchName)
    {
        _processRunner.Run("git", ["push", "--set-upstream", "origin", branchName], repoPath);
    }

    private PullRequestInfo CreatePullRequest(string repository, string branchName, string baseBranch, string title, string body)
    {
        var createResult = _processRunner.Run(
            "gh",
            ["pr", "create", "--repo", repository, "--base", baseBranch, "--head", branchName, "--title", title, "--body", body],
            Directory.GetCurrentDirectory());

        string url = createResult.StandardOutput.Trim();
        var prInfo = GetOpenPullRequest(repository, branchName);
        if (prInfo is not null)
        {
            return prInfo;
        }

        return new PullRequestInfo
        {
            Url = url,
            HeadRefName = branchName,
            BaseRefName = baseBranch,
            Title = title
        };
    }

    private void UpdatePullRequest(string repository, int number, string title, string body)
    {
        _processRunner.Run(
            "gh",
            ["pr", "edit", number.ToString(), "--repo", repository, "--title", title, "--body", body],
            Directory.GetCurrentDirectory());
    }

    private static string BuildPullRequestBody(LanguageAutomationResult result, GapCliOptions options, string baseBranch)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Automated translation summary");
        builder.AppendLine();
        builder.AppendLine($"- Language: {result.Language} (`{result.LanguageFolder}`)");
        builder.AppendLine($"- Base branch: {baseBranch}");
        builder.AppendLine($"- Files translated this run: {result.TranslatedCount}");
        builder.AppendLine($"- Remaining detected gaps after this run: {result.RemainingAfter}");

        if (result.Failures.Count > 0)
        {
            builder.AppendLine($"- Translation failures: {result.Failures.Count}");
        }

        builder.AppendLine();
        builder.AppendLine("## Files created");

        if (result.FilesTouched.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (string filePath in result.FilesTouched.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{filePath}`");
            }
        }

        if (result.Failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Failures");
            foreach (string failure in result.Failures)
            {
                builder.AppendLine($"- {failure}");
            }
        }

        if (result.OrphanWarnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Orphan workshop warnings");
            foreach (string orphan in result.OrphanWarnings.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{orphan}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"_Generated by WorkShopTranslationV2 with model `{options.Model}`._");
        return builder.ToString().TrimEnd();
    }
}

internal sealed class AutomationRunResult
{
    public string RepositoryPath { get; init; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public bool DryRun { get; init; }
    public int ExitCode { get; set; }
    public List<LanguageAutomationResult> Languages { get; } = [];
    public List<string> Errors { get; } = [];
}

internal sealed class LanguageAutomationResult
{
    public string Language { get; init; } = string.Empty;
    public string LanguageFolder { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public int MissingBefore { get; set; }
    public int SelectedForTranslation { get; set; }
    public int TranslatedCount { get; set; }
    public int RemainingAfter { get; set; }
    public bool ReusedExistingPullRequest { get; set; }
    public int? PullRequestNumber { get; set; }
    public string? PullRequestUrl { get; set; }
    public List<string> FilesTouched { get; } = [];
    public List<string> Failures { get; } = [];
    public List<string> Notes { get; } = [];
    public List<string> OrphanWarnings { get; } = [];
}

internal sealed class PullRequestInfo
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string HeadRefName { get; init; } = string.Empty;
    public string BaseRefName { get; init; } = string.Empty;
}

internal static class AutomationRunResultFormatter
{
    public static string Format(AutomationRunResult result)
    {
        var lines = new List<string>
        {
            $"PR automation summary for: {result.RepositoryPath}"
        };

        if (!string.IsNullOrWhiteSpace(result.Repository))
        {
            lines.Add($"Target repo: {result.Repository}");
        }

        if (result.DryRun)
        {
            lines.Add("Mode: dry-run");
        }

        foreach (var error in result.Errors)
        {
            lines.Add($"ERROR: {error}");
        }

        foreach (var language in result.Languages.OrderBy(entry => entry.Language, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {language.Language} ({language.BranchName})");
            lines.Add($"  missing before: {language.MissingBefore}");
            lines.Add($"  selected this run: {language.SelectedForTranslation}");
            lines.Add($"  translated: {language.TranslatedCount}");
            lines.Add($"  remaining after: {language.RemainingAfter}");

            if (language.ReusedExistingPullRequest)
            {
                lines.Add("  PR: reused existing open PR");
            }
            else if (!string.IsNullOrWhiteSpace(language.PullRequestUrl))
            {
                lines.Add($"  PR: {language.PullRequestUrl}");
            }

            foreach (var note in language.Notes)
            {
                lines.Add($"  note: {note}");
            }

            foreach (var failure in language.Failures)
            {
                lines.Add($"  failure: {failure}");
            }

            foreach (var orphan in language.OrphanWarnings)
            {
                lines.Add($"  orphan warning: {orphan}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
