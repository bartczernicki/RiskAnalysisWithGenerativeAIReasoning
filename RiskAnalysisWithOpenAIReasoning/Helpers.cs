using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiskAnalysisWithOpenAIReasoning
{
    public static class Helpers
    {
        public static string GetChatCompletionResultWithoutThinkingTokens(string chatCompletionResult)
        {
            // Extract the Thinking Section
            var chatCompletionResultStart = chatCompletionResult.IndexOf("</think>") + "</think>".Length;
            var chatCompletionResultEnd = chatCompletionResult.Length;
            var chatCompletionWithoutThinkingTokens = chatCompletionResult[chatCompletionResultStart..chatCompletionResultEnd];

            return chatCompletionWithoutThinkingTokens;
        }
    }
}
