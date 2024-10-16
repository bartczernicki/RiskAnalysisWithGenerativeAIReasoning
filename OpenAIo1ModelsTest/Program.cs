using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using Microsoft.ML.Tokenizers;
using ConsoleTables;
using Markdig;

namespace OpenAIo1ModelsTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Azure OpenAI Configuration from user secrets
            ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            IConfiguration configuration = configurationBuilder.AddUserSecrets<Program>().Build();

            // Retrieve the Azure OpenAI Configuration Section (secrets.json)
            var azureOpenAISection = configuration.GetSection("AzureOpenAI");
            var azureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["APIKey"];
            var azureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["Endpoint"];
            var azureModelDeploymentName = configuration.GetSection("AzureOpenAI")["ModelDeploymentName"];
            var azureOpenAIAPIKeyGPT4o = configuration.GetSection("AzureOpenAI")["APIKeyGPT4o"];
            var azureOpenAIEndpointGPT4o = configuration.GetSection("AzureOpenAI")["EndpointGPT4o"];
            var azureModelDeploymentNameGPT4o = configuration.GetSection("AzureOpenAI")["ModelDeploymentNameGPT4o"];



            var retryPolicy = new ClientRetryPolicy(maxRetries: 10);
            AzureOpenAIClientOptions azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_01_Preview);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;

            Uri azureOpenAIResourceUri = new(azureOpenAIEndpoint!);
            var azureApiCredential = new System.ClientModel.ApiKeyCredential(azureOpenAIAPIKey!);

            var client = new AzureOpenAIClient(azureOpenAIResourceUri, azureApiCredential, azureOpenAIClientOptions);
            var clientGPT4o = new AzureOpenAIClient(new Uri(azureOpenAIEndpointGPT4o!), new System.ClientModel.ApiKeyCredential(azureOpenAIAPIKeyGPT4o!), azureOpenAIClientOptions);
            var modelDeploymentName = azureModelDeploymentName;

            // Get new chat client for o1
            var chatClient = client.GetChatClient(modelDeploymentName);
            // Get new chat client for gpt-4o
            var chatClientGPT4o = clientGPT4o.GetChatClient(azureModelDeploymentNameGPT4o);

            //var systemPrompt = string.Empty;


            //var systemChatMessage = new SystemChatMessage(string.Empty);
            //var userChatMessage = new UserChatMessage(Prompts.FullPromptForSECDocumentAnalysis);

            //var chatMessages = new List<ChatMessage>();
            ////chatMessages.Add("List frameworks for Decision Making");
            //chatMessages.Add(string.Join(" ", new string[] { systemPrompt, Prompts.FullPromptForSECDocumentAnalysis }));

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "o1Testing",
                // TopLogProbabilityCount = true ? 5 : 1 // Azure OpenAI maximum is 5               
            };

            Console.WriteLine($"SUBMITTING RISK ANALYSIS...");
            Console.WriteLine(string.Empty);

            // This can be Parallelized
            for (int i = 0; i != Data.GetMicrosoft2023RiskFactors().Count; i++)
            {
                // Get C# Dictionary key at index
                var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

                var promptInstructions = Prompts.GetFullPromptForSECDocumentAnalysis(i);

                var promptInstructionsChatMessage = new UserChatMessage(promptInstructions);

                var chatMessagesRiskAnalysis = new List<ChatMessage>();
                chatMessagesRiskAnalysis.Add(promptInstructionsChatMessage);

                Console.WriteLine($"Section: {riskFactorSection}");
                Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
                var tokenCount = tokenizer.CountTokens(promptInstructions);
                Console.WriteLine($"Prompt Token Count: {tokenCount}");

                var startTime = DateTime.UtcNow;
                var response = await chatClient.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptions);
                var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
                var totalTokenCount = response.Value.Usage.TotalTokenCount;

                var responseo1RiskAnalysis = response.Value.Content.FirstOrDefault()!.Text;
                var endTime = DateTime.UtcNow;
                var durationSections = (endTime - startTime).TotalSeconds;

                Console.WriteLine($"Duration: {durationSections} seconds");
                Console.WriteLine($"Reasoning o1 Tokens: {outputTokenDetails.ReasoningTokenCount}");
                Console.WriteLine($"Total o1 Model Tokens: {totalTokenCount}");

                // Fix the Markdown table formatting
                Console.WriteLine("Fixing Markdown Formatting...");
                var chatMessagesGPT4o = new List<ChatMessage>();
                chatMessagesGPT4o.Add($"Fix the following {riskFactorSection} table formatting for proper Markdown: {responseo1RiskAnalysis}");
                var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, completionOptions);
                var llmResponseGPT4o = response.Value.Content.FirstOrDefault()!.Text;

                Console.WriteLine($"Creating MD File...{riskFactorSection}.md");
                File.WriteAllText($"{riskFactorSection}.md", llmResponseGPT4o);
                Console.WriteLine(string.Empty);
                Console.WriteLine(string.Empty);
            }
        }
    }
}
