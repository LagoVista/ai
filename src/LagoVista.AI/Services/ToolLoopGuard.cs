using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LagoVista.AI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Interfaces.Services
{
 
}

namespace LagoVista.AI.Services
{
    using LagoVista.AI.Interfaces.Pipeline;
    using LagoVista.AI.Interfaces.Services;

    public sealed class ToolLoopGuard : IAgentToolLoopGuard
    {
        private readonly Dictionary<string, int> _signatureCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly int _warnThreshold;
        private readonly int _suppressThreshold;
        private readonly int _forceFinalizeThresholdRemaining;

        /// <param name="warnThreshold">When signature count reaches this, we emit a warning instruction (but still execute).</param>
        /// <param name="suppressThreshold">When signature count reaches this, we suppress execution and return a synthetic result.</param>
        /// <param name="forceFinalizeThresholdRemaining">When remaining iterations <= this, we add a stronger finalize instruction.</param>
        public ToolLoopGuard(int warnThreshold = 2, int suppressThreshold = 3, int forceFinalizeThresholdRemaining = 1)
        {
            if (warnThreshold < 1) throw new ArgumentOutOfRangeException(nameof(warnThreshold));
            if (suppressThreshold <= warnThreshold) throw new ArgumentOutOfRangeException(nameof(suppressThreshold));
            if (forceFinalizeThresholdRemaining < 0) throw new ArgumentOutOfRangeException(nameof(forceFinalizeThresholdRemaining));

            _warnThreshold = warnThreshold;
            _suppressThreshold = suppressThreshold;
            _forceFinalizeThresholdRemaining = forceFinalizeThresholdRemaining;
        }

        public ToolLoopDecision Evaluate(AgentToolCall toolCall, int iteration, int maxIterations, bool hasAnyToolResultsThisTurn)
        {
            var signature = BuildSignature(toolCall);
            var count = Increment(signature);

            // Per your preference: only start nudging once tools have started returning results.
            // This avoids affecting the first model call in a turn.
            if (!hasAnyToolResultsThisTurn)
            {
                return new ToolLoopDecision(ToolLoopAction.Execute);
            }

            var remaining = maxIterations - iteration;

            // Suppress hard repeats with a synthetic SUCCESS result so the chain doesn't break.
            if (count >= _suppressThreshold)
            {
                var instructions = BuildSuppressInstructions(toolCall, count, remaining, maxIterations);
                var synthetic = CreateSyntheticSuccessResult(toolCall, instructions, executionMs: 0);

                return new ToolLoopDecision(
                    ToolLoopAction.SuppressWithSyntheticResult,
                    additionalInstructions: instructions,
                    syntheticResult: synthetic);
            }

            // Warn on early repeats (still execute).
            if (count >= _warnThreshold)
            {
                var warn = BuildWarnInstructions(toolCall, count, remaining, maxIterations);
                return new ToolLoopDecision(ToolLoopAction.Execute, additionalInstructions: warn);
            }

            return new ToolLoopDecision(ToolLoopAction.Execute);
        }

        public string GetDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tool loop guard signature counts:");
            foreach (var kvp in _signatureCounts)
            {
                sb.AppendLine($"- {kvp.Value}x {kvp.Key}");
            }
            return sb.ToString();
        }

        private int Increment(string signature)
        {
            _signatureCounts.TryGetValue(signature, out var count);
            count++;
            _signatureCounts[signature] = count;
            return count;
        }

        private static string BuildSignature(AgentToolCall toolCall)
        {
            var args = CanonicalizeJson(toolCall.ArgumentsJson);
            return $"{toolCall.Name}::{args}";
        }

        private static string CanonicalizeJson(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) return String.Empty;

            try
            {
                var token = JToken.Parse(json);
                var normalized = NormalizeToken(token);
                return normalized.ToString(Formatting.None);
            }
            catch
            {
                // Not valid JSON, fall back to trimmed raw string.
                return json.Trim();
            }
        }

        private static JToken NormalizeToken(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Object => NormalizeObject((JObject)token),
                JTokenType.Array => NormalizeArray((JArray)token),
                _ => token
            };
        }

        private static JToken NormalizeObject(JObject obj)
        {
            var props = new List<JProperty>();
            foreach (var prop in obj.Properties())
            {
                props.Add(new JProperty(prop.Name, NormalizeToken(prop.Value)));
            }

            props.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

            var normalized = new JObject();
            foreach (var p in props) normalized.Add(p);
            return normalized;
        }

        private static JToken NormalizeArray(JArray arr)
        {
            // Preserve array ordering; arguments often rely on it.
            var normalized = new JArray();
            foreach (var item in arr) normalized.Add(NormalizeToken(item));
            return normalized;
        }

        private string BuildWarnInstructions(AgentToolCall toolCall, int count, int remaining, int maxIterations)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Tool Loop Warning");
            sb.AppendLine($"- Remaining reasoning iterations: {remaining} of {maxIterations}");
            sb.AppendLine($"- You have requested `{toolCall.Name}` with the same arguments {count} times.");
            sb.AppendLine("- Do not repeat the same tool call again unless you change the arguments and explain why the output will differ.");
            return sb.ToString();
        }

        private string BuildSuppressInstructions(AgentToolCall toolCall, int count, int remaining, int maxIterations)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Tool Loop Detected: Execution Suppressed");
            sb.AppendLine($"- Remaining reasoning iterations: {remaining} of {maxIterations}");
            sb.AppendLine($"- The tool call `{toolCall.Name}` with the same arguments has been requested {count} times.");
            sb.AppendLine("- A synthetic tool result was returned to keep the tool-call protocol valid.");
            sb.AppendLine("- Do not request this exact tool call again in this turn.");
            sb.AppendLine("- Proceed without tools: provide the best answer using available context.");
            sb.AppendLine("- If required information is missing, ask exactly one clarifying question.");

            if (remaining <= _forceFinalizeThresholdRemaining)
            {
                sb.AppendLine("- FINALIZE NOW: Do not call tools again; produce the final response.");
            }

            return sb.ToString();
        }

        private static AgentToolCallResult CreateSyntheticSuccessResult(AgentToolCall toolCall, string guidance, int executionMs)
        {
            // Synthetic SUCCESS result. Keep ErrorMessage empty to avoid triggering failure paths.
            // ResultJson includes a structured payload the model can read.
            var payload = new
            {
                suppressed = true,
                reason = "repeat_tool_call_detected",
                tool = toolCall.Name,
                guidance = guidance
            };

            return new AgentToolCallResult
            {
                ToolCallId = toolCall.ToolCallId,
                RequiresClientExecution = toolCall.RequiresClientExecution,
                Name = toolCall.Name,
                ExecutionMs = executionMs,
                ResultJson = JsonConvert.SerializeObject(payload),
                ErrorMessage = String.Empty
            };
        }
    }
}