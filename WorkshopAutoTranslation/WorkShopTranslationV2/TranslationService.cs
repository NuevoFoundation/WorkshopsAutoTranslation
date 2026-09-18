using Azure;
using Azure.AI.Inference;

namespace WorkShopTranslationV2;

internal sealed class TranslationService
{
    private readonly ChatCompletionsClient _client;

    public TranslationService(ChatCompletionsClient client)
    {
        _client = client;
    }

    public static TranslationService CreateFromEnvironment()
    {
        // NOTE: GitHub Models (models.inference.ai.azure.com) was retired.
        // We now use an Azure AI Foundry resource, which exposes the same
        // Azure.AI.Inference-compatible chat completions API.
        var endpointValue = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")
            ?? throw new InvalidOperationException("The AZURE_AI_ENDPOINT environment variable is not set.");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_AI_API_KEY")
            ?? throw new InvalidOperationException("The AZURE_AI_API_KEY environment variable is not set.");

        var endpoint = new Uri(NormalizeEndpoint(endpointValue));
        var credential = new AzureKeyCredential(apiKey);
        var client = new ChatCompletionsClient(endpoint, credential, new ChatCompletionsClientOptions());
        return new TranslationService(client);
    }

    // Azure AI Foundry's chat-completions-compatible route lives at "<resource-endpoint>/models".
    // Accept either form so a plain resource endpoint (e.g. from the Azure Portal) works as-is.
    internal static string NormalizeEndpoint(string endpointValue)
    {
        string trimmed = endpointValue.TrimEnd('/');
        return trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/models";
    }

    public TranslationResult TranslateFileIfMissing(string sourceFilePath, string model, LanguageDefinition language, string? repoPath = null)
    {
        if (!File.Exists(sourceFilePath))
        {
            return TranslationResult.Failure(sourceFilePath, null, $"Source file is missing, skipping: {sourceFilePath}");
        }

        string targetFilePath = WorkshopPaths.GetTranslatedFilePath(sourceFilePath, language.FolderName, repoPath);
        if (File.Exists(targetFilePath))
        {
            return TranslationResult.Skipped(sourceFilePath, targetFilePath, $"A translated file already exists at: {targetFilePath}, skipping translation.");
        }

        string translatedContent = TranslateToLanguageWithRetry(model, language.LanguageKey, sourceFilePath);

        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        File.WriteAllText(targetFilePath, translatedContent);

        return TranslationResult.Success(sourceFilePath, targetFilePath, $"Success! Translated file path: {targetFilePath}");
    }

    public string TranslateToLanguageWithRetry(string model, string language, string filePath, int maxAttempts = 3)
    {
        Exception? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return TranslateToLanguage(model, language, filePath);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Console.WriteLine($"Translation attempt {attempt} failed for {filePath}: {ex.Message}. Retrying in {delay.TotalSeconds:0} seconds.");
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        throw new InvalidOperationException($"Translation failed for {filePath} after {maxAttempts} attempts.", lastError);
    }

    public string TranslateToLanguage(string model, string language, string filePath)
    {
        string prompt =
            $"Translate the following file to {language}. " +
            "Ensure that the translation does not alter any Hugo-specific syntax, front matter, or HTML tags. " +
            "Only translate the plain text content. Do not translate code blocks, URLs, or any metadata. " +
            "Maintain the structure and formatting of the original file.";

        string fileContent = ReadFromFile(filePath);

        var requestOptions = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(prompt),
                new ChatRequestUserMessage(fileContent)
            },
            Model = model,
            Temperature = 1.0f,
            MaxTokens = 1500,
            NucleusSamplingFactor = 1.0f
        };

        Response<ChatCompletions> response = _client.Complete(requestOptions);
        return response.Value.Choices[0].Message.Content;
    }

    public static string ReadFromFile(string filePath) => File.ReadAllText(filePath);
}

internal sealed class TranslationResult
{
    public bool Created { get; init; }
    public bool SkippedExisting { get; init; }
    public bool Failed => !Created && !SkippedExisting;
    public string SourcePath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TranslationResult Success(string sourcePath, string targetPath, string message) =>
        new()
        {
            Created = true,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Message = message
        };

    public static TranslationResult Skipped(string sourcePath, string? targetPath, string message) =>
        new()
        {
            SkippedExisting = true,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Message = message
        };

    public static TranslationResult Failure(string sourcePath, string? targetPath, string message) =>
        new()
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Message = message
        };
}
