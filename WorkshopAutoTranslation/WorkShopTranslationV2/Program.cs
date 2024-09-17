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

            string filePath = args[0];
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

            ReadPrompts(filePath, model, language, supportedLanguages[language.ToLower()], client);
        }

        public static void ReadPrompts(string folderPath, string model, string language, string languageFolder, ChatCompletionsClient client)
        {
			// Get a list of all files in the folder
			string[] files = Directory.GetFiles(folderPath);

            foreach (string path in files)
            {
                Console.WriteLine($"Translating file: {path}");

				string sourceStr = ReadFromFile(path);

                try
                {
					// Translate the content of each file to the target language
					var response = TranslateToLanguage(model, language, client, path);

					// Handle the translation response here
					// Create a translated file in the target language folder
					string fileName = Path.GetFileName(path);
					string contentFolder = Directory.GetParent(Directory.GetParent(folderPath).FullName).FullName;
					string workshopFolder = Path.GetFileName(Path.GetDirectoryName(path));
					string translatedFilePath = Path.Combine(contentFolder, languageFolder, workshopFolder, fileName);

					// create the directory if it doesn't exist
					string directoryPath = Path.GetDirectoryName(translatedFilePath);
					if (directoryPath != null)
					{
						Directory.CreateDirectory(directoryPath);
					}
					else
					{
						throw new InvalidOperationException("The directory path is invalid.");
					}

					File.WriteAllText(translatedFilePath, response);

					Console.WriteLine($"Success! Translated file path: {translatedFilePath}");
                    Console.WriteLine();
				}
		        catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
				}
            }
        }

        public static string TranslateToLanguage(string model, string language, ChatCompletionsClient client, string filePath)
        {
            string prompt = ReadFromFile(filePath);

            string message = @$"Translate the below to {language}: {prompt}; don't translate the header key title; don't translate the image html";

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                        {
                            new ChatRequestSystemMessage(message),
                            new ChatRequestUserMessage(prompt),
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
            string sourceStr = File.ReadAllText(filePath);
            return sourceStr;
        }
    }
}
