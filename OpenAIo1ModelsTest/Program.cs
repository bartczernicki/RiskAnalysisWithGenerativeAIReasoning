using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using Microsoft.ML.Tokenizers;

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

            var retryPolicy = new ClientRetryPolicy(maxRetries: 10);
            AzureOpenAIClientOptions azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_01_Preview);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;

            Uri azureOpenAIResourceUri = new(azureOpenAIEndpoint!);
            var azureApiCredential = new System.ClientModel.ApiKeyCredential(azureOpenAIAPIKey!);

            var client = new AzureOpenAIClient(azureOpenAIResourceUri, azureApiCredential, azureOpenAIClientOptions);
            var modelDeploymentName = azureModelDeploymentName;

            // Get new chat client
            var chatClient = client.GetChatClient(modelDeploymentName);

            var systemPrompt = string.Empty;

            var riskInstructions = """
                Please compare the risk factors from 2023 to 2024 by creating a Markdown table that shows
                the changes in risk factors between the two years. The Markdown compliant table should include the
                following columns:
                1. title, a brief summary tile for the risk factor
                2. 2023, summary of this risk factor in 2023 10K, if present
                3. 2024, summary of this risk factor in 2024 10K, if present
                4. Change, description of the change between 2023 and 2024 (e.g., new risk
                factor, removed risk factor, modified risk factor)
                Note this requires match similar risk factors between 2023 and 2024, and identify
                the changes in the risk factors between the two years. Make sure the table has
                row numbers and is Markdown compliant.
                """;

            var promptInstructions = $"""
            <Context>
            Below are Risk Factor section of Microsoft's 10K filings for 2023 and 2024.
            Please compare the risk factors from 2023 to 2024 and provide an analysis based
            on the following instructions:
            </Context>


            <Risk Factors in Microsoft 2023 10K>
            {Data.Microsoft2023RiskFactors}
            </Risk Factors in Microsoft 2023 10K>

            <Risk Factors in Microsoft 2024 10k>
            {Data.Microsoft2024RiskFactors}
            </End of Risk Factors in Microsoft 2024 10K>

            <Instructions>
            {riskInstructions}
            </Instructions>
            """;

            var systemChatMessage = new SystemChatMessage(systemPrompt);
            var userChatMessage = new UserChatMessage(promptInstructions);

            var chatMessages = new List<ChatMessage>();
            //chatMessages.Add("List frameworks for Decision Making");
            chatMessages.Add(string.Join(" ", new string[] { systemPrompt, promptInstructions }));

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "o1Testing",
                // TopLogProbabilityCount = true ? 5 : 1 // Azure OpenAI maximum is 5               
            };

            Console.WriteLine($"Submitting Risk Analysis...");
            Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
            var tokenCount = tokenizer.CountTokens(promptInstructions);
            Console.WriteLine($"Token Count: {tokenCount}");

            var startTime = DateTime.UtcNow;
            var response = await chatClient.CompleteChatAsync(chatMessages, completionOptions);
            var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
            var totalTokenCount = response.Value.Usage.TotalTokenCount;

            var llmResponse = response.Value.Content.FirstOrDefault()!.Text;
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Duration: {durationSections} seconds");
            Console.WriteLine($"Reasoning Tokens: {outputTokenDetails.ReasoningTokenCount}");
            Console.WriteLine($"Total Tokens: {totalTokenCount}");
            Console.WriteLine(string.Empty);
            Console.WriteLine(llmResponse);
        }
    }
}
