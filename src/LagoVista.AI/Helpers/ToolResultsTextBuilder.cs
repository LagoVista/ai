using System;
using System.Collections.Generic;
using System.Text;
using LagoVista.AI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Helpers
{
    public static class ToolResultsTextBuilder
    {
        /// <summary>
        /// Build a [TOOL_RESULTS] text block from the serialized tool results JSON.
        /// The input is expected to be a JSON array of AgentToolCall-like objects:
        /// [
        ///   {
        ///     "CallId": "...",
        ///     "Name": "...",
        ///     "ArgumentsJson": "{...}",
        ///     "IsServerTool": true,
        ///     "WasExecuted": true,
        ///     "ResultJson": "{...}",
        ///     "ErrorMessage": null
        ///   },
        ///   ...
        /// ]
        /// </summary>
        /// <param name="toolResultsJson">JSON array of tool result objects.</param>
        /// <returns>A formatted [TOOL_RESULTS] block, or null/empty if nothing usable is found.</returns>
        public static string BuildFromToolResultsJson(string toolResultsJson)
        {
            if (string.IsNullOrWhiteSpace(toolResultsJson))
            {
                Console.WriteLine("[ToolResultsTextBuilder__BuildFromToolResultsJson] - Empty String, aborting");
                return null;
            }

            JArray resultsArray;
            try
            {

                Console.WriteLine($"[ToolResultsTextBuilder__BuildFromToolResultsJson] - Parsing JSON - {toolResultsJson}");
                resultsArray = JArray.Parse(toolResultsJson);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ToolResultsTextBuilder__BuildFromToolResultsJson] - Malformed JSON - {ex.Message}");
                // If the JSON is malformed, don't poison the prompt – just skip it.
                return null;
            }

            if (resultsArray == null || resultsArray.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();

            sb.AppendLine("[TOOL_RESULTS]");
            sb.AppendLine();

            foreach (var token in resultsArray)
            {
                var obj = token as JObject;
                if (obj == null)
                {
                    continue;
                }

                var callId = obj.Value<string>("CallId") ?? obj.Value<string>("callId") ?? "(missing)";
                var name = obj.Value<string>("Name") ?? obj.Value<string>("name") ?? "(missing)";
                var isServerTool = obj.Value<bool?>("IsServerTool");
                var wasExecuted = obj.Value<bool?>("WasExecuted");
                var errorMessage = obj.Value<string>("ErrorMessage");

                var argumentsJson = obj.Value<string>("ArgumentsJson") ?? obj.Value<string>("argumentsJson");
                var resultJson = obj.Value<string>("ResultJson") ?? obj.Value<string>("resultJson");

                sb.AppendLine(string.Format("- CallId: {0}", callId));
                sb.AppendLine(string.Format("  Name: {0}", name));

                if (isServerTool.HasValue)
                {
                    sb.AppendLine(string.Format("  IsServerTool: {0}", isServerTool.Value.ToString().ToLowerInvariant()));
                }

                if (wasExecuted.HasValue)
                {
                    sb.AppendLine(string.Format("  WasExecuted: {0}", wasExecuted.Value.ToString().ToLowerInvariant()));
                }

                if (!string.IsNullOrWhiteSpace(argumentsJson))
                {
                    sb.AppendLine("  ArgumentsJson:");
                    sb.AppendLine("    " + argumentsJson);
                }

                if (!string.IsNullOrWhiteSpace(resultJson))
                {
                    sb.AppendLine("  ResultJson:");
                    sb.AppendLine("    " + resultJson);
                }

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    sb.AppendLine("  ErrorMessage:");
                    sb.AppendLine("    " + errorMessage);
                }

                sb.AppendLine();
            }

            var text = sb.ToString().TrimEnd();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public static string BuildFromToolResults(List<AgentToolCallResult> results)
        {
            if(results == null || results.Count == 0)
            {
                return null;
            }
            var sb = new StringBuilder();

            sb.AppendLine("[TOOL_RESULTS]");
            sb.AppendLine();

            foreach (var result in results)
            {
                sb.AppendLine(string.Format("- CallId: {0}", result.ToolCallId));
                sb.AppendLine(string.Format("  Name: {0}", result.Name));


                //if (!string.IsNullOrWhiteSpace(argumentsJson))
                //{
                //    sb.AppendLine("  ArgumentsJson:");
                //    sb.AppendLine("    " + argumentsJson);
                //}

                if (!string.IsNullOrWhiteSpace(result.ResultJson))
                {
                    sb.AppendLine("  ResultJson:");
                    sb.AppendLine("    " + result.ResultJson);
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    sb.AppendLine("  ErrorMessage:");
                    sb.AppendLine("    " + result.ErrorMessage);
                }

                sb.AppendLine();
            }

            Console.WriteLine(sb.ToString());

            var text = sb.ToString().TrimEnd();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
