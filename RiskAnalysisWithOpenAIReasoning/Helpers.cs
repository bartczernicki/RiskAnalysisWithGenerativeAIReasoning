using Azure;
using Azure.AI.OpenAI;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ClientModel;

namespace RiskAnalysisWithOpenAIReasoning
{
    public static class Helpers
    {
        public static string GetChatCompletionResultWithoutThinkingTokens(string chatCompletionResult)
        {
            // Extract the Thinking Section
            var chatCompletionResultStart = chatCompletionResult.IndexOf("</think>") + "</think>".Length;
            var chatCompletionResultEnd = chatCompletionResult.Length;
            var chatCompletionWithoutThinkingTokens = chatCompletionResult[chatCompletionResultStart..chatCompletionResultEnd];

            return chatCompletionWithoutThinkingTokens;
        }

        public static AzureOpenAIClient GetAzureOpenAIClient(string modelDeploymentName, string azureOpenAIEndpoint, string azureOpenAIEndpointAPIKey)
        {
            var retryPolicy = new ClientRetryPolicy(maxRetries: 5);
            var azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;
            azureOpenAIClientOptions.NetworkTimeout = TimeSpan.FromMinutes(20); // Large Timeout

            var isUnsuportedReasoningModel = modelDeploymentName.Equals("o1") || modelDeploymentName.Contains("o3-mini");
            if (isUnsuportedReasoningModel)
            {
                var reasoningHttpClient = new HttpClient(new ReplaceUriForAzureReasoning());
                reasoningHttpClient.Timeout = TimeSpan.FromMinutes(20);
                azureOpenAIClientOptions.Transport = new HttpClientPipelineTransport(reasoningHttpClient);
            }

            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(azureOpenAIEndpoint),
                new ApiKeyCredential(azureOpenAIEndpointAPIKey),
                azureOpenAIClientOptions
            );

            return azureOpenAIClient;
        }
    }
}
