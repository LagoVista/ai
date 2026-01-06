using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.ACP.Commands
{
    [AcpCommand("acp.change_mode", "Change Mode", "Changes the agent mode (session-level).")]
    [AcpTriggers("switch to", "change mode to", "mode")]
    [AcpArgs(1, 1)]
    // Optional safety metadata (enable if/when changing mode is considered a guarded action)
    // [AcpSafety(requiresConfirmation: false, producesSideEffects: false)]
    // Optional arg validation (example: simple token/id-ish constraint)
    // [AcpArgRegex(0, @"^[a-zA-Z][a-zA-Z0-9_\-]*$")]
    public sealed class ChangeModeCommand : IAcpCommand
    {
        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext context, string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (args == null) throw new ArgumentNullException(nameof(args));

            if (args.Length != 1 || String.IsNullOrWhiteSpace(args[0]))
                return InvokeResult<IAgentPipelineContext>.FromError("ChangeModeCommand requires exactly one argument: <modeId>.");

            var modeId = args[0].Trim();

            // TODO: Replace this with your actual mode/session mutation.
            // Examples (depending on your codebase):
            //  - context.Session.ModeId = modeId;
            //  - await context.Services.ModeService.SetModeAsync(context.SessionId, modeId, context.CancellationToken);
            //
            // NOTE: CancellationToken is available on the pipeline context per your note.
            await Task.CompletedTask;

            // TODO: Set a response on the pipeline context so the caller can return immediately.
            // Examples (depending on your pipeline):
            //  - context.SetTextResponse($"Mode changed to '{modeId}'.");
            //  - context.Response = new AgentResponse { ... };

            return InvokeResult<IAgentPipelineContext>.Create(context);
        }
    }
}