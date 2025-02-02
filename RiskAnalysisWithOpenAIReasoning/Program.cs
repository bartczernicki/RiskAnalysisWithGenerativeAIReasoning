using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using Microsoft.ML.Tokenizers;
using ConsoleTables;
using Markdig;
using Azure;
using System.ComponentModel.DataAnnotations;

namespace RiskAnalysisWithOpenAIReasoning
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // 0 - CONFIGURATION 
            Console.WriteLine($"STEP 0 - CONFIGURATION...");

            // Azure OpenAI Configuration from user secrets
            ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            IConfiguration configuration = configurationBuilder.AddUserSecrets<Program>().Build();

            // Retrieve the Azure OpenAI Configuration Section (secrets.json)
            var azureOpenAISection = configuration.GetSection("AzureOpenAI");
            var o1AzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["o1Endpoint"];
            var o1AzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["o1ModelDeploymentName"];
            var o1AzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["o1APIKey"];
            var gpt4oAzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["gpt4oEndpoint"];
            var gpt4oAzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["gpt4oModelDeploymentName"];
            var gpt4oAzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["gpt4oAPIKey"];

            // The output directory for the o1 model markdown analysis files
            var o1OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output", o1AzureModelDeploymentName!);

            var retryPolicy = new ClientRetryPolicy(maxRetries: 5);
            var azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21);
            var azureOpenAIClientOptionsReasoning = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21);

            azureOpenAIClientOptions.RetryPolicy = retryPolicy;
            azureOpenAIClientOptions.NetworkTimeout = TimeSpan.FromMinutes(30); // Large Timeout

            azureOpenAIClientOptionsReasoning.RetryPolicy = retryPolicy;
            azureOpenAIClientOptionsReasoning.NetworkTimeout = TimeSpan.FromMinutes(20); // Large Timeout
            var reasoningHttpClient = new HttpClient(new ReplaceUriForAzureReasoning());
            reasoningHttpClient.Timeout = TimeSpan.FromMinutes(30);
            azureOpenAIClientOptionsReasoning.Transport = new HttpClientPipelineTransport(reasoningHttpClient);

            Uri azureOpenAIResourceUri = new(o1AzureOpenAIEndpoint!);
            var azureApiCredentialReasoning = new AzureKeyCredential(o1AzureOpenAIAPIKey!);

            var reasoningClient = new AzureOpenAIClient(azureOpenAIResourceUri, azureApiCredentialReasoning, azureOpenAIClientOptionsReasoning);
            var gpt4oClient = new AzureOpenAIClient(new Uri(gpt4oAzureOpenAIEndpoint!), new System.ClientModel.ApiKeyCredential(gpt4oAzureOpenAIAPIKey!), azureOpenAIClientOptions);

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "GPT4o",
                MaxOutputTokenCount = 16000,           
            };

            var completionOptionsReasoning = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "o1_o3_Reasoning",
                MaxOutputTokenCount = 32000,
            };

            // 1 - RISK ANALYSIS ON RISK FACTOR SECTIONS 
            Console.WriteLine($"STEP 1-3 - RISK ANALYSIS ON RISK FACTOR SECTIONS...");
            Console.WriteLine(string.Empty);

            var totalDocumentPromptCount = 0;
            var totalDocumentReasoningTokenCount = 0;
            var totalDcoumentTotalTokenCount = 0;

            // Lock object to sync multiple threads
            // Note: This is just a simple hack to get Console to show messages in order
            var lockObject = new object();

            // This can be Parallelized
            // Note: this is using one of the risk factors as they are both the same to loop over
            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = 6 };
            var cancellationToken = new CancellationTokenSource();
            await Parallel.ForEachAsync(Data.GetMicrosoft2023RiskFactors(), options, async (riskFactorSection, cancellationToken) =>
            //foreach (var riskFactorSection in Data.GetMicrosoft2023RiskFactors())
            {
                // 1) Perform Risk Analysis over SEC Documents using o1
                Console.WriteLine($"Starting to Process Section: {riskFactorSection.Key}");

                // Retrieve the Risk Factor Section
                //var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

                // Create a Chat Message with Prompt Instructions for the Risk Factor Section
                var promptInstructions = Prompts.GetFullPromptForSECDocumentAnalysis(riskFactorSection.Key);
                var promptSystemMessage = new SystemChatMessage("Formatting re-enabled");
                var promptInstructionsChatMessageSection = new UserChatMessage(promptInstructions);
                var chatMessagesRiskAnalysis = new List<ChatMessage>();
                chatMessagesRiskAnalysis.Add(promptSystemMessage);
                chatMessagesRiskAnalysis.Add(promptInstructionsChatMessageSection);

                // Calculate the Prompt Tokens for Section
                Tokenizer sectionTokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
                var sectionPromptTokenCount = sectionTokenizer.CountTokens(promptInstructions);
                // Console.WriteLine($"Section: {riskFactorSection.Key} - Prompt Token Count: {sectionPromptTokenCount}");

                // Get new chat reasoningClient for o1 model deployment (used for reasoning)
                var chatClientReasoning = reasoningClient.GetChatClient(o1AzureModelDeploymentName);
                // Get new chat reasoningClient for gpt-4o model deployment (used for markdown formatting)
                var chatClientGPT4o = gpt4oClient.GetChatClient(gpt4oAzureModelDeploymentName);

                var sectionStartTime = DateTime.UtcNow;
                var sectionResponse = await chatClientReasoning.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptionsReasoning);
                var sectionOutputTokenDetails = sectionResponse.Value.Usage.OutputTokenDetails;
                var sectionTotalTokenCount = sectionResponse.Value.Usage.TotalTokenCount;

                var responseo1RiskAnalysis = sectionResponse.Value.Content.FirstOrDefault()!.Text;
                var sectionEndTime = DateTime.UtcNow;
                var sectionDurationSections = (sectionEndTime - sectionStartTime).TotalSeconds;

                // Update Totals (lock for multiple threads)
                lock(lockObject)
                {
                    totalDocumentPromptCount += sectionPromptTokenCount;
                    totalDocumentReasoningTokenCount += sectionOutputTokenDetails.ReasoningTokenCount;
                    totalDcoumentTotalTokenCount += sectionTotalTokenCount;
                }

                // 2) Fix the Markdown table formatting using GPT-4o
                // Console.WriteLine("Fixing Markdown Formatting...");
                var chatMessagesGPT4o = new List<ChatMessage>();
                chatMessagesGPT4o.Add($"Fix the following {riskFactorSection.Key} table formatting for proper Markdown: {responseo1RiskAnalysis}");
                var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, completionOptions);
                var llmResponseGPT4o = sectionResponse.Value.Content.FirstOrDefault()!.Text;

                // Write out the fixed markdown file
                // Console.WriteLine($"Creating MD File...{riskFactorSection.Key}.MD");
                var markdownRiskFactorSectionPath = Path.Combine(o1OutputDirectory, $"{o1AzureModelDeploymentName!.ToUpper()}-{riskFactorSection.Key}.MD");
                // Create directory if it doesn't exit
                if (!Directory.Exists(o1OutputDirectory))
                {
                    Directory.CreateDirectory(o1OutputDirectory);
                }
                File.WriteAllText(markdownRiskFactorSectionPath, llmResponseGPT4o);

                // Write out all of the Sections Processes
                lock(lockObject)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Finished Processing Section: {riskFactorSection.Key}");
                    Console.WriteLine($"Duration: {sectionDurationSections} seconds");
                    Console.WriteLine($"Prompt Token Count (section): {sectionPromptTokenCount}");
                    Console.WriteLine($"Reasoning o1 Tokens (section): {sectionOutputTokenDetails.ReasoningTokenCount}");
                    Console.WriteLine($"Total o1 Model Tokens (section): {sectionTotalTokenCount}");
                    Console.WriteLine($"Created MD File...{riskFactorSection.Key}.MD");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                Console.WriteLine(string.Empty);
            }); // End of parallel loop over SEC sections

            // Write out the totals
            Console.WriteLine(String.Empty);
            Console.WriteLine($"Overall Job Totals");
            Console.WriteLine($"Total Prompt Token Count: {totalDocumentPromptCount}");
            Console.WriteLine($"Total Reasoning Token Count: {totalDocumentReasoningTokenCount}");
            Console.WriteLine($"Total Model Output Token Count: {totalDcoumentTotalTokenCount}");
            Console.WriteLine(String.Empty);
            Console.WriteLine(String.Empty);

            // 3) Analyze the Markdown tables on only extract the relevant risk changes
            Console.WriteLine($"STEP 4 - CONSOLIDATE INTO A SINGLE RISK ANALYSIS...");
            Console.WriteLine(string.Empty);

            // Read each file and extract the table
            var markdownFiles = Directory.GetFiles(o1OutputDirectory, "*.MD");
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
            Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            var tokenCount = tokenizer.CountTokens(promptConsolidate);
            Console.WriteLine($"Prompt Token Count: {tokenCount}");

            var startTime = DateTime.UtcNow;

            // Get new chat reasoningClient for o1 model deployment (used for reasoning)
            var chatClientRiskAnalysis = reasoningClient.GetChatClient(o1AzureModelDeploymentName);

            var response = await chatClientRiskAnalysis.CompleteChatAsync(chatMessageRiskConsolidation, completionOptionsReasoning);
            var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
            var totalTokenCount = response.Value.Usage.TotalTokenCount;

            var responseRiskConsolidation = response.Value.Content.FirstOrDefault()!.Text;
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Duration: {durationSections} seconds");
            Console.WriteLine($"Reasoning o1 Tokens: {outputTokenDetails.ReasoningTokenCount}");
            Console.WriteLine($"Total o1 Model Tokens: {totalTokenCount}");

            var markdownConsolidatedRiskAnalysisPath = Path.Combine(o1OutputDirectory, $"{o1AzureModelDeploymentName!.ToUpper()}-CONSOLIDATEDRISKANALYSIS.MD");
            File.WriteAllText(markdownConsolidatedRiskAnalysisPath, responseRiskConsolidation);
            Console.WriteLine($"Created MD File...CONSOLIDATEDRISKANALYSIS.MD");
            Console.WriteLine(string.Empty);

            Console.WriteLine($"STEP 5 - APPLY A RISK MITIGATION FRAMEWORK TO CREATE A RISK MITIGATION STRATEGY...");
            Console.WriteLine($"Created MD File...RISKMITIGATIONSTRATEGY.MD");
            Console.WriteLine(string.Empty);

            var consolidatedRiskAnalysisPath = Path.Combine(o1OutputDirectory, $"{o1AzureModelDeploymentName!.ToUpper()}-CONSOLIDATEDRISKANALYSIS.MD");
            var consolidatedRiskAnalysis = File.ReadAllText(consolidatedRiskAnalysisPath);

            // Get Prompts
            var promptApplyRiskMethodology = Prompts.GetFullPromptToApplyRiskMitigation(consolidatedRiskAnalysis);
            var promptInstructionsApplyRiskMethodologyChatMessage = new UserChatMessage(promptApplyRiskMethodology);
            var chatMessagesApplyRiskMethodology = new List<ChatMessage>();
            chatMessagesApplyRiskMethodology.Add(promptInstructionsApplyRiskMethodologyChatMessage);
            // Execute the o1 Reasoning for creating a Risk Mitigation Strategy
            var chatClientApplyRiskMethodology = reasoningClient.GetChatClient(o1AzureModelDeploymentName);
            var responseApplyRiskMethodology = await chatClientApplyRiskMethodology.CompleteChatAsync(chatMessagesApplyRiskMethodology, completionOptionsReasoning);
            var responseApplyRiskMethodologyText = responseApplyRiskMethodology.Value.Content.FirstOrDefault()!.Text;

            var markdownApplyRiskMethodologyPath= Path.Combine(o1OutputDirectory, $"{o1AzureModelDeploymentName!.ToUpper()}-RISKMITIGATIONSTRATEGY.MD");
            File.WriteAllText(markdownApplyRiskMethodologyPath, responseApplyRiskMethodologyText);
        }
    }
}
