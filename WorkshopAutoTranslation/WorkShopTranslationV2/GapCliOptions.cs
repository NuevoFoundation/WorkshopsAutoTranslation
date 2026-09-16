namespace WorkShopTranslationV2;

internal sealed class GapCliOptions
{
    public string RepoPath { get; init; } = string.Empty;
    public bool ReportOnly { get; init; }
    public bool CreatePrs { get; init; }
    public OutputFormat Format { get; init; } = OutputFormat.Text;
    public string Model { get; init; } = "gpt-4o";
    public string? LanguageFilter { get; init; }
    public int MaxFilesPerPr { get; init; } = 100;
    public string BaseBranch { get; init; } = "master";
    public string BranchPrefix { get; init; } = "auto-translate/";
    public bool DryRun { get; init; }
    public string? RepoOverride { get; init; }

    public static bool IsGapScanCommand(string[] args) =>
        args.Length > 0 && args[0].Equals("--scan-gaps", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string[] args, out GapCliOptions options, out string errorMessage)
    {
        options = new GapCliOptions();
        errorMessage = string.Empty;

        if (!IsGapScanCommand(args))
        {
            errorMessage = "Missing --scan-gaps entry point.";
            return false;
        }

        if (args.Length < 3)
        {
            errorMessage = "Please provide the workshops repo path and one of --report or --create-prs.";
            return false;
        }

        string repoPath = args[1];
        bool reportOnly = false;
        bool createPrs = false;
        OutputFormat format = OutputFormat.Text;
        string model = "gpt-4o";
        string? languageFilter = null;
        int maxFilesPerPr = 100;
        string baseBranch = "master";
        string branchPrefix = "auto-translate/";
        bool dryRun = false;
        string? repoOverride = null;

        for (int index = 2; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--report":
                    reportOnly = true;
                    break;
                case "--create-prs":
                    createPrs = true;
                    break;
                case "--format":
                    if (!TryReadValue(args, ref index, out var formatValue))
                    {
                        errorMessage = "Missing value for --format.";
                        return false;
                    }

                    if (!formatValue.Equals("json", StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage = "Only --format json is supported.";
                        return false;
                    }

                    format = OutputFormat.Json;
                    break;
                case "--model":
                    if (!TryReadValue(args, ref index, out model))
                    {
                        errorMessage = "Missing value for --model.";
                        return false;
                    }

                    break;
                case "--language":
                    if (!TryReadValue(args, ref index, out languageFilter))
                    {
                        errorMessage = "Missing value for --language.";
                        return false;
                    }

                    if (!LanguageCatalog.TryResolveLanguage(languageFilter, out var language) || language.IsEnglish)
                    {
                        errorMessage = $"Unsupported translation target language '{languageFilter}'.";
                        return false;
                    }

                    break;
                case "--max-files-per-pr":
                    if (!TryReadValue(args, ref index, out var maxFilesValue) || !int.TryParse(maxFilesValue, out maxFilesPerPr) || maxFilesPerPr <= 0)
                    {
                        errorMessage = "Please provide a positive integer for --max-files-per-pr.";
                        return false;
                    }

                    break;
                case "--base-branch":
                    if (!TryReadValue(args, ref index, out baseBranch))
                    {
                        errorMessage = "Missing value for --base-branch.";
                        return false;
                    }

                    break;
                case "--branch-prefix":
                    if (!TryReadValue(args, ref index, out branchPrefix))
                    {
                        errorMessage = "Missing value for --branch-prefix.";
                        return false;
                    }

                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--repo":
                    if (!TryReadValue(args, ref index, out repoOverride))
                    {
                        errorMessage = "Missing value for --repo.";
                        return false;
                    }

                    break;
                default:
                    errorMessage = $"Unknown argument '{argument}'.";
                    return false;
            }
        }

        if (reportOnly == createPrs)
        {
            errorMessage = "Specify exactly one of --report or --create-prs.";
            return false;
        }

        if (format == OutputFormat.Json && createPrs)
        {
            errorMessage = "--format json is supported only with --report.";
            return false;
        }

        options = new GapCliOptions
        {
            RepoPath = repoPath,
            ReportOnly = reportOnly,
            CreatePrs = createPrs,
            Format = format,
            Model = model,
            LanguageFilter = languageFilter,
            MaxFilesPerPr = maxFilesPerPr,
            BaseBranch = baseBranch,
            BranchPrefix = branchPrefix,
            DryRun = dryRun,
            RepoOverride = repoOverride
        };

        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Length)
        {
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}

internal enum OutputFormat
{
    Text,
    Json
}
