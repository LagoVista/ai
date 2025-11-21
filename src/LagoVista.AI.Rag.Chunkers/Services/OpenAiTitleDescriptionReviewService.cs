using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    public static class OpenAiTitleDescriptionReview
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static async Task<TitleDescriptionReviewResult> ReviewAsync(
            SummaryObjectKind kind,
            string symbolName,
            string title,
            string description,
            string llmUrl,
            string llmApiKey,
            HttpClient httpClient = null,
            string model = "gpt-4.1-mini",
            CancellationToken cancellationToken = default)
        {
            if (httpClient == null)
                httpClient = new HttpClient();

            if (string.IsNullOrWhiteSpace(llmApiKey))
                throw new ArgumentNullException(nameof(llmApiKey));

            if (string.IsNullOrWhiteSpace(llmUrl))
                llmUrl = "https://api.openai.com/v1";

            var baseUrl = llmUrl.TrimEnd('/');
            var symbolType = kind.ToString();

            var result = new TitleDescriptionReviewResult
            {
                SymbolType = symbolType,
                OriginalTitle = title ?? string.Empty,
                OriginalDescription = description ?? string.Empty,
                SuggestedTitle = title ?? string.Empty,
                SuggestedDescription = description ?? string.Empty
            };

            var userPayload = new
            {
                symbolType = symbolType,
                symbolName = symbolName,
                title = title,
                description = description
            };

            var userPayloadJson = JsonConvert.SerializeObject(userPayload, JsonSettings);

            var systemMessage =
                "You are a senior software architect helping to review titles and descriptions " +
                "for code artifacts (domains and models) in a large C# solution. " +
                "Your job is to check for spelling, grammar, clarity, and whether the description actually " +
                "explains what the domain or model represents in the system.";

            var userMessage =
                "Here is the object to review, as JSON:\n\n" +
                userPayloadJson +
                "\n\n" +
                "Return ONLY a JSON object with this shape (no extra text):\n" +
                "{\n" +
                "  \"suggestedTitle\": string,\n" +
                "  \"suggestedDescription\": string,\n" +
                "  \"titleIssues\": string[],\n" +
                "  \"descriptionIssues\": string[]\n" +
                "}\n";

            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = systemMessage },
                    new ChatMessage { Role = "user", Content = userMessage }
                }
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/chat/completions");

            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", llmApiKey);

            httpRequest.Content = new StringContent(
                JsonConvert.SerializeObject(request, JsonSettings),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var completion = JsonConvert.DeserializeObject<ChatCompletionResponse>(json, JsonSettings);

            var content = completion?.Choices?[0]?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                result.TitleIssues.Add("LLM response was empty.");
                return result;
            }

            try
            {
                var suggestion =
                    JsonConvert.DeserializeObject<TitleDescriptionSuggestionPayload>(content, JsonSettings);

                if (suggestion != null)
                {
                    if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTitle))
                        result.SuggestedTitle = suggestion.SuggestedTitle;

                    if (!string.IsNullOrWhiteSpace(suggestion.SuggestedDescription))
                        result.SuggestedDescription = suggestion.SuggestedDescription;

                    if (suggestion.TitleIssues != null)
                        result.TitleIssues.AddRange(suggestion.TitleIssues);

                    if (suggestion.DescriptionIssues != null)
                        result.DescriptionIssues.AddRange(suggestion.DescriptionIssues);
                }
            }
            catch (JsonException)
            {
                result.TitleIssues.Add("Failed to parse LLM JSON response.");
            }

            return result;
        }

        #region DTOs

        private sealed class ChatCompletionRequest
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("messages")]
            public List<ChatMessage> Messages { get; set; }
        }

        private sealed class ChatMessage
        {
            [JsonProperty("role")]
            public string Role { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }
        }

        private sealed class ChatCompletionResponse
        {
            [JsonProperty("choices")]
            public List<Choice> Choices { get; set; }
        }

        private sealed class Choice
        {
            [JsonProperty("message")]
            public ChatMessage Message { get; set; }
        }

        private sealed class TitleDescriptionSuggestionPayload
        {
            [JsonProperty("suggestedTitle")]
            public string SuggestedTitle { get; set; }

            [JsonProperty("suggestedDescription")]
            public string SuggestedDescription { get; set; }

            [JsonProperty("titleIssues")]
            public List<string> TitleIssues { get; set; }

            [JsonProperty("descriptionIssues")]
            public List<string> DescriptionIssues { get; set; }
        }

        #endregion
    }
}
