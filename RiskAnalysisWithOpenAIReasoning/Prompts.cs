using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiskAnalysisWithOpenAIReasoning
{
    internal class Prompts
    {
        // Convert to get-only properties
        public static string RiskAnalysisInstructions { get; } = @"""
            Compare the risk factors from the 2023 and 2024 10-K SEC documents and highlight their evolution.
            Instructions:
            Create a Markdown table (without enclosing it in a code block) that includes the following columns:

            Row Number: Sequential numbering for each risk factor comparison.
            Title: A concise title summarizing the risk factor.
            2023 Risk Factor Summary: A brief summary of the risk factor as described in the 2023 report.
            2024 Risk Factor Summary: A brief summary of the risk factor as described in the 2024 report.
            Change: A description of how the risk factor has evolved from 2023 to 2024. Indicate whether it is new, modified, or removed, and provide details on any changes.

            Match and align similar risk factors from both years, even if the wording differs, to ensure an accurate comparison.
            """;

        public static string GetFullPromptForSECDocumentAnalysis(string riskFactorSection)
        {
            //var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(secDocumentSectionIndex);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""                    
            <Instructions>
            {Prompts.RiskAnalysisInstructions}
            </Instructions>

            <Context>
            Below are Risk Factor section of Microsoft's 10K filings for 2023 and 2024.
            </Context>

            <Risk Factors in Microsoft 2023 10K>
            {Data.GetMicrosoft2023RiskFactors()[riskFactorSection]}
            </Risk Factors in Microsoft 2023 10K>
            
            <Risk Factors in Microsoft 2024 10k>
            {Data.GetMicrosoft2024RiskFactors()[riskFactorSection]}
            </End of Risk Factors in Microsoft 2024 10K>
            """;

            return fullPromptTemplate;
        }

        public static string GetFullPromptToConsolidateImportantRisks(List<string> markdownTables)
        {
            // Concatenate all the markdown tables into a single string
            var markdownTablesString = string.Join("\n\n", markdownTables);

            var promptTemplate = $"""
            <Instructions>
            You have extracted risk factor changes from multiple sections of 10-K filings. Each risk factor includes:
            Title: A brief title for the risk factor.
            2023 Risk Factor Summary: A brief summary from the 2023 10-K report.
            2024 Risk Factor Summary: A brief summary from the 2024 10-K report.
            Change: A description of the differences between 2023 and 2024.

            Using the list of “Risk Analysis Tables” you extracted, please perform a consolidated risk analysis by selecting only the significant and impactful risk factor changes. 

            For each significant risk factor, include:
            Potential Impact: A detailed evaluation of how the change affects the company's risk profile, operations, financial performance, or strategic direction.
            Key Insights: A detailed explanation of why the change is important and how it differs from the previous year.
            Relevance: Focus on changes that introduce new risks, significantly alter existing risks, or remove previously critical risks.
            Important Instructions:

            Do not include the entire tables; only include the selected significant risk factor changes.
            Generate a Markdown table (without using a code block) that is fully complete with no missing fields.

            The table must have exactly 5 columns with the following exact headings:
            Number: Sequential number starting from 1.
            Risk Factor: Title of the Risk Factor.
            Change: Detailed description of the change between 2023 and 2024.
            Potential Impact: A detailed explanation of the potential risk and company impact of the change.
            Key Insights: A detailed analysis of the significance of the change.
            </Instructions>

            <Risk Analysis Tables>
            {markdownTablesString}
            </Risk Analysis Tables>
            """;

            return promptTemplate;
        }

        public static string GetFullPromptToApplyRiskMitigation(string consolidatedRiskAnalysis)
        {
            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Context>
            Below is a consolidated risk analysis of Microsoft's 10K filings for 2023 and 2024. 
            Only the important and sufficiently impactful risk factor changes have been included in the analysis. 
            The columns in the Consolidated Risk Analysis Markdown Table are as follows: 
            Number, Risk Factor, Potential Impact, Key Insights. 
            </Context>
            
            <Consolidated Risk Analysis Markdown Table>
            {consolidatedRiskAnalysis}
            </Consolidated Risk Analysis Markdown Table>
                     
            <Instructions>
            Apply the COSO ERM framework to recommend risk mitigation strategies for the consolidated risk analysis. 
            </Instructions>
            """;

            return fullPromptTemplate;
        }

    }
}
