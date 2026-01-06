using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    public sealed class ClientToolContinuationResolverPipelineStep : PipelineStep, IClientToolContinuationResolverPipelineStep
    {
        private readonly IToolCallManifestRepo _repo;
        private readonly IAdminLogger _adminLogger;

        public ClientToolContinuationResolverPipelineStep(
            IAgentContextLoaderPipelineStap next,
            IAgentPipelineContextValidator validator,
            IToolCallManifestRepo repo,
            IAdminLogger adminLogger) : base(next, validator, adminLogger){
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        protected override PipelineSteps StepType => PipelineSteps.ClientToolContinuationResolver;

        protected override async Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx)
        {
            var manifest = await _repo.GetToolCallManifestAsync(ctx.Envelope.Org.Id, ctx.ToolManifestId);
            if (manifest == null) return InvokeResult<IAgentPipelineContext>.FromError($"Tool Call Manifest with Id {ctx.ToolManifestId} not found.", "CLIENT_TOOL_CONTINUATION_RESOLVER_MANIFEST_NOT_FOUND");

            _adminLogger.Trace($"{this.Tag()} - manifest loaded");

            var reconcileToolResult = new InvokeResult();

            var missingIdsAlreadyReported = new HashSet<string>(StringComparer.Ordinal);

            // Detect duplicate ToolCallIds provided by the client
            var duplicateClientToolCallIds = ctx.Envelope.ToolResults.GroupBy(tr => tr.ToolCallId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            foreach (var duplicateToolCallId in duplicateClientToolCallIds)
            {
                reconcileToolResult.AddSystemError($"Duplicate ToolCallId {duplicateToolCallId} was provided by the client for Manifest {ctx.ToolManifestId}.");
            }

            foreach (var toolCall in manifest.ToolCalls)
            {
                var hasResult = manifest.ToolCallResults.Any(tcr => tcr.ToolCallId == toolCall.ToolCallId);
                if (!hasResult && toolCall.RequiresClientExecution)
                {
                    reconcileToolResult.AddSystemError($"Tool Call {toolCall.Name}, with Id {toolCall.ToolCallId} requires execution but no result was provided.");
                }
            }

            if (reconcileToolResult.Successful)
            {
                _adminLogger.Trace($"{this.Tag()} - tool called vs tool call reqeusted validated.");

                foreach (var toolResult in ctx.Envelope.ToolResults)
                {
                    var pendingCall = manifest.ToolCalls.SingleOrDefault(tc => tc.ToolCallId == toolResult.ToolCallId);
                    if (pendingCall == null)
                    {
                        missingIdsAlreadyReported.Add(toolResult.ToolCallId);
                        reconcileToolResult.AddSystemError($"Tool Call with Id {toolResult.ToolCallId} not found in Manifest {ctx.ToolManifestId}.");
                    }
                    else
                    {
                        if (!pendingCall.RequiresClientExecution)
                            reconcileToolResult.AddSystemError($"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} result was provided, however it was not a client tool type {ctx.ToolManifestId}.");
                        else
                        {
                            var existingResult = manifest.ToolCallResults.SingleOrDefault(tcr => tcr.ToolCallId == toolResult.ToolCallId);
                            if (existingResult != null)
                            {
                                var canApply = true;

                                if (!existingResult.RequiresClientExecution)
                                {
                                    reconcileToolResult.AddSystemError(
                                        $"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} was provided from client, however call request was not marked as requiring client execution.  In Manifest {ctx.ToolManifestId}.");
                                    canApply = false;
                                }

                                if (!String.IsNullOrEmpty(existingResult.ErrorMessage))
                                {
                                    reconcileToolResult.AddSystemError(
                                        $"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} was provided from client, however error message was already set.  In Manifest {ctx.ToolManifestId}.");
                                    canApply = false;
                                }

                                if (!String.IsNullOrEmpty(existingResult.ResultJson))
                                {
                                    reconcileToolResult.AddSystemError(
                                        $"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} was provided from client, however results json was already set.  In Manifest {ctx.ToolManifestId}.");
                                    canApply = false;
                                }

                                if (canApply)
                                {
                                    existingResult.ErrorMessage = toolResult.ErrorMessage;
                                    existingResult.ResultJson = toolResult.ResultJson;
                                    if(!String.IsNullOrEmpty(toolResult.ResultJson))
                                    {
                                        _adminLogger.Trace($"{this.Tag()} - success client tool call", toolResult.ToolCallId.ToKVP("callId"), toolResult.ResultJson.ToKVP("resultJson"));
                                    }
                                    else if(!String.IsNullOrEmpty(toolResult.ErrorMessage))
                                        _adminLogger.Trace($"{this.Tag()} - failed client tool call", toolResult.ToolCallId.ToKVP("callId"), toolResult.ErrorMessage.ToKVP("errorMessage"));
                                    else
                                    {
                                        _adminLogger.AddError(this.Tag(), "neither results nor error message were set", toolResult.ToolCallId.ToKVP("callId"));
                                        reconcileToolResult.AddSystemError($"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} was provided from client, however both result json and error message are empty.  In Manifest {ctx.ToolManifestId}.");
                                    }                                 
                                }
                                else
                                {
                                        _adminLogger.AddError(this.Tag(), "could not apply, review results", toolResult.ToolCallId.ToKVP("callId"));                                    
                                }
                            }
                            else
                            {
                                reconcileToolResult.AddSystemError(
                                    $"Tool Call {pendingCall.Name}, with Id {toolResult.ToolCallId} was not found in Manifest {ctx.ToolManifestId}.");
                            }
                        }
                    }
                }
            }            

            var manifestToolCallIds = manifest.ToolCalls.Select(tc => tc.ToolCallId).ToHashSet(StringComparer.Ordinal);

            var extraClientToolCallIds = ctx.Envelope.ToolResults.Select(tr => tr.ToolCallId).Where(toolCallId => !manifestToolCallIds.Contains(toolCallId)).ToList();

            foreach (var extraToolCallId in extraClientToolCallIds)
            {
                if (missingIdsAlreadyReported.Contains(extraToolCallId))
                {
                    continue;
                }

                reconcileToolResult.AddSystemError($"Tool Call with Id {extraToolCallId} was provided by the client but does not exist in Manifest {ctx.ToolManifestId}.");
            }


            if (!reconcileToolResult.Successful)
            {
                _adminLogger.AddError(this.Tag(), $"Tool call results reconciliation failed", 
                    string.Join(", ", reconcileToolResult.Errors.Select(err => err.Message).ToArray()).ToKVP("errors"));
                return InvokeResult<IAgentPipelineContext>.FromInvokeResult(reconcileToolResult);
            }

            ctx.AttachToolManifest(manifest);
            
            _adminLogger.Trace($"{this.Tag()} - success applying tools");
     
            // Tool manifests are temporary per-turn state.
            // If the client posts results multiple times for the same manifest, we treat it as an error.
            // If we later need an explicit retry mechanism, we can add it; for now, reconcile once and clean up.
            await _repo.RemoveToolCallManifestAsync(ctx.Envelope.Org.Id, ctx.ToolManifestId);
            
            _adminLogger.Trace($"{this.Tag()} - old manifest removed from repo");
                
            return InvokeResult<IAgentPipelineContext>.Create(ctx);
        }
    }
}
