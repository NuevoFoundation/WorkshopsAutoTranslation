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
            var credential = new AzureKeyCredential("Enter your Pat token");
            #endregion

            string model = args[0];
            string filePath = args[1];
            string language = args[2];

            var client = new ChatCompletionsClient(
                endpoint,
                credential,
                new ChatCompletionsClientOptions());

            ReadPrompts(filePath, model, language, client);

        }

        public static void ReadPrompts(string folderPath, string model, string language, ChatCompletionsClient client)
        {
            // Get a list of all files in the folder
            string[] files = Directory.GetFiles(folderPath);

            foreach (string path in files)
            {
                string sourceStr = ReadFromFile(path);

                // Translate the content of each file to French
                var response = TranslateToLanguage(model, language, client, path);

                // Handle the translation response here
                // Create a translated file on the user's desktop
                string fileName = Path.GetFileName(path);
                string translatedFilePath = Path.Combine(@$"C:\Demos\Nuevo Workshops\workshops\content\{GetLanguageEnum(language)}\python-turtle\", fileName);

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

                Console.WriteLine($"Translation for file: {path}");
                Console.WriteLine(response);
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
            System.Console.WriteLine(response.Value.Choices[0].Message.Content);
            return response.Value.Choices[0].Message.Content;
        }

        public static string ReadFromFile(string filePath)
        {
            // Read from a file
            string sourceStr = File.ReadAllText(filePath);
            return sourceStr;
        }

        public enum Language
        {
            francais,
            espanol,
            english,
            german,
            italiano
        }

        public static Language GetLanguageEnum(string language)
        {
            switch (language.ToLower())
            {
                case "french":
                    return Language.francais;
                case "spanish":
                    return Language.espanol;
                case "english":
                    return Language.english;
                case "german":
                    return Language.german;
                case "italian":
                    return Language.italiano;
                default:
                    throw new ArgumentException("Unsupported language");
            }
        }

    }
}
