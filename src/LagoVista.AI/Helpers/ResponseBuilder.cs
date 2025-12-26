using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Builds the external AgentExecuteResponse (AGN-033) from an IAgentPipelineContext.
    ///
    /// IMPORTANT:
    /// - This implementation uses only types and members explicitly provided on IAgentPipelineContext.
    /// - ToolCallManifest is NOT exposed on IAgentPipelineContext, so client-tool-continuation payloads
    ///   cannot be constructed without additional contract surface.
    /// </summary>
    public sealed class ResponseBuilder : IAgentExecuteResponseBuilder
    {
        public Task<InvokeResult<AgentExecuteResponse>> BuildAsync(IAgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromError("Pipeline context is null."));
            }

            if(ctx.ResponseType == ResponseTypes.NotReady)
            {
                return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromError("Response not ready."));
            }

            var validationResult = ctx.Validate(PipelineSteps.ResponseBuilder);
            if(!validationResult.Successful) 
                return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromInvokeResult(validationResult));

            var mode = ctx.AgentContext.AgentModes.SingleOrDefault(md => md.Key == ctx.Session.Mode);
            if (mode == null)
            {
                return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromError($"Agent mode '{ctx.Session.Mode}' not found on agent context."));
            }

            var response = new AgentExecuteResponse
            {
                SessionId = ctx.Session.Id,
                TurnId = ctx.Turn.Id,
                ModeDisplayName = mode.DisplayName
            };

            switch(ctx.ResponseType)
            {
                case ResponseTypes.Final:
                    // Final response path.
                    response.Kind = AgentExecuteResponseKind.Final;
                    response.Usage = ctx.ResponsePayload.Usage;
                    response.Files = ctx.ResponsePayload.Files;
                    response.ToolCalls = null;
                    response.ToolContinuationMessage = null;
                    response.PrimaryOutputText = ctx.ResponsePayload.PrimaryOutputText;
                   
                    // Allowed buckets for Final.          
                    if (ctx.Turn.Warnings != null && ctx.Turn.Warnings.Count > 0)
                    {
                        response.UserWarnings = ctx.Turn.Warnings.ToList();
                    }

                    break;
                case ResponseTypes.ToolContinuation:
                    response.ToolCalls = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls
                        .Where(tc => tc.RequiresClientExecution)
                        .Select(tc => new ClientToolCall() { ToolCallId = tc.ToolCallId, ArgumentsJson = tc.ArgumentsJson, Name = tc.Name }).ToList();

                    if(!response.ToolCalls.Any())
                    {
                        return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromError("ResponseType.ToolContinuation requires one or more client ToolCalls"));
                    }

                    response.Kind = AgentExecuteResponseKind.ClientToolContinuation;
                    response.PrimaryOutputText = null;
                    response.Files = null;
                    response.Usage = null;
                    response.UserWarnings = null;
                    response.ToolContinuationMessage = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolContinuationMessage;
                    break;
                default:
                    return Task.FromResult(InvokeResult<AgentExecuteResponse>.FromError($"Unknown response type {ctx.ResponseType}."));
            }

            return Task.FromResult(InvokeResult<AgentExecuteResponse>.Create(response));
        }
    }
}
