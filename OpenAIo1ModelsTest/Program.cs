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
            {Data.GetMicrosoft2023RiskFactors()["Strategic and Competitive Risks"]}
            </Risk Factors in Microsoft 2023 10K>

            <Risk Factors in Microsoft 2024 10k>
            {Data.GetMicrosoft2024RiskFactors()["Strategic and Competitive Risks"]}
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

            Console.WriteLine($"SUBMITTING RISK ANALYSIS...");
            Console.WriteLine(string.Empty);

            // This can be Parallelized
            for (int i = 0; i != Data.GetMicrosoft2023RiskFactors().Count; i++)
            {
                // Get C# Dictionary key at index
                var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

                var promptInstructionsTemplate = $"""
                    <Context>
                    Below are Risk Factor {riskFactorSection} section of Microsoft's 10K filings for 2023 and 2024.
                    Please compare the risk factors from 2023 to 2024 and provide an analysis based
                    on the following instructions:
                    </Context>

                    <Risk Factors in Microsoft 2023 10K>
                    {Data.GetMicrosoft2023RiskFactors()[riskFactorSection]}
                    </Risk Factors in Microsoft 2023 10K>

                    <Risk Factors in Microsoft 2024 10k>
                    {Data.GetMicrosoft2024RiskFactors()[riskFactorSection]}
                    </End of Risk Factors in Microsoft 2024 10K>

                    <Instructions>
                    {riskInstructions}
                    </Instructions>
                    """;

                var promptInstructionsChatMessage = new UserChatMessage(promptInstructions);

                var chatMessagesRiskAnalysis = new List<ChatMessage>();
                chatMessagesRiskAnalysis.Add(promptInstructionsChatMessage);

                Console.WriteLine($"Section: {riskFactorSection}");
                Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
                var tokenCount = tokenizer.CountTokens(promptInstructionsTemplate);
                Console.WriteLine($"Prompt Token Count: {tokenCount}");

                var startTime = DateTime.UtcNow;
                var response = await chatClient.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptions);
                var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
                var totalTokenCount = response.Value.Usage.TotalTokenCount;

                var responseo1RiskAnalysis = response.Value.Content.FirstOrDefault()!.Text;
                var endTime = DateTime.UtcNow;
                var durationSections = (endTime - startTime).TotalSeconds;

                Console.WriteLine($"Duration: {durationSections} seconds");
                Console.WriteLine($"Reasoning Tokens: {outputTokenDetails.ReasoningTokenCount}");
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
