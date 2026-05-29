using System.Text.RegularExpressions;
using Azure;
using Azure.AI.Inference;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace WorkShopTranslationV2
{
    public class Program
    {
        // Regex matches an opening code fence like ```, ```markdown, ```md, ```yaml, ```yml (case-insensitive)
        private static readonly Regex OpeningFenceRegex = new(@"^```(markdown|md|yaml|yml)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClosingFenceRegex = new(@"^```\s*$", RegexOptions.Compiled);
        // Extracts YAML frontmatter from a Hugo markdown file (between leading and second `---`)
        private static readonly Regex FrontmatterRegex = new(@"\A---\s*\r?\n(.*?)\r?\n---\s*(\r?\n|$)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static int Main(string[] args)
        {
            #region Secrets
            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new InvalidOperationException("The GITHUB_TOKEN environment variable is not set.");
            var credential = new AzureKeyCredential(token);
            #endregion

            // Contains the supported languages and their corresponding folder names
            Dictionary<string, string> supportedLanguages = new()
            {
                { "french", "francais" },
                { "spanish", "espanol" },
                { "english", "english" },
                { "german", "german" },
                { "portuguese", "brazilian-portuguese" },
                { "kyrgyz", "kyrgyz" },
                { "simplified-chinese", "simplified-chinese" },
                { "traditional-chinese", "traditional-chinese" }
            };

            if (args.Length < 2)
            {
                Console.WriteLine("Please provide the path and language as command line arguments.\n" +
                    "Usage: dotnet run <path> <language> <model (optional)>");
                return 1;
            }

            string path = args[0];
            string language = args[1];
            string model = args.Length > 2 ? args[2] : "gpt-4o";

            if (!supportedLanguages.ContainsKey(language.ToLower()))
            {
                Console.WriteLine("The language you entered is not supported. Please enter a language from the following list");
                foreach (var item in supportedLanguages.Keys)
                {
                    Console.WriteLine(item);
                }
                return 1;
            }

            var client = new ChatCompletionsClient(
                endpoint,
                credential,
                new ChatCompletionsClientOptions());

            int failureCount = 0;

            // Check if the input path is a markdown file
            if (File.Exists(path) && Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                if (!TranslateFile(path, model, language, supportedLanguages[language.ToLower()], client))
                {
                    failureCount++;
                }
            }
            // Otherwise, check if the input path is a directory
            else if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories);
                foreach (string filePath in files)
                {
                    if (!TranslateFile(filePath, model, language, supportedLanguages[language.ToLower()], client))
                    {
                        failureCount++;
                    }
                }
            }
            else
            {
                Console.WriteLine("The path you entered is invalid. Please enter a valid file or directory path containing markdown files.");
                return 1;
            }

            // Exit non-zero if any file failed validation so CI surfaces the problem
            return failureCount == 0 ? 0 : 2;
        }

        public static bool TranslateFile(string filePath, string model, string language, string newFolder, ChatCompletionsClient client)
        {
            Console.WriteLine($"Translating file: {filePath}");

            try
            {
                var translatedFilePath = ReplaceEnglishSegment(filePath, newFolder);

                // Check if the translated file already exists
                if (File.Exists(translatedFilePath))
                {
                    Console.WriteLine($"A translated file already exists at: {translatedFilePath}, skipping translation.");
                    return true;
                }

                string sourceContent = File.ReadAllText(filePath);
                Console.WriteLine($"  Source size: {sourceContent.Length} chars");

                // Translate via LLM and post-process / validate
                string translated = TranslateToLanguage(model, language, client, sourceContent);

                // Create the directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(translatedFilePath)!);

                File.WriteAllText(translatedFilePath, translated);

                Console.WriteLine($"  Success! Translated file path: {translatedFilePath}");
                Console.WriteLine();
                return true;
            }
            catch (TranslationValidationException ex)
            {
                Console.WriteLine($"  ✗ Validation failed for {filePath} -> {language}: {ex.Message}");
                Console.WriteLine($"    Skipping write to avoid producing broken output.");
                Console.WriteLine();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Unexpected error translating {filePath} -> {language}: {ex.Message}");
                Console.WriteLine();
                return false;
            }
        }

        /// <summary>
        /// Replaces only the "english" path segment with the target language folder.
        /// Avoids the bug where Replace("english", ...) would corrupt filenames or
        /// path components that happen to contain the substring "english".
        /// </summary>
        public static string ReplaceEnglishSegment(string filePath, string newFolder)
        {
            char sep = Path.DirectorySeparatorChar;
            char alt = Path.AltDirectorySeparatorChar;
            var parts = filePath.Split(new[] { sep, alt });
            bool replaced = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("english", StringComparison.OrdinalIgnoreCase))
                {
                    parts[i] = newFolder;
                    replaced = true;
                }
            }
            if (!replaced)
            {
                // Fall back to legacy behavior with a warning so callers learn about it
                Console.WriteLine($"  ! Warning: no 'english' path segment found in {filePath}; falling back to substring replace.");
                return filePath.Replace("english", newFolder);
            }
            return string.Join(sep, parts);
        }

        public static string TranslateToLanguage(string model, string language, ChatCompletionsClient client, string fileContent)
        {
            string prompt = $@"You are a professional translator for Hugo markdown documentation. Translate the following file to {language}.

CRITICAL OUTPUT RULES (failure to follow these breaks the build):
- Output ONLY the translated file content. Do NOT wrap the response in code fences. Never start your response with ```markdown, ```yaml, ```md, ```yml, or any ``` line. Never end with ```.
- Do NOT add any preamble, commentary, or explanation. Return the raw translated file and nothing else.
- Preserve the YAML frontmatter (the block between the leading `---` lines) with EXACTLY the same set of keys as the source. Do not add new keys. Do not remove keys. Do not duplicate keys. Do not reorder keys.
- Translate the VALUES of `title`, `description`, and `summary` if those keys exist. Keep the values of all other frontmatter keys (date, weight, hidden, draft, icon, language, topics, difficulties, prereq, etc.) byte-for-byte identical to the source.
- Do NOT translate or modify: code blocks (anything inside ``` fences inside the body), URLs, file paths, Hugo shortcodes ({{% ... %}} and {{< ... >}}), HTML tags, or attribute values.
- Preserve all whitespace, blank lines, indentation, list markers, and markdown structure exactly.

Translate the body prose (paragraphs, headings, list items, image alt text) to natural, fluent {language}.";

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(prompt),
                    new ChatRequestUserMessage(fileContent),
                },
                Model = model,
                Temperature = 0.2f,
                MaxTokens = 8192,
                NucleusSamplingFactor = 1.0f
            };

            Response<ChatCompletions> response = client.Complete(requestOptions);
            var choice = response.Value.Choices[0];

            // Detect truncation. The Azure.AI.Inference SDK exposes finish reason as a struct
            // whose string form is "length" when the model hit the token limit.
            string finishReason = choice.FinishReason.ToString() ?? string.Empty;
            Console.WriteLine($"  LLM finish reason: {finishReason}");
            if (finishReason.Equals("length", StringComparison.OrdinalIgnoreCase) ||
                finishReason.Equals("token_limit", StringComparison.OrdinalIgnoreCase))
            {
                throw new TranslationValidationException(
                    $"LLM response was truncated (finish_reason={finishReason}). Increase MaxTokens or split the file.");
            }

            string raw = choice.Message.Content ?? string.Empty;
            Console.WriteLine($"  LLM response size: {raw.Length} chars");

            string cleaned = PostProcessTranslation(raw, out var warnings);
            foreach (var w in warnings)
            {
                Console.WriteLine($"  ! Post-process: {w}");
            }

            ValidateAgainstSource(cleaned, fileContent);
            return cleaned;
        }

        /// <summary>
        /// Strips a single layer of code-fence wrapping that some LLM responses add around
        /// the whole file, plus BOM and stray surrounding whitespace. Conservative: only
        /// strips when the FIRST non-empty line is an opening fence AND the LAST non-empty
        /// line is a closing fence. Never strips fences that appear in the middle of the
        /// content (those are legitimate code blocks in the body).
        /// </summary>
        public static string PostProcessTranslation(string response, out List<string> warnings)
        {
            warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new TranslationValidationException("LLM returned an empty response.");
            }

            // Strip UTF-8 BOM if present
            if (response.Length > 0 && response[0] == '\uFEFF')
            {
                response = response.Substring(1);
                warnings.Add("Stripped leading BOM");
            }

            response = response.Trim();

            // Detect & strip an outer code fence wrapping the whole response.
            // We normalize CRLF for matching, then split on \n.
            var normalized = response.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');

            int firstNonEmpty = -1;
            int lastNonEmpty = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i])) { firstNonEmpty = i; break; }
            }
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(lines[i])) { lastNonEmpty = i; break; }
            }

            if (firstNonEmpty >= 0 && lastNonEmpty > firstNonEmpty &&
                OpeningFenceRegex.IsMatch(lines[firstNonEmpty].Trim()) &&
                ClosingFenceRegex.IsMatch(lines[lastNonEmpty].Trim()))
            {
                // Defense-in-depth: only strip if the content between them does NOT itself
                // contain an unbalanced fence count that would indicate this is a legitimate
                // code block (very unlikely for a whole-file wrap, but worth checking).
                int innerFenceCount = 0;
                for (int i = firstNonEmpty + 1; i < lastNonEmpty; i++)
                {
                    if (ClosingFenceRegex.IsMatch(lines[i].Trim()) || OpeningFenceRegex.IsMatch(lines[i].Trim()))
                    {
                        innerFenceCount++;
                    }
                }
                if (innerFenceCount % 2 == 0)
                {
                    var inner = string.Join("\n", lines, firstNonEmpty + 1, lastNonEmpty - firstNonEmpty - 1);
                    warnings.Add($"Stripped outer code-fence wrapper ({lines[firstNonEmpty].Trim()} ... ```)");
                    response = inner.Trim();
                }
                else
                {
                    warnings.Add($"Detected possible fence wrap but inner fence count is odd; leaving as-is.");
                }
            }

            // Detect & strip a partial wrap where only the frontmatter is fenced:
            // ```yaml
            // ---
            // ...frontmatter...
            // ---
            // ```
            // ...body...
            //
            // After the outer-wrap strip above this case can still slip through.
            var partialFenceMatch = Regex.Match(response,
                @"\A```(yaml|yml|markdown|md)?\s*\r?\n(---\s*\r?\n.*?\r?\n---\s*)\r?\n```\s*\r?\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (partialFenceMatch.Success)
            {
                response = response.Substring(0, partialFenceMatch.Index)
                    + partialFenceMatch.Groups[2].Value + "\n"
                    + response.Substring(partialFenceMatch.Index + partialFenceMatch.Length);
                warnings.Add("Stripped fence wrapper around frontmatter only");
            }

            return response;
        }

        /// <summary>
        /// Validates the translated content against the source:
        /// - If the source has YAML frontmatter, the translation must too.
        /// - Translated frontmatter must parse as YAML (catches duplicate keys via YamlDotNet).
        /// - Translated frontmatter must contain exactly the same set of keys as the source.
        /// - Translation must not start with a code fence after post-processing.
        /// - Translation must have a non-empty body (frontmatter alone is not a valid translation).
        /// </summary>
        public static void ValidateAgainstSource(string translated, string source)
        {
            if (translated.StartsWith("```", StringComparison.Ordinal))
            {
                throw new TranslationValidationException(
                    "Translated content still starts with a code fence after post-processing.");
            }

            var sourceFm = FrontmatterRegex.Match(source);
            if (!sourceFm.Success)
            {
                return; // Source had no frontmatter; nothing more to validate.
            }

            var translatedFm = FrontmatterRegex.Match(translated);
            if (!translatedFm.Success)
            {
                throw new TranslationValidationException(
                    "Source has YAML frontmatter but translation does not (or it is malformed).");
            }

            // Body after frontmatter must be non-empty if source body is non-empty.
            string sourceBody = source.Substring(sourceFm.Index + sourceFm.Length);
            string translatedBody = translated.Substring(translatedFm.Index + translatedFm.Length);
            if (!string.IsNullOrWhiteSpace(sourceBody) && string.IsNullOrWhiteSpace(translatedBody))
            {
                throw new TranslationValidationException(
                    "Translation has empty body but source body is non-empty.");
            }

            // Parse both blocks with YamlDotNet. WithDuplicateKeyChecking() makes the
            // deserializer throw on duplicate keys, which is exactly the failure mode we
            // want to catch (the bug pattern from PR #606).
            var deserializer = new DeserializerBuilder()
                .WithDuplicateKeyChecking()
                .Build();
            Dictionary<string, object?> sourceKeys;
            Dictionary<string, object?> translatedKeys;
            try
            {
                sourceKeys = deserializer.Deserialize<Dictionary<string, object?>>(sourceFm.Groups[1].Value)
                             ?? new Dictionary<string, object?>();
            }
            catch (YamlException ex)
            {
                // Source is malformed; not the translator's fault, but worth surfacing.
                Console.WriteLine($"  ! Warning: source frontmatter is not valid YAML: {ex.Message}");
                return;
            }
            try
            {
                translatedKeys = deserializer.Deserialize<Dictionary<string, object?>>(translatedFm.Groups[1].Value)
                                 ?? new Dictionary<string, object?>();
            }
            catch (YamlException ex)
            {
                throw new TranslationValidationException(
                    $"Translated frontmatter is not valid YAML (likely duplicate or malformed key): {ex.Message}");
            }

            var sourceSet = new HashSet<string>(sourceKeys.Keys, StringComparer.Ordinal);
            var translatedSet = new HashSet<string>(translatedKeys.Keys, StringComparer.Ordinal);

            var missing = sourceSet.Except(translatedSet).ToList();
            var added = translatedSet.Except(sourceSet).ToList();
            if (missing.Count > 0 || added.Count > 0)
            {
                throw new TranslationValidationException(
                    $"Frontmatter key mismatch. Missing: [{string.Join(", ", missing)}]. " +
                    $"Added: [{string.Join(", ", added)}].");
            }
        }

        public static string ReadFromFile(string filePath)
        {
            return File.ReadAllText(filePath);
        }
    }

    /// <summary>
    /// Thrown when a translation fails post-processing/validation. Causes the file to be
    /// skipped (no broken output written) and the process to exit non-zero so CI notices.
    /// </summary>
    public class TranslationValidationException : Exception
    {
        public TranslationValidationException(string message) : base(message) { }
    }
}
