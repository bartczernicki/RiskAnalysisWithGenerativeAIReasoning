using Azure.AI.OpenAI;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace RiskAnalysisWithOpenAIReasoning
{
    public static class Helpers
    {
        public static string GetChatCompletionResultWithoutThinkingTokens(string chatCompletionResult)
        {
            // Extract the Thinking Section
            var chatCompletionResultStart = (chatCompletionResult.IndexOf("</think>") == -1) ? 
                0 : ((chatCompletionResult.IndexOf("</think>") + "</think>".Length));
            var chatCompletionResultEnd = chatCompletionResult.Length;
            var chatCompletionWithoutThinkingTokens = chatCompletionResult[chatCompletionResultStart..chatCompletionResultEnd];

            return chatCompletionWithoutThinkingTokens;
        }

        public static AzureOpenAIClient GetAzureOpenAIClient(string modelDeploymentName, string azureOpenAIEndpoint, string azureOpenAIEndpointAPIKey)
        {
            var retryPolicy = new ClientRetryPolicy(maxRetries: 5);
            var azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2025_01_01_Preview);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;
            azureOpenAIClientOptions.NetworkTimeout = TimeSpan.FromMinutes(20); // Large Timeout

            // var isUnsuportedReasoningModel = modelDeploymentName.Equals("o1") || modelDeploymentName.Contains("o3-mini");
            //if (isUnsuportedReasoningModel)
            //{
            //    var reasoningHttpClient = new HttpClient(new ReplaceUriForAzureReasoning());
            //    reasoningHttpClient.Timeout = TimeSpan.FromMinutes(20);
            //    azureOpenAIClientOptions.Transport = new HttpClientPipelineTransport(reasoningHttpClient);
            //}

            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(azureOpenAIEndpoint),
                new ApiKeyCredential(azureOpenAIEndpointAPIKey),
                azureOpenAIClientOptions
            );

            return azureOpenAIClient;
        }

        public static OpenAIClient GetOpenAIClient(string modelDeploymentName, string openAIEndpoint, string openAIEndpointAPIKey)
        {
            var localDeepSeekUri = new Uri(openAIEndpoint!); // Local DeepSeek
            var localDeepSeekClientOptions = new OpenAIClientOptions { Endpoint = localDeepSeekUri };
            var retryPolicy = new ClientRetryPolicy(maxRetries: 5);
            localDeepSeekClientOptions.RetryPolicy = retryPolicy;
            localDeepSeekClientOptions.NetworkTimeout = TimeSpan.FromMinutes(20); // Large Timeout
            var apiCredential = new ApiKeyCredential(openAIEndpointAPIKey!); // No API Key needed for local DeepSeek 
            var localDeepSeekClient = new OpenAIClient(apiCredential, localDeepSeekClientOptions);

            return localDeepSeekClient;
        }
    }
}
