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
            //for (int i = 0; i != Data.GetMicrosoft2023RiskFactors().Count; i++)
            //{
            //    // 1) Perform Risk Analysis over SEC Documents using o1
            //    // Get C# Dictionary key at index
            //    var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

            //    var promptInstructions = Prompts.GetFullPromptForSECDocumentAnalysis(i);

            //    var promptInstructionsChatMessageSection = new UserChatMessage(promptInstructions);

            //    var chatMessagesRiskAnalysis = new List<ChatMessage>();
            //    chatMessagesRiskAnalysis.Add(promptInstructionsChatMessageSection);

            //    Console.WriteLine($"Section: {riskFactorSection}");
            //    Tokenizer sectiontokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
            //    var sectionTokenCount = sectiontokenizer.CountTokens(promptInstructions);
            //    Console.WriteLine($"Prompt Token Count: {sectionTokenCount}");

            //    var sectionStartTime = DateTime.UtcNow;
            //    var sectionResponse = await chatClient.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptions);
            //    var sectionOutputTokenDetails = sectionResponse.Value.Usage.OutputTokenDetails;
            //    var sectionTotalTokenCount = sectionResponse.Value.Usage.TotalTokenCount;

            //    var responseo1RiskAnalysis = sectionResponse.Value.Content.FirstOrDefault()!.Text;
            //    var sectionEndTime = DateTime.UtcNow;
            //    var sectionDurationSections = (sectionEndTime - sectionStartTime).TotalSeconds;

            //    Console.WriteLine($"Duration: {sectionDurationSections} seconds");
            //    Console.WriteLine($"Reasoning o1 Tokens: {sectionOutputTokenDetails.ReasoningTokenCount}");
            //    Console.WriteLine($"Total o1 Model Tokens: {sectionTotalTokenCount}");

            //    // 2) Fix the Markdown table formatting using GPT-4o
            //    Console.WriteLine("Fixing Markdown Formatting...");
            //    var chatMessagesGPT4o = new List<ChatMessage>();
            //    chatMessagesGPT4o.Add($"Fix the following {riskFactorSection} table formatting for proper Markdown: {responseo1RiskAnalysis}");
            //    var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, completionOptions);
            //    var llmResponseGPT4o = sectionResponse.Value.Content.FirstOrDefault()!.Text;

            //    Console.WriteLine($"Creating MD File...{riskFactorSection}.md");
            //    File.WriteAllText($"{riskFactorSection}.md", llmResponseGPT4o);
            //    Console.WriteLine(string.Empty);

            //} // End of loop over SEC sections

            // 3) Analyze the Markdown tables on only extract the relevant risk changes
            Console.WriteLine("Consolidate...Risks into one Markdown file");

            var markdownFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.md");
            // read each file and extract the table
            var markdownTables = new List<string>();
            foreach (var file in markdownFiles)
            {
                var markdown = File.ReadAllText(file);
                markdownTables.Add(markdown);
            }

            var promptConsolidate = Prompts.GetFullPromptToConsolidateImportantRisks(markdownTables);

            var promptInstructionsChatMessage = new UserChatMessage(promptConsolidate);
            var chatMessageRiskConsolidation = new List<ChatMessage>();
            chatMessageRiskConsolidation.Add(promptInstructionsChatMessage);

            Console.WriteLine("Consolidating Markdown...");
            Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
            var tokenCount = tokenizer.CountTokens(promptConsolidate);
            Console.WriteLine($"Prompt Token Count: {tokenCount}");

            var startTime = DateTime.UtcNow;
            var response = await chatClient.CompleteChatAsync(chatMessageRiskConsolidation, completionOptions);
            var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
            var totalTokenCount = response.Value.Usage.TotalTokenCount;

            var responseRiskConsolidation = response.Value.Content.FirstOrDefault()!.Text;
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Duration: {durationSections} seconds");
            Console.WriteLine($"Reasoning o1 Tokens: {outputTokenDetails.ReasoningTokenCount}");
            Console.WriteLine($"Total o1 Model Tokens: {totalTokenCount}");

            Console.WriteLine($"Creating MD File...ConsolidatedRiskAnalysis.md");
            File.WriteAllText($"ConsolidatedRiskAnalysis.md", responseRiskConsolidation);
            Console.WriteLine(string.Empty);
        }
    }
}
