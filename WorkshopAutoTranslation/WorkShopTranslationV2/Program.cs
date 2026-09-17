using System.Text.Json;

namespace WorkShopTranslationV2;

internal static class Program
{
    private const string DefaultModel = "gpt-4o";

    public static int Main(string[] args)
    {
        try
        {
            if (GapCliOptions.IsGapScanCommand(args))
            {
                return RunGapWorkflow(args);
            }

            return RunLegacyWorkflow(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int RunLegacyWorkflow(string[] args)
    {
        if (args.Length < 2)
        {
            PrintLegacyUsage();
            return 1;
        }

        string path = args[0];
        string languageInput = args[1];
        string model = args.Length > 2 ? args[2] : DefaultModel;

        if (!LanguageCatalog.TryResolveLanguage(languageInput, out var language) || language.IsEnglish)
        {
            Console.WriteLine("The language you entered is not supported. Please enter one of the following languages:");
            foreach (var supportedLanguage in LanguageCatalog.GetSupportedLanguageKeys())
            {
                Console.WriteLine(supportedLanguage);
            }

            return 1;
        }

        var translationService = TranslationService.CreateFromEnvironment();
        string? repoPath = WorkshopPaths.TryResolveRepositoryRoot(path);

        if (File.Exists(path) && Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            WriteLegacyResult(translationService.TranslateFileIfMissing(path, model, language, repoPath));
            return 0;
        }

        if (Directory.Exists(path))
        {
            foreach (string filePath in Directory.GetFiles(path, "*.md", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            {
                WriteLegacyResult(translationService.TranslateFileIfMissing(filePath, model, language, repoPath));
            }

            return 0;
        }

        Console.WriteLine("The path you entered is invalid. Please enter a valid file or directory path containing markdown files.");
        return 1;
    }

    private static int RunGapWorkflow(string[] args)
    {
        if (!GapCliOptions.TryParse(args, out var options, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            PrintGapUsage();
            return 1;
        }

        var scanner = new GapScanner();

        if (options.ReportOnly)
        {
            var report = scanner.Scan(options.RepoPath, options.LanguageFilter);
            WriteFormattedOutput(report, options.Format);
            return report.Errors.Count == 0 ? 0 : 1;
        }

        var translationService = TranslationService.CreateFromEnvironment();
        var automation = new PrAutomation(scanner, translationService, new ProcessRunner());
        var result = automation.Execute(options);

        WriteAutomationSummary(result);
        return result.ExitCode;
    }

    private static void WriteLegacyResult(TranslationResult result)
    {
        Console.WriteLine(result.Message);
        if (!string.IsNullOrWhiteSpace(result.TargetPath))
        {
            Console.WriteLine($"Target: {result.TargetPath}");
        }

        Console.WriteLine();
    }

    private static void WriteFormattedOutput(object payload, OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return;
        }

        switch (payload)
        {
            case GapReport report:
                Console.WriteLine(GapReportFormatter.Format(report));
                break;
            case AutomationRunResult result:
                Console.WriteLine(AutomationRunResultFormatter.Format(result));
                break;
            default:
                Console.WriteLine(payload);
                break;
        }
    }

    private static void WriteAutomationSummary(AutomationRunResult result)
    {
        Console.WriteLine(AutomationRunResultFormatter.Format(result));
    }

    private static void PrintLegacyUsage()
    {
        Console.WriteLine("Please provide the path and language as command line arguments.");
        Console.WriteLine("Usage: dotnet run <path> <language> <model (optional)>");
    }

    private static void PrintGapUsage()
    {
        Console.WriteLine("Gap scanning usage:");
        Console.WriteLine("  dotnet run -- --scan-gaps <path-to-workshops-repo> --report [--format json] [--language <lang>]");
        Console.WriteLine("  dotnet run -- --scan-gaps <path-to-workshops-repo> --create-prs [--model gpt-4o] [--language <lang>] [--max-workshops-per-pr N] [--base-branch master] [--branch-prefix auto-translate/] [--repo owner/name] [--dry-run]");
    }
}
