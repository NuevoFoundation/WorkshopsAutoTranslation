using Azure;
using Azure.AI.Inference;
public class AzureAIApiClient
{
    private readonly string _apiKey;
    private readonly string _model;
    private const string Endpoint = "https://models.inference.ai.azure.com";

    public AzureAIApiClient(
        string apiKey, 
        string model="gpt-4o")
    {
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<String> SendPrompt(string prompt, string language)
    {
        var endpoint = new Uri(Endpoint);
        var credential = new AzureKeyCredential(_apiKey);
        var model = _model;
        var message = $"Translate the below to {language}: {prompt}; don't translate the header key title; don't translate the image html";

        var client = new ChatCompletionsClient(
            endpoint,
            credential,
            new ChatCompletionsClientOptions());

        var requestOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage("You are a translator."),
                new ChatRequestUserMessage(message),
            },
            Model = model,
            Temperature = 1.0f,
            MaxTokens = 1000,
            NucleusSamplingFactor = 1.0f
        };

        Response<ChatCompletions> response = client.Complete(requestOptions);

        return response.Value.Choices[0].Message.Content;
    }
}