# WorkShopTranslationV2 Tool

This program is designed to translate a set of markdown files to a target language using [Github Models](https://github.com/marketplace/models). It reads the contents of each file in a specified directory, sends the content to an LLM, and saves the translated content to new files in a different directory.

## Features

1. **Reading from Folder**: The script reads the content of each file in a given directory.
2. **Translation using Github Models**: The script uses the Azure AI Inference SDK to translate the content by feeding it to a supported Github Models LLM.
3. **Saving Translated Content**: Once translated, the script saves the translated content to new files in a different directory.

## How to Use

1. **Prerequisites**
   - Create a Github account if you do not already have one and generate a [personal access token](https://github.com/settings/tokens)
   - Download and install [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
   - Clone/download this repo locally or run it in [Github Codespaces](https://github.com/features/codespaces)

2. **Set environment variables**: 
Run the following command in terminal

   If you're using bash:

   `export GITHUB_TOKEN="<your-github-token-goes-here>"`

   If you're in powershell:

   `$Env:GITHUB_TOKEN="<your-github-token-goes-here>"`

   If you're using Windows command prompt:

   `set GITHUB_TOKEN=<your-github-token-goes-here>`

3. **Run the project**: Navigate to the `WorkShopTranslationV2` directory and run the following command:

   `dotnet run <inputFolderPath> <targetLanguage> <model (optional)>`

   ex. `dotnet run C:\Documents\workshops\content\english\csharp-basics french gpt-4o`

## Note

This program currently only supports a small set of languages. In order to add more, modify the `supportedLanguages` dictionary in `Program.cs`.

**Disclaimer**: Ensure that your personal access token is kept confidential and not hardcoded into scripts that are publically accessible. Consider using environment variables or secure key vaults to store your access tokens. Also limit the usage to 30 days so that you can generate a new token to be in compliance.
