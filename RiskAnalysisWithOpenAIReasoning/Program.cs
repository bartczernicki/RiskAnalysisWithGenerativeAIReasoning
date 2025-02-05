using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.Tokenizers;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

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
            var reasoningAzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["reasoningEndpoint"];
            var reasoningAzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["reasoningModelDeploymentName"];
            var reasoningAzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["reasoningAPIKey"];
            var gpt4oAzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["gpt4oEndpoint"];
            var gpt4oAzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["gpt4oModelDeploymentName"];
            var gpt4oAzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["gpt4oAPIKey"];
            // Retrieve the DeepSeek Configuration Section (secrets.json)
            var deepSeekEndpoint = configuration.GetSection("DeepSeek")["DeepSeekAPIEndpoint"];
            var deepSeekDeploymentName = configuration.GetSection("DeepSeek")["DeepSeekModelName"];
            var deepSeekAPIKey = configuration.GetSection("DeepSeek")["DeepSeekAPIKey"];

            var useLocalReasoning = false;

            // Azure OpenAI Clients
            var gpt4oClient = Helpers.GetAzureOpenAIClient(gpt4oAzureModelDeploymentName!, gpt4oAzureOpenAIEndpoint!, gpt4oAzureOpenAIAPIKey!);
            var reasoningClient = Helpers.GetAzureOpenAIClient(reasoningAzureModelDeploymentName!, reasoningAzureOpenAIEndpoint!, reasoningAzureOpenAIAPIKey!);
            // DeepSeek Local Client
            var localReasoningClient = Helpers.GetOpenAIClient(deepSeekDeploymentName!, deepSeekEndpoint!, deepSeekAPIKey!);

            // Determine the reasoning model to use
            var reasoningModelDeploymentName = string.Empty;
            if (useLocalReasoning)
            {
                reasoningModelDeploymentName = deepSeekDeploymentName;
            }
            else
            {
                reasoningModelDeploymentName = reasoningAzureModelDeploymentName;
            }

            // The output directory for the reasoning model markdown analysis files
            var reasoningOutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output", reasoningModelDeploymentName!);

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "GPT4o",
                MaxOutputTokenCount = 16000, // max output for GPT-4o           
            };

            var completionOptionsReasoning = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "Azure_Reasoning",
                //MaxOutputTokenCount = 32000, // Increase output for o1 or o3-mini
            };

            if (useLocalReasoning || ((reasoningModelDeploymentName == "o1") || reasoningModelDeploymentName == "o3-mini"))
            {
                completionOptionsReasoning.MaxOutputTokenCount = 32000;
            }

            // 1 - RISK ANALYSIS ON RISK FACTOR SECTIONS 
            Console.WriteLine($"STEP 1-3 - RISK ANALYSIS ON RISK FACTOR SECTIONS...");
            Console.WriteLine(string.Empty);

            var totalDocumentPromptCount = 0;
            var totalDocumentReasoningTokenCount = 0;
            var totalDocumentTotalTokenCount = 0;

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
                Thread.Sleep(200);
                // 1) Perform Risk Analysis over SEC Documents using a Reasoning Model 
                Console.WriteLine($"Starting to Process Section: {riskFactorSection.Key}");

                // Retrieve the Risk Factor Section
                //var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(i);

                // Create a Chat Message with Prompt Instructions for the Risk Factor Section
                var promptInstructions = Prompts.GetFullPromptForSECDocumentAnalysis(riskFactorSection.Key);
                var promptSystemMessage = new SystemChatMessage("Formatting re-enabled");
                var promptInstructionsChatMessageSection = new UserChatMessage(promptInstructions);
                var chatMessagesRiskAnalysis = new List<ChatMessage>();

                if (reasoningAzureModelDeploymentName == "o1" || reasoningModelDeploymentName == "o3-mini")
                {
                    chatMessagesRiskAnalysis.Add(promptSystemMessage);
                }
                chatMessagesRiskAnalysis.Add(promptInstructionsChatMessageSection);

                // Calculate the Prompt Tokens for Section
                Tokenizer sectionTokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
                var sectionPromptTokenCount = sectionTokenizer.CountTokens(promptInstructions);
                // Console.WriteLine($"Section: {riskFactorSection.Key} - Prompt Token Count: {sectionPromptTokenCount}");

                // Get new chat reasoningClient for gpt-4o model deployment (used for markdown formatting)
                var chatClientGPT4o = gpt4oClient.GetChatClient(gpt4oAzureModelDeploymentName);

                var sectionStartTime = DateTime.UtcNow;

                // DeepSeek Reasoning
                var chatClientReasoning = (useLocalReasoning) ?
                    localReasoningClient.GetChatClient(deepSeekDeploymentName!) : reasoningClient.GetChatClient(reasoningAzureModelDeploymentName);

                var sectionResponse = await chatClientReasoning.CompleteChatAsync(chatMessagesRiskAnalysis, completionOptionsReasoning);

                var sectionOutputTokenDetails = sectionResponse.Value.Usage.OutputTokenDetails;
                var sectionTotalTokenCount = sectionResponse.Value.Usage.TotalTokenCount;

                var responseReasoningRiskAnalysis = Helpers.GetChatCompletionResultWithoutThinkingTokens(
                    sectionResponse.Value.Content.FirstOrDefault()!.Text);

                var sectionEndTime = DateTime.UtcNow;
                var sectionDurationSections = (sectionEndTime - sectionStartTime).TotalSeconds;

                // Update Totals (lock for multiple threads)
                lock (lockObject)
                {
                    totalDocumentPromptCount += sectionPromptTokenCount;
                    // Check for null
                    if (sectionOutputTokenDetails != null)
                    {
                        totalDocumentReasoningTokenCount += sectionOutputTokenDetails.ReasoningTokenCount;
                    }
                    totalDocumentTotalTokenCount += sectionTotalTokenCount;
                }

                // 2) Fix the Markdown table formatting using GPT-4o
                // Console.WriteLine("Fixing Markdown Formatting...");
                var chatMessagesGPT4o = new List<ChatMessage>();
                chatMessagesGPT4o.Add($"Fix the following {riskFactorSection.Key} table formatting for proper Markdown: {responseReasoningRiskAnalysis}. Only output the fixed markdown table structure (without enclosing it in a code block).");
                var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, completionOptions);
                var llmResponseGPT4o = responseGPT4o.Value.Content.FirstOrDefault()!.Text;

                // Write out the fixed markdown file
                // Console.WriteLine($"Creating MD File...{riskFactorSection.Key}.MD");
                var markdownRiskFactorSectionPath = Path.Combine(reasoningOutputDirectory, $"{reasoningModelDeploymentName!.ToUpper()}-{riskFactorSection.Key}.MD");
                // Create directory if it doesn't exit
                if (!Directory.Exists(reasoningOutputDirectory))
                {
                    Directory.CreateDirectory(reasoningOutputDirectory);
                }
                File.WriteAllText(markdownRiskFactorSectionPath, llmResponseGPT4o);

                // Write out all of the Sections Processes
                lock (lockObject)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Finished Processing Section: {riskFactorSection.Key}");
                    Console.WriteLine($"Duration: {sectionDurationSections} seconds");
                    Console.WriteLine($"Prompt Token Count (section): {sectionPromptTokenCount}");
                    Console.WriteLine($"Reasoning Model Tokens (section): {sectionOutputTokenDetails?.ReasoningTokenCount}");
                    Console.WriteLine($"Total Reasoning Model Tokens (section): {sectionTotalTokenCount}");
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
            Console.WriteLine($"Total Model Output Token Count: {totalDocumentTotalTokenCount}");
            Console.WriteLine(String.Empty);
            Console.WriteLine(String.Empty);

            // 3) Analyze the Markdown tables on only extract the relevant risk changes
            Console.WriteLine($"STEP 4 - CONSOLIDATE INTO A SINGLE RISK ANALYSIS...");
            Console.WriteLine(string.Empty);

            // Read each file and extract the table
            var markdownFiles = Directory.GetFiles(reasoningOutputDirectory, "*.MD");
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

            // Get new chat reasoningClient for reasoning model deployment (used for reasoning)
            var chatClientRiskAnalysis = (useLocalReasoning) ? localReasoningClient.GetChatClient(deepSeekDeploymentName!) :
                reasoningClient.GetChatClient(reasoningAzureModelDeploymentName);

            var response = await chatClientRiskAnalysis.CompleteChatAsync(chatMessageRiskConsolidation, completionOptionsReasoning);

            var outputTokenDetails = response.Value.Usage.OutputTokenDetails;
            var totalTokenCount = response.Value.Usage.TotalTokenCount;

            var responseRiskConsolidation = Helpers.GetChatCompletionResultWithoutThinkingTokens(response.Value.Content.FirstOrDefault()!.Text);
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Duration: {durationSections} seconds");
            Console.WriteLine($"Reasoning Reasoning Tokens: {outputTokenDetails?.ReasoningTokenCount}");
            Console.WriteLine($"Total Reasoning Model Tokens: {totalTokenCount}");

            var markdownConsolidatedRiskAnalysisPath = Path.Combine(reasoningOutputDirectory, $"{reasoningModelDeploymentName!.ToUpper()}-CONSOLIDATEDRISKANALYSIS.MD");
            File.WriteAllText(markdownConsolidatedRiskAnalysisPath, responseRiskConsolidation);
            Console.WriteLine($"Created MD File...CONSOLIDATEDRISKANALYSIS.MD");
            Console.WriteLine(string.Empty);

            Console.WriteLine($"STEP 5 - APPLY A RISK MITIGATION FRAMEWORK TO CREATE A RISK MITIGATION STRATEGY...");

            var consolidatedRiskAnalysisPath = Path.Combine(reasoningOutputDirectory, $"{reasoningModelDeploymentName!.ToUpper()}-CONSOLIDATEDRISKANALYSIS.MD");
            var consolidatedRiskAnalysis = File.ReadAllText(consolidatedRiskAnalysisPath);

            // Get Prompts
            var promptApplyRiskMethodology = Prompts.GetFullPromptToApplyRiskMitigation(consolidatedRiskAnalysis);
            var promptInstructionsApplyRiskMethodologyChatMessage = new UserChatMessage(promptApplyRiskMethodology);
            var chatMessagesApplyRiskMethodology = new List<ChatMessage>();
            chatMessagesApplyRiskMethodology.Add(promptInstructionsApplyRiskMethodologyChatMessage);
            // Execute the Reasoning Model for creating a Risk Mitigation Strategy
            var chatClientApplyRiskMethodology = (useLocalReasoning) ? localReasoningClient.GetChatClient(deepSeekDeploymentName!) :
                reasoningClient.GetChatClient(reasoningAzureModelDeploymentName);
            var responseApplyRiskMethodology = await chatClientApplyRiskMethodology.CompleteChatAsync(chatMessagesApplyRiskMethodology, completionOptionsReasoning);
            var responseApplyRiskMethodologyText = responseApplyRiskMethodology.Value.Content.FirstOrDefault()!.Text;

            var markdownApplyRiskMethodologyPath= Path.Combine(reasoningOutputDirectory, $"{reasoningModelDeploymentName!.ToUpper()}-RISKMITIGATIONSTRATEGY.MD");
            File.WriteAllText(markdownApplyRiskMethodologyPath, responseApplyRiskMethodologyText);

            Console.WriteLine($"Created MD File...RISKMITIGATIONSTRATEGY.MD");
            Console.WriteLine(string.Empty);
        }
    }
}
