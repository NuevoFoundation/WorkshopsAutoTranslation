using System;
using System.IO;
using System.Threading.Tasks;

public class Translation
{
    public static async Task Main(string[] args)
    {
        string accessToken = string.Empty;
        string folderPath = string.Empty;
        string modelLanguage = string.Empty;

        Console.WriteLine("Enter your Github personal access token (or type 'exit' to quit):");
        accessToken = Console.ReadLine();

        while (true)
        {
            try
            {
                Console.WriteLine("Enter the folder path (or type 'exit' to quit):");
                folderPath = Console.ReadLine();
                if (folderPath.ToLower() == "exit") break;

                Console.WriteLine("Enter the model language (or type 'exit' to quit):");
                modelLanguage = Console.ReadLine();
                if (modelLanguage.ToLower() == "exit") break;

                // Get a list of all files in the folder
                string absoluteFolderPath;
                if (Path.IsPathRooted(folderPath))
                {
                    absoluteFolderPath = folderPath;
                }
                else
                {
                    string executablePath = AppDomain.CurrentDomain.BaseDirectory;
                    absoluteFolderPath = Path.Combine(executablePath, folderPath);
                }

                string[] files = Directory.GetFiles(absoluteFolderPath);

                var azureClient = new AzureAIApiClient(accessToken);

                foreach (string filePath in files)
                {
                    if (Path.GetExtension(filePath) != ".md" || Path.GetFileName(filePath).Contains("translated"))
                    {
                        continue;
                    }

                    string sourceStr = ReadFromFile(filePath);
                    
                    // Show progress indication
                    Console.WriteLine($"Translating file: {filePath}...");

                    // Translate the content of each file to the specified language
                    var response = await azureClient.SendPrompt(sourceStr, modelLanguage);

                    // Handle the translation response here
                    // Create a translated file on the user's desktop
                    string fileName = Path.GetFileName(filePath);
                    string translatedFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(fileName) + $"_translated_{modelLanguage}.md");

                    // Overwrite existing file or create a new file if it doesn't already exist
                    File.WriteAllText(translatedFilePath, response);

                    Console.WriteLine($"Success! Translated file path: {translatedFilePath}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine("Do you want to continue? (yes to continue, any other key to quit):");
                string userResponse = Console.ReadLine();
                if (userResponse.ToLower() != "yes")
                {
                    break;
                }
            }
        }
    }

    private static string ReadFromFile(string filePath)
    {
        // Read from a file
        string sourceStr = File.ReadAllText(filePath);
        return sourceStr;
    }
}