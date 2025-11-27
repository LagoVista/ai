using System;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Stateless helper for interpreting individual SSE events from the
    /// OpenAI /responses streaming API.
    ///
    /// This class is pure: no logging, no HTTP, no publishing.
    /// It just parses JSON and returns simple results, which makes it
    /// straightforward to unit test.
    /// </summary>
    public static class OpenAiStreamingEventHelper
    {
        /// <summary>
        /// Represents the interpreted result of a single SSE event payload.
        /// </summary>
        public sealed class SseEventResult
        {
            /// <summary>
            /// Effective event type, e.g. "response.output_text.delta" or "response.completed".
            /// This is derived from "type" in the JSON if present, otherwise falls back to
            /// the SSE event name argument.
            /// </summary>
            public string EventType { get; set; }

            /// <summary>
            /// Any delta text extracted from this event (for output_text.delta).
            /// Null or empty if not applicable.
            /// </summary>
            public string DeltaText { get; set; }

            /// <summary>
            /// Response id extracted from a response.completed event.
            /// Null or empty if not applicable.
            /// </summary>
            public string ResponseId { get; set; }
        }

        /// <summary>
        /// Parse a single SSE event payload (the JSON passed in "data:")
        /// and extract the delta text and/or response id as appropriate.
        ///
        /// This does NOT perform any logging or publishing; it simply
        /// interprets the JSON and returns a small result object.
        /// </summary>
        /// <param name="eventName">
        /// The SSE event name from the "event:" line (may be null/empty).
        /// Used as a fallback if the JSON does not contain a "type" property.
        /// </param>
        /// <param name="dataJson">
        /// The JSON payload from the "data:" line(s), excluding the "data:" prefix.
        /// </param>
        public static SseEventResult AnalyzeEventPayload(string eventName, string dataJson)
        {
            if (string.IsNullOrWhiteSpace(dataJson))
            {
                return new SseEventResult
                {
                    EventType = eventName ?? string.Empty,
                    DeltaText = null,
                    ResponseId = null
                };
            }

            try
            {
                var root = JObject.Parse(dataJson);

                var type = (string)root["type"] ?? eventName ?? string.Empty;

                var result = new SseEventResult
                {
                    EventType = type,
                    DeltaText = null,
                    ResponseId = null
                };

                if (type.EndsWith("output_text.delta", StringComparison.OrdinalIgnoreCase))
                {
                    string deltaText = null;

                    // 1 & 2: "delta": "text"  OR  "delta": { "text": "..." }
                    var deltaToken = root["delta"];
                    if (deltaToken != null)
                    {
                        if (deltaToken.Type == JTokenType.String)
                        {
                            deltaText = (string)deltaToken;
                        }
                        else
                        {
                            deltaText = (string)deltaToken["text"];
                        }
                    }

                    // 3: "output_text": { "delta": "text" } OR { "delta": { "text": "..." } }
                    if (string.IsNullOrEmpty(deltaText))
                    {
                        var outputTextToken = root["output_text"];
                        if (outputTextToken != null)
                        {
                            var nestedDelta = outputTextToken["delta"];
                            if (nestedDelta != null)
                            {
                                if (nestedDelta.Type == JTokenType.String)
                                {
                                    deltaText = (string)nestedDelta;
                                }
                                else
                                {
                                    deltaText = (string)nestedDelta["text"];
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        result.DeltaText = deltaText;
                    }
                }
                else if (string.Equals(type, "response.completed", StringComparison.OrdinalIgnoreCase))
                {
                    // { "response": { "id": "resp_123", ... } }
                    var respId = (string)root["response"]?["id"];
                    if (!string.IsNullOrWhiteSpace(respId))
                    {
                        result.ResponseId = respId;
                    }
                }

                return result;
            }
            catch (JsonException)
            {
                // Malformed JSON – keep it simple but stable.
                return new SseEventResult
                {
                    EventType = eventName ?? string.Empty,
                    DeltaText = null,
                    ResponseId = null
                };
            }
        }


        /// <summary>
        /// Given the JSON payload from a "response.completed" SSE event, extract the inner
        /// "response" object (if present) and return it as a compact JSON string.
        ///
        /// Typical shape:
        /// { "type": "response.completed", "response": { ... full /responses object ... } }
        /// </summary>
        public static InvokeResult<string> ExtractCompletedResponseJson(string completedEventJson)
        {
            if (string.IsNullOrWhiteSpace(completedEventJson))
            {
                return InvokeResult<string>.FromError("empty-payload", "The completed event JSON payload is empty or null.");
            }

            try
            {
                var root = JObject.Parse(completedEventJson);

                var responseToken = root["response"];
                if (responseToken != null && responseToken.Type == JTokenType.Object)
                {
                    return InvokeResult<string>.Create(responseToken.ToString(Formatting.None));
                }

                Console.WriteLine(completedEventJson);
                return InvokeResult<string>.Create(completedEventJson);
            }
            catch (JsonException ex)
            {
                Console.WriteLine(completedEventJson);
                return InvokeResult<string>.FromError("json-parse-failure", $"Failed to parse completed event JSON: {ex.Message}"); 
            }
        }
    }
}
