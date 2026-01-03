using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using RingCentral;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// V1 implementation of AGN-035 Agent Knowledge Pack assembly.
    ///
    /// Responsibilities:
    /// - Load AgentContext
    /// - Resolve AgentContextRole and ModeKey
    /// - Assemble and dedupe DDR identifiers and tool names
    /// - Resolve DDR consumption fields via IDdrConsumptionFieldProvider
    /// - Produce a ready-to-render AgentKnowledgePack
    ///
    /// Non-responsibilities:
    /// - Prompt rendering (PKP)
    /// - Tool schema attachment (PKP + ToolSchemaProvider)
    /// - Caching policy (delegated to underlying providers)
    /// </summary>
    public sealed partial class AgentKnowledgePackService : IAgentKnowledgePackService
    {
        private readonly IDdrConsumptionFieldProvider _ddrRepo;
        private readonly IServerToolUsageMetadataProvider _toolUsageMetaData;
        private readonly IAgentToolBoxRepo _toolBoxRepo;
        private readonly IAdminLogger _adminLogger;

     
        public AgentKnowledgePackService(IDdrConsumptionFieldProvider ddrConsumption, IAgentToolBoxRepo toolBoxRepo, IServerToolUsageMetadataProvider toolUsageMetaData, IAdminLogger adminLogger)
        {
            _ddrRepo = ddrConsumption ?? throw new ArgumentNullException(nameof(ddrConsumption));
            _toolUsageMetaData = toolUsageMetaData ?? throw new ArgumentNullException(nameof(toolUsageMetaData));
            _toolBoxRepo = toolBoxRepo ?? throw new ArgumentNullException(nameof(toolBoxRepo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentKnowledgePack>> CreateAsync(IAgentPipelineContext context, bool changedMode)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var providers = new List<ProviderDescriptor>
            {
                 new ProviderDescriptor("AgentContext", context.AgentContext),
                 new ProviderDescriptor("Role", context.Role),
                 new ProviderDescriptor("Mode", context.Mode),
            };

            var shouldCollect = context.Type == AgentPipelineContextTypes.Initial || changedMode;

            var log = new StringBuilder();
            log.Append($"\\r\\n[AKP] Building pack; ChangedMode={changedMode}; Type={context.Type};\\r\\n");

            var acc = new KnowledgeAccumulator();

            if (shouldCollect)
            {
                foreach (var pd in providers)
                {
                    await CollectProviderAsync(pd.Label, pd.Provider, acc, log, context.CancellationToken);
                }
            }

            _adminLogger.Trace($"[AgentKnowledgePackService__CreateAsync] - {log}");

            var instructionIds = acc.InstructionDdrs.Select(x => x.Id).ToList();
            var referenceIds = acc.ReferenceDdrs.Select(x => x.Id).ToList();


            var pack = new AgentKnowledgePack
            {
                AgentContextId = context.AgentContext.Id,
                RoleId = context.Role.Id,
                ModeKey = context.Mode.Id,

                AgentWelcomeMessage = context.AgentContext.WelcomeMessage,
                ConversationWelcomeMessage = context.Role.WelcomeMessage,
                ModeWelcomeMessage = context.Mode.WelcomeMessage,
            };

            EnsureKindCatalog(pack);

            if (shouldCollect)
            {
                var resolvedInstructions = await _ddrRepo.GetDdrModelSummaryAsync(context.Envelope.Org.Id, instructionIds, context.CancellationToken);
                if (!resolvedInstructions.Successful) return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedInstructions.ToInvokeResult());

                var resolvedReferences = await _ddrRepo.GetDdrModelSummaryAsync(context.Envelope.Org.Id, referenceIds, context.CancellationToken);
                if (!resolvedReferences.Successful) return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedReferences.ToInvokeResult());

                AddBaseInstructions(pack, acc);

                AddInstructionDDR(pack.KindCatalog[KnowledgeKind.Instruction].SessionKnowledge, KnowledgeKind.Instruction, instructionIds, resolvedInstructions.Result);
                AddReferenceDDR(pack.KindCatalog[KnowledgeKind.Reference].SessionKnowledge, KnowledgeKind.Reference, referenceIds, resolvedReferences.Result);

                AddActiveTools(pack.KindCatalog[KnowledgeKind.ToolUsage].SessionKnowledge, acc.ActiveTools.Select(x => x.Id));
                AddAvailableTools(pack.KindCatalog[KnowledgeKind.ToolSummary].SessionKnowledge, acc.AvailableTools.Select(x => x.Id));
            }

            pack.ActiveTools = acc.ActiveTools.Select(x => x.Id).ToList();

            _adminLogger.Trace($"[JSON.AKP]={JsonConvert.SerializeObject(pack)}");
            return InvokeResult<AgentKnowledgePack>.Create(pack);
        }
    }
}
