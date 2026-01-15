using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

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
    public sealed class AgentExecuteResponseBuilder : IAgentExecuteResponseBuilder
    {
        private readonly IAgentPipelineContextValidator _validator;
        private readonly IAdminLogger _adminlogger;
        private readonly IToolCallManifestRepo _toolCallManifestRepo;

        public AgentExecuteResponseBuilder(IAgentPipelineContextValidator validator, IToolCallManifestRepo toolCallManifestRepo, IAdminLogger adminlogger)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _adminlogger = adminlogger ?? throw new ArgumentNullException(nameof(adminlogger));
            _toolCallManifestRepo = toolCallManifestRepo ?? throw new ArgumentNullException(nameof(toolCallManifestRepo));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> BuildAsync(IAgentPipelineContext ctx)
        {
            _adminlogger.Trace($"{this.Tag()} Creating Response");

            if (ctx == null)
            {
                return InvokeResult<AgentExecuteResponse>.FromError("Pipeline context is null.");
            }

            if (ctx.ResponseType == ResponseTypes.NotReady)
            {
                return InvokeResult<AgentExecuteResponse>.FromError("Response not ready.");
            }

            var validationResult = _validator.ValidatePreStep(ctx, PipelineSteps.ResponseBuilder);
            if (!validationResult.Successful)
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(validationResult);

            var mode = ctx.AgentContext.AgentModes.SingleOrDefault(md => md.Key == ctx.Session.Mode);
            if (mode == null)
            {
                return InvokeResult<AgentExecuteResponse>.FromError($"Agent mode '{ctx.Session.Mode}' not found on agent context.");
            }

            var response = new AgentExecuteResponse
            {
                SessionId = ctx.Session.Id,
                TurnId = ctx.ThisTurn.Id,
                ModeDisplayName = mode.Name
            };

            switch(ctx.ResponseType)
            {
                case ResponseTypes.ACP:
                    _adminlogger.Trace($"{this.Tag()} Creating APC Response");

                    response.AcpIntents.AddRange(ctx.ResponsePayload.AcpIntents);
                    response.PrimaryOutputText = null;
                    response.ToolCalls = null;
                    break;

                case ResponseTypes.Final:
                    _adminlogger.Trace($"{this.Tag()} Creating Final Response");

                    // Final response path.
                    response.Kind = AgentExecuteResponseKind.Final;
                    if (ctx.ThisTurn.Type.Value == AgentSessionTurnType.ChapterEnd)
                    {
                        response.Usage = new LlmUsage() { };
                    }
                    else
                    {
                        response.Usage = ctx.ResponsePayload.Usage;
                    }

                    response.Files = ctx.ResponsePayload.Files;
                    response.ToolCalls = null;
                    response.ClientAcpCalls = null;
                    response.ToolContinuationMessage = null;
                    response.PrimaryOutputText = ctx.ResponsePayload.PrimaryOutputText;
                   
                    // Allowed buckets for Final.          
                    if (ctx.ThisTurn.Warnings != null && ctx.ThisTurn.Warnings.Count > 0)
                    {
                        response.UserWarnings = ctx.ThisTurn.Warnings.ToList();
                    }

                    break;
                case ResponseTypes.ToolContinuation:
                    _adminlogger.Trace($"{this.Tag()} Creating Tool Continuation Response");

                    response.ToolCalls = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls
                        .Where(tc => tc.RequiresClientExecution)
                        .Select(tc => new ClientToolCall() { ToolCallId = tc.ToolCallId, ArgumentsJson = tc.ArgumentsJson, Name = tc.Name }).ToList();

                    if(!response.ToolCalls.Any())
                    {
                        return InvokeResult<AgentExecuteResponse>.FromError("ResponseType.ToolContinuation requires one or more client ToolCalls");
                    }

                    await _toolCallManifestRepo.SetCallToolManifestAsync(ctx.Envelope.Org.Id, ctx.ToolManifestId, ctx.PromptKnowledgeProvider.ToolCallManifest);

                    response.Kind = AgentExecuteResponseKind.ClientToolContinuation;
                    response.PrimaryOutputText = null;
                    response.Files = null;
                    response.Usage = null;
                    response.ClientAcpCalls = null;
                    response.UserWarnings = null;
                    response.ToolContinuationMessage = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolContinuationMessage;
                    break;
                default:
                    return InvokeResult<AgentExecuteResponse>.FromError($"Unknown response type {ctx.ResponseType}.");
            }

            return InvokeResult<AgentExecuteResponse>.Create(response);
        }
    }
}
