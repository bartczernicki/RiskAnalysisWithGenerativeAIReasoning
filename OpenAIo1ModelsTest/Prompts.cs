using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAIo1ModelsTest
{
    internal class Prompts
    {
        // Convert to get-only properties
        public static string RiskAnalysisInstructions { get; } = @"""
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

        public static string GetFullPromptForSECDocumentAnalysis(int secDocumentSectionIndex)
        {
            var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(secDocumentSectionIndex);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Context>
            Below are Risk Factor section of Microsoft's 10K filings for 2023 and 2024.
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
            {Prompts.RiskAnalysisInstructions}
            </Instructions>
            """;

            return fullPromptTemplate;
        }
    }
}
