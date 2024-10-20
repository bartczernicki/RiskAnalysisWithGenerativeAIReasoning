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
            Please compare the risk factors from 2023 to 2024 by creating a Markdown table that shows
            the changes in risk factors between the two years. The Markdown compliant table should include the
            following columns:
            1. Title, a brief summary Title for the Risk Factor
            2. 2023 Risk Factor Summary: A brief summary of the risk factor from the 2023 10-K report.
            3. 2024 Risk Factor Summary: A brief summary of the risk factor from the 2024 10-K report.
            4. Change, description of the change between 2023 and 2024. 
            Describe how the risk factor has evolved, specifying if it is new, modified, or removed. 
            
            Match and align similar risk factors from both years, even if the wording has changed, 
            to accurately reflect modifications. Ensure the table is properly formatted in Markdown and 
            includes sequential row numbers.
            """;

        public static string GetFullPromptForSECDocumentAnalysis(int secDocumentSectionIndex)
        {
            var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(secDocumentSectionIndex);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Context>
            Below are Risk Factor section of Microsoft's 10K filings for 2023 and 2024.
            Please compare the Risk Factors from 2023 to 2024 and provide an analysis based
            on the following instructions:
            </Context>
            
            
            <Risk Factors in Microsoft 2023 10K>
            {Data.GetMicrosoft2023RiskFactors()[riskFactorSection]}
            </Risk Factors in Microsoft 2023 10K>
            
            <Risk Factors in Microsoft 2024 10k>
            {Data.GetMicrosoft2024RiskFactors()[riskFactorSection]}
            </End of Risk Factors in Microsoft 2024 10K>
            
            <Instructions>
            {Prompts.RiskAnalysisInstructions}
            </Instructions>
            """;

            return fullPromptTemplate;
        }

        public static string GetFullPromptToConsolidateImportantRisks(List<string> markdownTables)
        {
            // Concatenate all the markdown tables into a single string
            var markdownTablesString = string.Join("\n\n", markdownTables);

            var promptTemplate = $"""
            <Context>
            You HAVE extracted risk factor changes from multiple sections of the 10K filings in the following format: 
            1. Title, a brief summary Title for the Risk Factor
            2. 2023 Risk Factor Summary: A brief summary of the risk factor from the 2023 10-K report.
            3. 2024 Risk Factor Summary: A brief summary of the risk factor from the 2024 10-K report.
            4. Change, description of the change between 2023 and 2024. 
            Describe how the risk factor has evolved, specifying if it is new, modified, or removed. 
            </Context>

            <Instructions>
            Below is a list of Markdown Tables you HAVE extracted from the 10-K filings. 
            Please perform a comprehensive risk analysis by consolidating only the significant and 
            impactful risk factor changes into a single Markdown table. 
            For each selected significant Risk Factor: 
            1. Assess the Potential Impact: Evaluate how the change may affect the company's risk profile, operations, financial performance, or strategic direction. 
            2. Highlight Key Insights: Provide a brief analysis explaining why this change is important and how it differs from the previous year. 
            3. Prioritize Relevance: Focus on changes that introduce new risks, significantly alter existing risks, or remove previously critical risks. 

            DO NOT include the entire tables, only the relevant and selected significant risk factor changes.
            </Instructions>

            <Markdown Tables>
            {markdownTablesString}
            </Markdown Tables>
            """;

            return promptTemplate;
        }
    }
}
