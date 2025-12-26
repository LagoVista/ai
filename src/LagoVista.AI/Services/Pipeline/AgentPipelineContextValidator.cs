using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.Core.Validation;
using System;
using System.Linq;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class AgentPipelineContextValidator : IAgentPipelineContextValidator
    {
        public InvokeResult ValidateCore(IAgentPipelineContext ctx)
        {
            if (ctx == null) return InvokeResult.FromError("Pipeline context is null.");

            var result = new InvokeResult();

            ValidateCore_Invariants(ctx, result);
            if (!result.Successful) return result;

            ValidateCore_TypeBasedEnvelopeRules(ctx, result);
            return result;
        }

        public InvokeResult ValidatePreStep(IAgentPipelineContext ctx, PipelineSteps step)
        {
            if (ctx == null) return InvokeResult.FromError("Pipeline context is null.");

            // Always enforce Core first.
            var core = ValidateCore(ctx);
            if (!core.Successful) return core;

            return step switch
            {
                PipelineSteps.RequestHandler => ValidatePre_RequestHandler(ctx),
                PipelineSteps.SessionRestorer => ValidatePre_SessionRestorer(ctx),
                PipelineSteps.AgentContextResolver => ValidatePre_AgentContextResolver(ctx),
                PipelineSteps.ClientToolContinuationResolver => ValidatePre_ClientToolContinuationResolver(ctx),
                PipelineSteps.AgentSessionCreator => ValidatePre_AgentSessionCreator(ctx),
                PipelineSteps.AgentContextLoader => ValidatePre_AgentContextLoader(ctx),
                PipelineSteps.PromptKnowledgeProviderInitializer => ValidatePre_PromptContentProvider(ctx),
                PipelineSteps.Reasoner => ValidatePre_Reasoner(ctx),
                PipelineSteps.LLMClient => ValidatePre_LLMClient(ctx),
                PipelineSteps.ResponseBuilder => ValidatePre_ResponseBuilder(ctx),
                _ => InvokeResult.Success,
            };
        }

        public InvokeResult ValidatePostStep(IAgentPipelineContext ctx, PipelineSteps step)
        {
            if (ctx == null) return InvokeResult.FromError("Pipeline context is null.");

            // Always enforce Core first.
            var core = ValidateCore(ctx);
            if (!core.Successful) return core;

            return step switch
            {
                PipelineSteps.RequestHandler => ValidatePost_RequestHandler(ctx),
                PipelineSteps.SessionRestorer => ValidatePost_SessionRestorer(ctx),
                PipelineSteps.AgentContextResolver => ValidatePost_AgentContextResolver(ctx),
                PipelineSteps.ClientToolContinuationResolver => ValidatePost_ClientToolContinuationResolver(ctx),
                PipelineSteps.AgentSessionCreator => ValidatePost_AgentSessionCreator(ctx),
                PipelineSteps.AgentContextLoader => ValidatePost_AgentContextLoader(ctx),
                PipelineSteps.PromptKnowledgeProviderInitializer => ValidatePost_PromptContentProvider(ctx),
                PipelineSteps.Reasoner => ValidatePost_Reasoner(ctx),
                PipelineSteps.LLMClient => ValidatePost_LLMClient(ctx),
                PipelineSteps.ResponseBuilder => ValidatePost_ResponseBuilder(ctx),
                _ => InvokeResult.Success,
            };
        }

        public InvokeResult ValidateToolCallManifest(ToolCallManifest manifest)
        {
            if (manifest == null) return InvokeResult.FromError("ToolCallManifest is null.");

            var result = new InvokeResult();

            var toolCalls = manifest.ToolCalls ?? new System.Collections.Generic.List<AgentToolCall>();
            var toolResults = manifest.ToolCallResults ?? new System.Collections.Generic.List<AgentToolCallResult>();

            if (toolCalls.Count != toolResults.Count)
            {
                result.Errors.Add(new ErrorMessage($"ToolCallManifest mismatch: ToolCalls={toolCalls.Count}, ToolCallResults={toolResults.Count}."));
                return result;
            }

            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                var tr = toolResults[i];

                if (tc == null)
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: ToolCall is null."));
                    return result;
                }

                if (tr == null)
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: ToolCallResult is null."));
                    return result;
                }

                if (String.IsNullOrWhiteSpace(tc.ToolCallId))
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: ToolCallId is required."));
                    return result;
                }

                if (String.IsNullOrWhiteSpace(tc.Name))
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: ToolCall Name is required (ToolCallId '{tc.ToolCallId}')."));
                    return result;
                }

                if (!String.Equals(tc.ToolCallId, tr.ToolCallId, StringComparison.Ordinal))
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: ToolCallId mismatch (call '{tc.ToolCallId}' vs result '{tr.ToolCallId}')."));
                    return result;
                }

                if (!String.Equals(tc.Name, tr.Name, StringComparison.Ordinal))
                {
                    result.Errors.Add(new ErrorMessage($"ToolCallManifest invalid at index {i}: tool name mismatch for ToolCallId '{tc.ToolCallId}' (call '{tc.Name}' vs result '{tr.Name}')."));
                    return result;
                }

                // LOCKED rule: All tool call results MUST have ResultJson.
                if (String.IsNullOrWhiteSpace(tr.ResultJson))
                {
                    var reason = !String.IsNullOrWhiteSpace(tr.ErrorMessage) ? tr.ErrorMessage : "ResultJson is required.";
                    result.Errors.Add(new ErrorMessage($"Tool call '{tc.Name}' ({tc.ToolCallId}) failed validation: {reason}"));
                    return result;
                }
            }

            return InvokeResult.Success;
        }

        private static void ValidateCore_Invariants(IAgentPipelineContext ctx, InvokeResult result)
        {
            if (!Enum.IsDefined(typeof(AgentPipelineContextTypes), ctx.Type))
                result.Errors.Add(new ErrorMessage("Invalid AgentPipelineContextTypes value."));

            if (String.IsNullOrEmpty(ctx.TimeStamp))
                result.Errors.Add(new ErrorMessage("TimeStamp is required."));

            if (String.IsNullOrEmpty(ctx.CorrelationId))
                result.Errors.Add(new ErrorMessage("CorrelationId is required."));

            if (ctx.Envelope?.Org == null)
                result.Errors.Add(new ErrorMessage("Envelope.Org is required."));

            if (ctx.Envelope?.User == null)
                result.Errors.Add(new ErrorMessage("Envelope.User is required."));
        }

        private static void ValidateCore_TypeBasedEnvelopeRules(IAgentPipelineContext ctx, InvokeResult result)
        {
            var hasInstructions = !String.IsNullOrWhiteSpace(ctx.Envelope?.Instructions);
            var hasArtifacts = (ctx.Envelope?.InputArtifacts?.Count ?? 0) > 0;
            var hasClipboard = (ctx.Envelope?.ClipBoardImages?.Count ?? 0) > 0;

            if (ctx.Type == AgentPipelineContextTypes.Initial || ctx.Type == AgentPipelineContextTypes.FollowOn)
            {
                if (!hasInstructions && !hasArtifacts && !hasClipboard)
                    result.Errors.Add(new ErrorMessage("At least one of Instructions, InputArtifacts, or ClipBoardImages must be provided."));
            }

            switch (ctx.Type)
            {
                case AgentPipelineContextTypes.Initial:
                    if (!String.IsNullOrEmpty(ctx.Envelope?.ConversationContextId) && String.IsNullOrEmpty(ctx.Envelope?.AgentContextId))
                        result.Errors.Add(new ErrorMessage("ConversationContextId must be empty when AgentContextId is not provided."));

                    if (!String.IsNullOrEmpty(ctx.Envelope?.SessionId) || !String.IsNullOrEmpty(ctx.Envelope?.TurnId))
                        result.Errors.Add(new ErrorMessage("SessionId and TurnId must be empty for Initial requests."));

                    if ((ctx.Envelope?.ToolResults?.Count ?? 0) > 0)
                        result.Errors.Add(new ErrorMessage("ToolResults must be empty for Initial requests."));
                    break;

                case AgentPipelineContextTypes.FollowOn:
                    if (String.IsNullOrEmpty(ctx.Envelope?.SessionId))
                        result.Errors.Add(new ErrorMessage("SessionId is required for FollowOn requests."));

                    if (String.IsNullOrEmpty(ctx.Envelope?.TurnId))
                        result.Errors.Add(new ErrorMessage("TurnId is required for FollowOn requests."));

                    if ((ctx.Envelope?.ToolResults?.Count ?? 0) > 0)
                        result.Errors.Add(new ErrorMessage("ToolResults must be empty for FollowOn requests."));
                    break;

                case AgentPipelineContextTypes.ClientToolCallContinuation:
                    if (String.IsNullOrEmpty(ctx.Envelope?.SessionId))
                        result.Errors.Add(new ErrorMessage("SessionId is required for ClientToolCallContinuation requests."));

                    if (String.IsNullOrEmpty(ctx.Envelope?.TurnId))
                        result.Errors.Add(new ErrorMessage("TurnId is required for ClientToolCallContinuation requests."));

                    if ((ctx.Envelope?.ToolResults?.Count ?? 0) == 0)
                        result.Errors.Add(new ErrorMessage("ToolResults must contain at least one row for ClientToolCallContinuation requests."));
                    break;
            }
        }

        private static InvokeResult ValidatePre_RequestHandler(IAgentPipelineContext ctx)
        {
            // PRE - N/A
            return InvokeResult.Success;
        }

        private InvokeResult ValidatePost_RequestHandler(IAgentPipelineContext ctx)
        {
            // POST - Must validate with ValidateCore
            return ValidateCore(ctx);
        }

        private static InvokeResult ValidatePre_SessionRestorer(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (String.IsNullOrEmpty(ctx.Envelope?.SessionId))
                result.Errors.Add(new ErrorMessage("SessionRestorer PRE: Envelope.SessionId is required."));

            if (String.IsNullOrEmpty(ctx.Envelope?.TurnId))
                result.Errors.Add(new ErrorMessage("SessionRestorer PRE: Envelope.TurnId is required."));

            if (ctx.Session != null)
                result.Errors.Add(new ErrorMessage("SessionRestorer PRE: ctx.Session must be null."));

            if (ctx.Turn != null)
                result.Errors.Add(new ErrorMessage("SessionRestorer PRE: ctx.Turn must be null."));

            return result;
        }

        private static InvokeResult ValidatePost_SessionRestorer(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("SessionRestorer POST: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("SessionRestorer POST: ctx.Turn must be populated."));

            if (ctx.Session != null && String.IsNullOrWhiteSpace(ctx.Session.Mode))
                result.Errors.Add(new ErrorMessage("SessionRestorer POST: Session.Mode must have a value."));

            if (ctx.Turn != null && String.Equals(ctx.Turn.Id, ctx.Envelope?.TurnId, StringComparison.Ordinal))
                result.Errors.Add(new ErrorMessage("SessionRestorer POST: Turn.Id must NOT equal Envelope.TurnId."));

            return result;
        }

        private static InvokeResult ValidatePre_AgentContextResolver(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session != null)
                result.Errors.Add(new ErrorMessage("AgentContextResolver PRE: ctx.Session must be null."));

            if (ctx.Turn != null)
                result.Errors.Add(new ErrorMessage("AgentContextResolver PRE: ctx.Turn must be null."));

            return result;
        }

        private static InvokeResult ValidatePost_AgentContextResolver(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.AgentContext == null)
                result.Errors.Add(new ErrorMessage("AgentContextResolver POST: ctx.AgentContext must be populated."));

            if (ctx.ConversationContext == null)
                result.Errors.Add(new ErrorMessage("AgentContextResolver POST: ctx.ConversationContext must be populated."));

            return result;
        }

        private static InvokeResult ValidatePre_ClientToolContinuationResolver(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            // You locked this: AgentContext/ConversationContext should be null pre and post.
            if (ctx.AgentContext != null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver PRE: ctx.AgentContext must be null."));

            if (ctx.ConversationContext != null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver PRE: ctx.ConversationContext must be null."));

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver PRE: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver PRE: ctx.Turn must be populated."));

            if ((ctx.Envelope?.ToolResults?.Count ?? 0) == 0)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver PRE: Envelope.ToolResults must contain at least one row."));

            return result;
        }

        private InvokeResult ValidatePost_ClientToolContinuationResolver(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.AgentContext != null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: ctx.AgentContext must be null."));

            if (ctx.ConversationContext != null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: ctx.ConversationContext must be null."));

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: ctx.Turn must be populated."));

            if (ctx.Turn != null && !String.Equals(ctx.Turn.Id, ctx.Envelope?.TurnId, StringComparison.Ordinal))
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: Turn.Id must equal Envelope.TurnId."));

            if (ctx.PromptKnowledgeProvider?.ToolCallManifest == null)
            {
                result.Errors.Add(new ErrorMessage("ClientToolContinuationResolver POST: ToolCallManifest must be populated."));
                return result;
            }

            var manifestResult = ValidateToolCallManifest(ctx.PromptKnowledgeProvider.ToolCallManifest);
            if (!manifestResult.Successful)
                return manifestResult;

            return result;
        }

        private static InvokeResult ValidatePre_AgentSessionCreator(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.AgentContext == null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator PRE: ctx.AgentContext must be populated."));

            if (ctx.ConversationContext == null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator PRE: ctx.ConversationContext must be populated."));

            if (ctx.Session != null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator PRE: ctx.Session must be null."));

            if (ctx.Turn != null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator PRE: ctx.Turn must be null."));

            return result;
        }

        private static InvokeResult ValidatePost_AgentSessionCreator(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator POST: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("AgentSessionCreator POST: ctx.Turn must be populated."));

            return result;
        }

        private static InvokeResult ValidatePre_AgentContextLoader(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("AgentContextLoader PRE: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("AgentContextLoader PRE: ctx.Turn must be populated."));

            return result;
        }

        private static InvokeResult ValidatePost_AgentContextLoader(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.AgentContext == null)
                result.Errors.Add(new ErrorMessage("AgentContextLoader POST: ctx.AgentContext must be populated."));

            if (ctx.ConversationContext == null)
                result.Errors.Add(new ErrorMessage("AgentContextLoader POST: ctx.ConversationContext must be populated."));

            return result;
        }

        private static InvokeResult ValidatePre_PromptContentProvider(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("PromptContentProvider PRE: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("PromptContentProvider PRE: ctx.Turn must be populated."));

            if (ctx.AgentContext == null)
                result.Errors.Add(new ErrorMessage("PromptContentProvider PRE: ctx.AgentContext must be populated."));

            if (ctx.ConversationContext == null)
                result.Errors.Add(new ErrorMessage("PromptContentProvider PRE: ctx.ConversationContext must be populated."));

            // If tool continuation, ToolCallManifest must be non-null.
            if (ctx.ResponseType == ResponseTypes.ToolContinuation && ctx.PromptKnowledgeProvider?.ToolCallManifest == null)
                result.Errors.Add(new ErrorMessage("PromptContentProvider PRE: ToolCallManifest must be non-null for tool continuation."));

            return result;
        }

        private static InvokeResult ValidatePost_PromptContentProvider(IAgentPipelineContext ctx)
        {
            // We are not validating provider internal state here.
            return InvokeResult.Success;
        }

        private static InvokeResult ValidatePre_Reasoner(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.Session == null)
                result.Errors.Add(new ErrorMessage("Reasoner PRE: ctx.Session must be populated."));

            if (ctx.Turn == null)
                result.Errors.Add(new ErrorMessage("Reasoner PRE: ctx.Turn must be populated."));

            if (ctx.AgentContext == null)
                result.Errors.Add(new ErrorMessage("Reasoner PRE: ctx.AgentContext must be populated."));

            if (ctx.ConversationContext == null)
                result.Errors.Add(new ErrorMessage("Reasoner PRE: ctx.ConversationContext must be populated."));

            if (ctx.PromptKnowledgeProvider == null)
                result.Errors.Add(new ErrorMessage("Reasoner PRE: ctx.PromptContentProvider must be populated."));

            return result;
        }

        private static InvokeResult ValidatePost_Reasoner(IAgentPipelineContext ctx)
        {
            return InvokeResult.Success;
        }

        private InvokeResult ValidatePre_LLMClient(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.ResponsePayload != null)
                result.Errors.Add(new ErrorMessage("LLMClient PRE: ResponsePayload must be null."));

            // For tool continuation calls we can validate the tool manifest (when present).
            if (ctx.Type == AgentPipelineContextTypes.ClientToolCallContinuation)
            {
                if (ctx.PromptKnowledgeProvider?.ToolCallManifest == null)
                {
                    result.Errors.Add(new ErrorMessage("LLMClient PRE: ToolCallManifest must be populated for ClientToolCallContinuation."));
                    return result;
                }

                var manifest = ValidateToolCallManifest(ctx.PromptKnowledgeProvider.ToolCallManifest);
                if (!manifest.Successful) return manifest;
            }

            return result;
        }

        private static InvokeResult ValidatePost_LLMClient(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            var hasPayload = ctx.ResponsePayload != null;
            var hasClientToolCalls = ctx.HasClientToolCalls;

            if (hasPayload && hasClientToolCalls)
                result.Errors.Add(new ErrorMessage("LLMClient POST: ResponsePayload and client ToolCalls cannot both be present."));

            if (!hasPayload && !hasClientToolCalls)
                result.Errors.Add(new ErrorMessage("LLMClient POST: Must produce either a ResponsePayload (final) or client ToolCalls (tool continuation)."));

            // If any tool calls exist after LLM, they must require client execution.
            var toolCalls = ctx.PromptKnowledgeProvider?.ToolCallManifest?.ToolCalls;
            if (toolCalls != null && toolCalls.Count > 0)
            {
                if (toolCalls.Any(tc => tc != null && !tc.RequiresClientExecution))
                    result.Errors.Add(new ErrorMessage("LLMClient POST: All returned ToolCalls must require client execution."));
            }

            return result;
        }

        private static InvokeResult ValidatePre_ResponseBuilder(IAgentPipelineContext ctx)
        {
            var result = new InvokeResult();

            if (ctx.ResponseType == ResponseTypes.NotReady)
                result.Errors.Add(new ErrorMessage("ResponseBuilder PRE: ResponseType must not be NotReady."));

            if (ctx.ResponseType != ResponseTypes.Final && ctx.ResponseType != ResponseTypes.ToolContinuation)
                result.Errors.Add(new ErrorMessage($"ResponseBuilder PRE: Unknown ResponseType '{ctx.ResponseType}'."));

            if (ctx.ResponseType == ResponseTypes.Final)
            {
                if (ctx.ResponsePayload == null)
                    result.Errors.Add(new ErrorMessage("ResponseBuilder PRE: ResponsePayload is required for Final responses."));
                else if (String.IsNullOrWhiteSpace(ctx.ResponsePayload.PrimaryOutputText))
                    result.Errors.Add(new ErrorMessage("ResponseBuilder PRE: ResponsePayload.PrimaryOutputText is required for Final responses."));
            }

            if (ctx.ResponseType == ResponseTypes.ToolContinuation)
            {
                if (ctx.PromptKnowledgeProvider?.ToolCallManifest == null)
                    result.Errors.Add(new ErrorMessage("ResponseBuilder PRE: ToolCallManifest is required for ToolContinuation responses."));
            }

            return result;
        }

        private static InvokeResult ValidatePost_ResponseBuilder(IAgentPipelineContext ctx)
        {
            // Builder must not mutate context. We validate the same PRE rules as POST.
            return ValidatePre_ResponseBuilder(ctx);
        }


    }
}
