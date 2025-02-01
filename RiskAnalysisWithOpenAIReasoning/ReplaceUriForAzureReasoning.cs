using Azure;
using Azure.AI.OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Dynamic;
using OpenAI.Chat;
using System.Web;

namespace RiskAnalysisWithOpenAIReasoning
{
    internal class ReplaceUriForAzureReasoning() : DelegatingHandler(new SocketsHttpHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // retrieve JSON Message Payload
            var jsonRequestString = await request.Content.ReadAsStringAsync();

            // Convert jsonRequestString to Object to Manipulate
            dynamic jsonRequestObject = JsonSerializer.Deserialize<ExpandoObject>(jsonRequestString);

            // Check if using an o1 GA or o3 GA model

            // 1) Change the Request URI to support the new 2024-12-01-preview API Version
            var apiVersionKey = "api-version";
            var uriBuilder = new UriBuilder(request.RequestUri!);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[apiVersionKey] = "2024-12-01-preview";
            uriBuilder.Query = query.ToString();
            request.RequestUri = uriBuilder.Uri;

            // 2) Change the Request Body to support the new o1 and o3 model parameters

            // Extract the value of Max_Tokens
            var jsonRequestObjectDictionary = (IDictionary<String, Object>) jsonRequestObject!;
            jsonRequestObjectDictionary.TryGetValue("max_tokens", out var maxTokensObject);

            if (maxTokensObject != null)
            {
                // Convert JsonElement number to int
                JsonElement maxTokensElement = (JsonElement)maxTokensObject!;
                var maxTokens = maxTokensElement.GetInt32();

                // Remove Max_Tokens
                var isMaxTokensRemoved = ((IDictionary<String, Object>)jsonRequestObject!).Remove("max_tokens");
                // Add Max Completion Tokens
                jsonRequestObject!.max_completion_tokens = maxTokens;
            }

            // Add new elements to support o1 and o3 models
            jsonRequestObject!.reasoning_effort = "medium";
            jsonRequestObject!.stream = false;

            var jsonUpdatedRequestString = JsonSerializer.Serialize(jsonRequestObject);

            request.Content = new StringContent(jsonUpdatedRequestString, Encoding.UTF8, "application/json");

            var response = await base.SendAsync(request, cancellationToken);

            return response;
        }
    }
}
