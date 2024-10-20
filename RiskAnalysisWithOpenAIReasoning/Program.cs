using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using Microsoft.ML.Tokenizers;
using ConsoleTables;
using Markdig;

namespace RiskAnalysisWithOpenAIReasoning
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
            var o1AzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["o1APIKey"];
            var o1AzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["o1Endpoint"];
            var o1AzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["o1ModelDeploymentName"];
            var gpt4oAzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["gpt4oAPIKey"];
            var gpt4oAzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["gpt4oEndpoint"];
            var gpt4oAzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["gpt4oModelDeploymentName"];


            var o1OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output", o1AzureModelDeploymentName!);

            var retryPolicy = new ClientRetryPolicy(maxRetries: 3);
            AzureOpenAIClientOptions azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_01_Preview);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;
            azureOpenAIClientOptions.NetworkTimeout = TimeSpan.FromMinutes(20); // Large Timeout

            Uri azureOpenAIResourceUri = new(o1AzureOpenAIEndpoint!);
            var azureApiCredential = new System.ClientModel.ApiKeyCredential(o1AzureOpenAIAPIKey!);

            var o1Client = new AzureOpenAIClient(azureOpenAIResourceUri, azureApiCredential, azureOpenAIClientOptions);
            var gpt4oClient = new AzureOpenAIClient(new Uri(gpt4oAzureOpenAIEndpoint!), new System.ClientModel.ApiKeyCredential(gpt4oAzureOpenAIAPIKey!), azureOpenAIClientOptions);

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "o1Testing",
                // TopLogProbabilityCount = true ? 5 : 1 // Azure OpenAI maximum is 5               
            };

            Console.WriteLine($"SUBMITTING RISK ANALYSIS...");
            Console.WriteLine(string.Empty);
            var totalDocumentPromptCount = 0;
            var totalDocumentReasoningTokenCount = 0;
            var totalDcoumentTotalTokenCount = 0;

            // This can be Parallelized
            // Note: this is using one of the risk factors as they are both the same to loop over 
            for (int i = 0; i != Data.GetMicrosoft2023RiskFactors().Count; i++)
            {
                // 1) Perform Risk Analysis over SEC Documents using o1
                // Get C# Dictionary key at index
                var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

                var promptInstructions = Prompts.GetFullPromptForSECDocumentAnalysis(i);

                var promptInstructionsChatMessageSection = new UserChatMessage(promptInstructions);

                var chatMessagesRiskAnalysis = new List<ChatMessage>();
                chatMessagesRiskAnalysis.Add(promptInstructionsChatMessageSection);

                Console.WriteLine($"Section: {riskFactorSection}");
                Tokenizer sectionTokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
                var sectionPromptTokenCount = sectionTokenizer.CountTokens(promptInstructions);
                Console.WriteLine($"Prompt Token Count: {sectionPromptTokenCount}");

                // Get new chat o1Client for o1 model deployment (used for reasoning)
                var chatClient = o1Client.GetChatClient(o1AzureModelDeploymentName);
                // Get new chat o1Client for gpt-4o model deployment (used for markdown formatting)
                var chatClientGPT4o = gpt4oClient.GetChatClient(gpt4oAzureModelDeploymentName);

                var sectionStartTime = DateTime.UtcNow;
                var sectionResponse = await chatClient.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptions);
                var sectionOutputTokenDetails = sectionResponse.Value.Usage.OutputTokenDetails;
                var sectionTotalTokenCount = sectionResponse.Value.Usage.TotalTokenCount;

                var responseo1RiskAnalysis = sectionResponse.Value.Content.FirstOrDefault()!.Text;
                var sectionEndTime = DateTime.UtcNow;
                var sectionDurationSections = (sectionEndTime - sectionStartTime).TotalSeconds;

                Console.WriteLine($"Duration: {sectionDurationSections} seconds");
                Console.WriteLine($"Reasoning o1 Tokens: {sectionOutputTokenDetails.ReasoningTokenCount}");
                Console.WriteLine($"Total o1 Model Tokens: {sectionTotalTokenCount}");

                // Update Totals
                totalDocumentPromptCount += sectionPromptTokenCount;
                totalDocumentReasoningTokenCount += sectionOutputTokenDetails.ReasoningTokenCount;
                totalDcoumentTotalTokenCount += sectionTotalTokenCount;


                // 2) Fix the Markdown table formatting using GPT-4o
                Console.WriteLine("Fixing Markdown Formatting...");
                var chatMessagesGPT4o = new List<ChatMessage>();
                chatMessagesGPT4o.Add($"Fix the following {riskFactorSection} table formatting for proper Markdown: {responseo1RiskAnalysis}");
                var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, completionOptions);
                var llmResponseGPT4o = sectionResponse.Value.Content.FirstOrDefault()!.Text;

                // Write out the fixed markdown file
                Console.WriteLine($"Creating MD File...{riskFactorSection}.md");
                var markdownRiskFactorSectionPath = Path.Combine(o1OutputDirectory, $"{riskFactorSection}.md");
                File.WriteAllText(markdownRiskFactorSectionPath, llmResponseGPT4o);
                Console.WriteLine(string.Empty);

            } // End of loop over SEC sections

            // Write out the totals
            Console.WriteLine($"Total Prompt Token Count: {totalDocumentPromptCount}");
            Console.WriteLine($"Total Reasoning Token Count: {totalDocumentReasoningTokenCount}");
            Console.WriteLine($"Total Model Output Token Count: {totalDcoumentTotalTokenCount}");

            // 3) Analyze the Markdown tables on only extract the relevant risk changes
            Console.WriteLine("Consolidate...Risks into one Markdown file");

            var markdownFiles = Directory.GetFiles(o1OutputDirectory, "*.md");
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

            // Get new chat o1Client for o1 model deployment (used for reasoning)
            var chatClientRiskAnalysis = o1Client.GetChatClient(o1AzureModelDeploymentName);

            var response = await chatClientRiskAnalysis.CompleteChatAsync(chatMessageRiskConsolidation, completionOptions);
            var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
            var totalTokenCount = response.Value.Usage.TotalTokenCount;

            var responseRiskConsolidation = response.Value.Content.FirstOrDefault()!.Text;
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Duration: {durationSections} seconds");
            Console.WriteLine($"Reasoning o1 Tokens: {outputTokenDetails.ReasoningTokenCount}");
            Console.WriteLine($"Total o1 Model Tokens: {totalTokenCount}");

            Console.WriteLine($"Creating MD File...ConsolidatedRiskAnalysis.md");
            var markdownConsolidatedRiskAnalysisPath = Path.Combine(o1OutputDirectory, "ConsolidatedRiskAnalysis.md");
            File.WriteAllText(markdownConsolidatedRiskAnalysisPath, responseRiskConsolidation);
            Console.WriteLine(string.Empty);
        }
    }
}
