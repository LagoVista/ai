using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Default implementation of IAgentToolExecutor.
    ///
    /// Uses IAgentToolRegistry to resolve tools by name and delegates
    /// execution to the underlying IAgentTool.ExecuteAsync method.
    /// </summary>
    public class AgentToolExecutor : IAgentToolExecutor
    {
        private readonly IAgentToolFactory _toolFactory;
        private readonly IAdminLogger _logger;
        private readonly IAgentStreamingContext _agentStreamingContext;

        private class DuplicateToolCallResult
        {
            public bool Success { get; } = true;
            public bool CanRetry { get; } = false;
            public string Reason { get; set; } 
        }

        public AgentToolExecutor(IAgentToolFactory agentToolFactory, IAgentStreamingContext agentStreamingContext, IAdminLogger logger)
        {
            _toolFactory = agentToolFactory ?? throw new ArgumentNullException(nameof(agentToolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        public async Task<InvokeResult<AgentToolCallResult>> ExecuteServerToolAsync(AgentToolCall call, IAgentPipelineContext context)
        {
            if (call == null)
            {
                throw new ArgumentNullException(nameof(call));
            }
    
            if (string.IsNullOrWhiteSpace(call.Name))
            {
                var errorMessage = "Tool call name is empty.";
                _logger.AddError(this.Tag(), errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__EmptyName] {errorMessage}");
            }


            var toolResult = _toolFactory.GetTool(call.Name);
            if (!toolResult.Successful)
            {
                var errorMessage = toolResult.ErrorMessage ?? "Failed to resolve server tool.";
                _logger.AddError(this.Tag(),
                    $"Tool '{call.Name}' resolve failed: {errorMessage}");

                return InvokeResult<AgentToolCallResult>.FromInvokeResult(toolResult.ToInvokeResult());
            }

            var tool = toolResult.Result;
            if (tool == null)
            {
                var errorMessage = $"Tool '{call.Name}' resolved to null instance.";
                _logger.AddError(this.Tag(), errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"{this.Tag()} {errorMessage}");
            }


            await _agentStreamingContext.AddWorkflowAsync("calling tool " + call.Name + "...", context.CancellationToken);
            _logger.Trace($"{this.Tag()} Tool '{call.Name}' Was Called, Starting Execution");
            _logger.Trace($"[JSON.ToolCallArgs]={call.ArgumentsJson}");

            if(call.Name != ActivateToolsTool.ToolName && tool.IsToolFullyExecutedOnServer)
            {
                var callCount = context.GetToolCallCount(call.Name);
                if(callCount > 0)
                {

                    var dupResult = new DuplicateToolCallResult()
                    {
                        Reason = $"tool '{call.Name}' was already called {callCount} time(s) in this the last few turns. To prevent infinite loops, the tool will not be executed again."
                    };

                    var toolCallResult = new AgentToolCallResult()
                    {
                        RequiresClientExecution = !tool.IsToolFullyExecutedOnServer,
                        Name = call.Name,
                        ExecutionMs = 0,
                        ToolCallId = call.ToolCallId,
                    };
                    var result = InvokeResult<AgentToolCallResult>.Create(toolCallResult);
                    result.AddWarning($"The tool {call.Name} was previously called {callCount} time(s).  Please use previous resuts and to not call again until arguments change.");
                }
            }              
            
            try
            {
                call.RequiresClientExecution = !tool.IsToolFullyExecutedOnServer;

                var sw = Stopwatch.StartNew();
                var execResult = await tool.ExecuteAsync(call.ArgumentsJson, context);

                var result = new AgentToolCallResult()
                {
                    ToolCallId = call.ToolCallId,
                    Name = call.Name,
                };

                if (!execResult.Successful)
                {
                    var errorMessage = execResult.ErrorMessage;

                    _logger.AddError(this.Tag(), $"Tool '{call.Name}' execution failed: {errorMessage}");

                    return InvokeResult<AgentToolCallResult>.FromInvokeResult(execResult.ToInvokeResult());
                }
                else if(!call.RequiresClientExecution)
                {
                    result.ExecutionMs = Convert.ToInt32(sw.Elapsed.TotalMilliseconds);
                    result.ResultJson = execResult.Result;
                }

                result.RequiresClientExecution = call.RequiresClientExecution;
             
                _logger.Trace($"{this.Tag()} Tool '{call.Name}' Was Successfully Executed in {sw.Elapsed.TotalMilliseconds}ms\r\n", call.Name.ToKVP("tooLName"), result.RequiresClientExecution.ToString().ToKVP("Requires Client Execution."));

                return InvokeResult<AgentToolCallResult>.Create(result);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                var errorMessage = $"Tool '{call.Name}' execution was cancelled.";

                _logger.AddError(this.Tag(),
                    errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__Cancelled] {errorMessage}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Tool '{call.Name}' threw an exception: {ex.Message}";

                _logger.AddException("[AgentToolExecutor_ExecuteServerToolAsync__Exception]", ex);


                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync_Exception] {errorMessage} ");
            }
        }
    }
}
