using Azure;
using Azure.AI.Inference;

namespace WorkShopTranslationV2
{
    internal class Program
    {
        static void Main(string[] args)
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
				return;
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
				return;
            }

			var client = new ChatCompletionsClient(
                endpoint,
                credential,
                new ChatCompletionsClientOptions());

			// Check if the input path is a markdown file
            if (File.Exists(path) && Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase))
			{
				TranslateFile(path, model, language, supportedLanguages[language.ToLower()], client);
			}
			// Otherwise, check if the input path is a directory
			else if (Directory.Exists(path))
			{
				// Get a list of all markdown files in the folder and its subdirectories
				string[] files = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories);

				foreach (string filePath in files)
				{
					TranslateFile(filePath, model, language, supportedLanguages[language.ToLower()], client);
				}
			}
			else
			{
				Console.WriteLine("The path you entered is invalid. Please enter a valid file or directory path containing markdown files.");
				return;
			}
        }

        public static void TranslateFile(string filePath, string model, string language, string newFolder, ChatCompletionsClient client)
        {
			Console.WriteLine($"Translating file: {filePath}");
			
			try
			{
				var translatedFilePath = filePath.Replace("english", newFolder);
				
				// Check if the translated file already exists
				if (File.Exists(translatedFilePath))
				{
					Console.WriteLine($"A translated file already exists at: {translatedFilePath}, skipping translation.");
					return;
				}

				// Translate the contents of the file to the target language
				var response = TranslateToLanguage(model, language, client, filePath);

				// Create the directory if it doesn't exist
				Directory.CreateDirectory(Path.GetDirectoryName(translatedFilePath)!);

				// Write the translated content to the file
				File.WriteAllText(translatedFilePath, response);

				Console.WriteLine($"Success! Translated file path: {translatedFilePath}");
				Console.WriteLine();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An unexpected error occurred: {ex.Message}");
			}
        }

        public static string TranslateToLanguage(string model, string language, ChatCompletionsClient client, string filePath)
        {
			string prompt = $"Translate the following file to {language}." +
				$" Ensure that the translation does not alter any Hugo-specific syntax, front matter, or HTML tags." +
				$" Only translate the plain text content. Do not translate code blocks, URLs, or any metadata." +
				$" Maintain the structure and formatting of the original file.";

			string fileContent = ReadFromFile(filePath);

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                        {
                            new ChatRequestSystemMessage(prompt),
                            new ChatRequestUserMessage(fileContent),
                        },
                Model = model,
                Temperature = 1.0f,
                MaxTokens = 1000,
                NucleusSamplingFactor = 1.0f
            };

            Response<ChatCompletions> response = client.Complete(requestOptions);
            return response.Value.Choices[0].Message.Content;
        }

        public static string ReadFromFile(string filePath)
        {
            // Read from a file
            string fileContents = File.ReadAllText(filePath);
            return fileContents;
        }
    }
}
