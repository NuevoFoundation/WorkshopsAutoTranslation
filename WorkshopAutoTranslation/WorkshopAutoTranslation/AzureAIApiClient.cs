using Azure;
using Azure.AI.OpenAI;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System;
using static OpenAI.ObjectModels.SharedModels.IOpenAiModels;
using OpenAI.ObjectModels.ResponseModels;
using MathNet.Numerics.LinearAlgebra;

public class AzureAIApiClient
{
    private readonly string _apiKey;

    public AzureAIApiClient(string apiKey)
    {
        _apiKey = apiKey;
    }
    public async Task<string> SendPrompt(string prompt, string language)
    {
        OpenAIClient client = new OpenAIClient(new Uri("https://openairesearch.openai.azure.com/"),
            new AzureKeyCredential(this._apiKey));
        string message = @$"Translate the below to {language}: {prompt}; don't translate the header key title; don't translate the image html";

        // ### If streaming is not selected
        Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(
            "GPT-35-turbo-prop",
            new ChatCompletionsOptions()
            {
                Messages =
                {
            new ChatMessage(ChatRole.System, message),
                },
                Temperature = (float)0.7,
                MaxTokens = 800,
                NucleusSamplingFactor = (float)0.95,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
            });

        ChatCompletions completions = responseWithoutStream.Value;
        var result = completions.Choices[0].Message.Content.ToString();
        return result;
    }
}

