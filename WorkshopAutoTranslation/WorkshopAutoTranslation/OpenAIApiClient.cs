
using OpenAI.Managers;
using OpenAI;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using static OpenAI.ObjectModels.SharedModels.IOpenAiModels;
using static System.Net.Mime.MediaTypeNames;

public class OpenAIApiClient
{
    private readonly string _apiKey;

    public OpenAIApiClient(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<string> SendPrompt(string prompt, string model)
    {
        string responseBody = string.Empty;
        var gpt3 = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = this._apiKey
        });
        var completionResult = await gpt3.CreateCompletion(new CompletionCreateRequest()
        {
            Prompt = prompt,
            Model = model,
            Temperature = 0.5F,
            MaxTokens = 100
        });

        if (completionResult.Successful)
        {
            foreach (var choice in completionResult.Choices)
            {
                responseBody += choice.Text + Environment.NewLine;
            }
        }
        else
        {
            if (completionResult.Error == null)
            {
                throw new Exception("Unknown Error");
            }
            Console.WriteLine($"{completionResult.Error.Code}: {completionResult.Error.Message}");
        }
        return responseBody;
    }
}
