using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Helper to format server-side tool execution results into a
    /// plain-text block suitable for inclusion in an input_text
    /// content item for the /responses API.
    ///
    /// This isolates our current "contract" with the model for how
    /// tool results are represented in text, so if we ever need to
    /// change it (e.g., OpenAI adds first-class tool_result support),
    /// we can do so in one place.
    /// </summary>
    public static class ToolResultsTextBuilder
    {
        /// <summary>
        /// Builds a human- and LLM-friendly [TOOL_RESULTS] block from the
        /// serialized AgentToolCall collection.
        ///
        /// Expected JSON shape (array of objects) is the serialized form of
        /// AgentToolCall, e.g.:
        ///
        /// [
        ///   {
        ///     "CallId": "call_123",
        ///     "Name": "testing_ping_pong",
        ///     "ArgumentsJson": "{\"message\":\"hello\",\"count\":0}",
        ///     "IsServerTool": true,
        ///     "WasExecuted": true,
        ///     "ResultJson": "{\"reply\":\"pong: hello\",\"count\":1}",
        ///     "ErrorMessage": null
        ///   }
        /// ]
        ///
        /// If the payload can't be parsed or contains no usable entries,
        /// this returns null so callers can simply skip adding the block.
        /// </summary>
        public static string BuildFromToolResultsJson(string toolResultsJson)
        {
            if (string.IsNullOrWhiteSpace(toolResultsJson))
            {
                return null;
            }

            JArray resultsArray;
            try
            {
                resultsArray = JArray.Parse(toolResultsJson);
            }
            catch (JsonException)
            {
                // If shape changes or is malformed, we want tests to catch it,
                // but at runtime we simply skip adding the block to avoid 400s.
                return null;
            }

            if (resultsArray.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();

            sb.AppendLine("[TOOL_RESULTS]");
            sb.AppendLine("The following server-side tools were executed in the previous step.");
            sb.AppendLine("Use these results as ground truth and avoid calling the same tools again");
            sb.AppendLine("for the same purpose unless explicitly instructed.");
            sb.AppendLine();

            foreach (var token in resultsArray)
            {
                var obj = token as JObject;
                if (obj == null)
                {
                    continue;
                }

                var callId = obj.Value<string>("CallId") ?? obj.Value<string>("call_id");
                var name = obj.Value<string>("Name") ?? obj.Value<string>("name");
                var isServerTool = obj.Value<bool?>("IsServerTool") ?? false;
                var wasExecuted = obj.Value<bool?>("WasExecuted") ?? false;
                var argumentsJson = obj.Value<string>("ArgumentsJson");
                var resultJson = obj.Value<string>("ResultJson");
                var errorMessage = obj.Value<string>("ErrorMessage");

                if (string.IsNullOrWhiteSpace(callId) && string.IsNullOrWhiteSpace(name))
                {
                    // Not enough info to be useful; skip this entry.
                    continue;
                }

                // Status classification
                var hasError = !string.IsNullOrWhiteSpace(errorMessage);
                var status = hasError
                    ? "error"
                    : (wasExecuted ? "success" : "not_executed");

                sb.AppendLine("ToolCall:");
                if (!string.IsNullOrWhiteSpace(callId))
                {
                    sb.AppendLine("- call_id: " + callId);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    sb.AppendLine("- tool_name: " + name);
                }

                sb.AppendLine("- is_server_tool: " + isServerTool);
                sb.AppendLine("- was_executed: " + wasExecuted);
                sb.AppendLine("- status: " + status);

                if (hasError)
                {
                    sb.AppendLine("- error_message: " + errorMessage);
                }

                // Arguments as pretty JSON block (or null)
                sb.AppendLine("- arguments_json:");
                sb.AppendLine("```json");
                sb.AppendLine(string.IsNullOrWhiteSpace(argumentsJson) ? "null" : argumentsJson);
                sb.AppendLine("```");

                // Result as pretty JSON block (or null)
                sb.AppendLine("- result_json:");
                sb.AppendLine("```json");
                sb.AppendLine(string.IsNullOrWhiteSpace(resultJson) ? "null" : resultJson);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // If we didn't emit any ToolCall blocks, return null.
            if (sb.ToString().IndexOf("ToolCall:", StringComparison.Ordinal) < 0)
            {
                return null;
            }

            return sb.ToString();
        }
    }
}
