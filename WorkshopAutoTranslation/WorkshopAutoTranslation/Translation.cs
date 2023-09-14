using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI.Managers;
using OpenAI;
class Translation
{
    public string ReadFromFile(string filePath)
    {
        // Read from a file
        string sourceStr = File.ReadAllText(filePath);
        return sourceStr;
    }



    public static async Task Main(string[] args)
    {
        string folderPath = "C:\\hackathon2023\\workshops\\content\\english\\python-turtle\\";
        string apiKey = "API-KEY-AZUREAI"; // Replace with your OpenAI API key
        Translation t = new Translation();



        // Get a list of all files in the folder
        string[] files = Directory.GetFiles(folderPath);



        var azureClient = new AzureAIApiClient(apiKey);



        foreach (string filePath in files)
        {
            string sourceStr = t.ReadFromFile(filePath);



            // Translate the content of each file to French
            var response = await azureClient.SendPrompt(sourceStr, "French");



            // Handle the translation response here
            // Create a translated file on the user's desktop
            string fileName = Path.GetFileName(filePath);
            string translatedFilePath = Path.Combine("C:\\hackathon2023\\workshops\\content\\francais\\python-turtle\\", fileName + "_translated.md" );


            File.WriteAllText(translatedFilePath, response);


            Console.WriteLine($"Translation for file: {filePath}");
            Console.WriteLine(response);
        }
    }
}