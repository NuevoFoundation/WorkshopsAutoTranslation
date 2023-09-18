# README: Translation.cs Documentation

This script is designed to translate a set of markdown files from English to another language using the OpenAI API. The script reads the contents of each file in a specified directory, sends the content to the API for translation, and saves the translated content to a new file in a different directory.

## Features

1. **Reading from Files**: The script reads the content of each file in a given directory.
2. **Translation using OpenAI**: The script uses the OpenAI API to translate the content from English to French (in this case, but it supports any language supported by the OpenAI API).
3. **Saving Translated Content**: Once translated, the script saves the translated content to a new file in a different directory.

## Code Structure

1. **ReadFromFile Method**: This method takes a `filePath` as its parameter and reads the content from the specified file.
```csharp
public string ReadFromFile(string filePath)
```

2. **Main Method**: This is the entry point for the script. It does the following:
   - Sets the directory from which files will be read.
   - Initializes the OpenAI client.
   - Iterates over each file in the directory, reads its content, translates it, and saves the translated content to a new file.
```csharp
public static async Task Main(string[] args)
```

## How to Use

1. **Prerequisite**: Ensure that you have the OpenAI library installed and properly referenced in your project.
2. **Set API Key**: Replace the placeholder `API-KEY-AZUREAI` with your actual OpenAI API key.
```csharp
string apiKey = "API-KEY-AZUREAI"; // Replace with your OpenAI API key
```
3. **Modify Source and Destination Paths**: Update the `folderPath` and `translatedFilePath` to point to your source and destination directories, respectively.
4. **Run the Script**: Once you've made the necessary changes, run the script. For each file, the script will print a message indicating that the translation has been completed, along with the translated content.

## Note

This script is set up to translate content to French. If you wish to translate to another language, modify the target language in the `azureClient.SendPrompt(sourceStr, "French")` line.

**Disclaimer**: Ensure that your API key is kept confidential and not hardcoded into scripts that are publically accessible. Consider using environment variables or secure key vaults to store your API keys.
