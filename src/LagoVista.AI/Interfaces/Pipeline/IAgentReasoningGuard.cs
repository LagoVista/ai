using LagoVista.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces.Pipeline
{

    public enum ToolLoopAction
    {
        Execute = 0,
        SuppressWithSyntheticResult = 1,
    }

    public sealed class ToolLoopDecision
    {
        public ToolLoopDecision(ToolLoopAction action, string additionalInstructions = null, AgentToolCallResult syntheticResult = null)
        {
            Action = action;
            AdditionalInstructions = additionalInstructions;
            SyntheticResult = syntheticResult;
        }

        public ToolLoopAction Action { get; }
        public string AdditionalInstructions { get; }
        public AgentToolCallResult SyntheticResult { get; }
    }

    /// <summary>
    /// Tracks repeated tool calls (same tool + same canonicalized args) during a single reasoner run.
    /// If a loop is detected, it returns a synthetic SUCCESS tool result (so the tool-call protocol remains valid),
    /// plus instructions nudging the model to stop calling tools and finalize.
    /// </summary>
    public interface IAgentToolLoopGuard
    {
        /// <summary>
        /// Evaluate the tool call and decide whether to execute it or suppress it with a synthetic result.
        /// </summary>
        ToolLoopDecision Evaluate(AgentToolCall toolCall, int iteration, int maxIterations, bool hasAnyToolResultsThisTurn);

        /// <summary>
        /// Optional diagnostics string for logging.
        /// </summary>
        string GetDiagnostics();
    }
}
