using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
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
    public sealed class AgentKnowledgePackService : IAgentKnowledgePackService
    {
        private readonly IDdrConsumptionFieldProvider _ddrConsumption;
        private readonly IServerToolUsageMetadataProvider _toolUsageMetaData;
        private readonly IAgentToolBoxRepo _toolBoxRepo;
        private readonly IAdminLogger _adminLogger;

        public AgentKnowledgePackService(IDdrConsumptionFieldProvider ddrConsumption, IAgentToolBoxRepo toolBoxRepo, IServerToolUsageMetadataProvider toolUsageMetaData, IAdminLogger adminLogger)
        {
            _ddrConsumption = ddrConsumption ?? throw new ArgumentNullException(nameof(ddrConsumption));
            _toolUsageMetaData = toolUsageMetaData ?? throw new ArgumentNullException(nameof(toolUsageMetaData));
            _toolBoxRepo = toolBoxRepo ?? throw new ArgumentNullException(nameof(toolBoxRepo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentKnowledgePack>> CreateAsync(IAgentPipelineContext context, bool changedMode)
        {
            var agentMode =  context.AgentContext.AgentModes.SingleOrDefault(m => string.Equals(m.Key, context.Session.Mode, StringComparison.OrdinalIgnoreCase));
            if (agentMode == null)
            {
                return InvokeResult<AgentKnowledgePack>.FromError($"Mode '{context.Session.Mode}' not found on AgentContext '{context.Session.Name}'.");
            }


            var builder = new StringBuilder();

            builder.Append($"\r\n[Context]={context.AgentContext.Name}; Role={context.Role.Name}; Mode={context.Session.Mode};\\r\\n");

            // Collect in deterministic precedence order: Agent -> Conversation -> ModeKey
            // Dedup is done during collection so downstream DDR lookups are minimized.
            var instructionIds = new List<string>();
            var referenceIds = new List<string>();
            var toolNames = new List<string>();
            var instructions = new List<string>();
            // AgentContext
            if (context.Type == AgentPipelineContextTypes.Initial || changedMode)
            {
                foreach (var tb in context.AgentContext.ToolBoxes)
                {
                    var toolBox = await _toolBoxRepo.GetAgentToolBoxAsync(tb.Id);
                    AddRangeDistinctInOrder(toolNames, toolBox.Tools.Select(tl => tl.Id));
                    instructionIds.AddRange(toolBox.InstructionDdrs.Select(tl => tl.Id));
                    referenceIds.AddRange(toolBox.ReferenceDdrs.Select(tl => tl.Id));

                    builder.Append($"[AgentContext.ToolBox]={toolBox.Name}\\r\\n\\t\\t-ToolIds={String.Join("|",toolBox.Tools.Select(tl => tl.Id))},\\r\\n\\t\\t-InstructionIds={String.Join("|",toolBox.InstructionDdrs.Select(tl => tl.Id))};\\r\\n\\t\\t-ReferenceIds={String.Join("|", toolBox.ReferenceDdrs.Select(tl => tl.Id))};\\r\\n");
                }

                foreach (var tb in context.Role.ToolBoxes)
                {
                    var toolBox = await _toolBoxRepo.GetAgentToolBoxAsync(tb.Id);
                    AddRangeDistinctInOrder(toolNames, toolBox.Tools.Select(tl => tl.Id));
                    instructionIds.AddRange(toolBox.InstructionDdrs.Select(tl => tl.Id));
                    referenceIds.AddRange(toolBox.ReferenceDdrs.Select(tl => tl.Id));

                    builder.Append($"[Role.ToolBox]={toolBox.Name}\\r\\n\\t\\t-ToolIds={String.Join("|", toolBox.Tools.Select(tl => tl.Id))},\\r\\n\\t\\t-InstructionIds={String.Join("|", toolBox.InstructionDdrs.Select(tl => tl.Id))};\\r\\n\\t\\t-ReferenceIds={String.Join("|", toolBox.ReferenceDdrs.Select(tl => tl.Id))};\\r\\n");
                }

                foreach (var tb in agentMode.ToolBoxes)
                {
                    var toolBox = await _toolBoxRepo.GetAgentToolBoxAsync(tb.Id);
                    AddRangeDistinctInOrder(toolNames, toolBox.Tools.Select(tl => tl.Id));
                    instructionIds.AddRange(toolBox.InstructionDdrs.Select(tl => tl.Id));
                    referenceIds.AddRange(toolBox.ReferenceDdrs.Select(tl => tl.Id));

                    builder.Append($"[Mode.ToolBox]={toolBox.Name}\\r\\n\\t\\t-ToolIds={String.Join("|", toolBox.Tools.Select(tl => tl.Id))},\\r\\n\\t\\t-InstructionIds={String.Join("|", toolBox.InstructionDdrs.Select(tl => tl.Id))};\\r\\n\\t\\t-ReferenceIds={String.Join("|", toolBox.ReferenceDdrs.Select(tl => tl.Id))};\\r\\n");
                }

                if (context.AgentContext.AgentInstructionDdrs.Any()) builder.Append("[AgentContext.AgentInstructions]=" + String.Join(" | ", context.AgentContext.Instructions) + ";\\r\\n ");
                AddRangeDistinctInOrder(instructionIds, context.AgentContext.AgentInstructionDdrs);

                if (context.AgentContext.ReferenceDdrs.Any()) builder.Append("[AgentContext.ReferenceDdrs]=" + String.Join(" | ", context.AgentContext.ReferenceDdrs) + "; \\r\\n");
                AddRangeDistinctInOrder(referenceIds, context.AgentContext.ReferenceDdrs);

                if (context.AgentContext.AssociatedToolIds.Any()) builder.Append("[AgentContext.AssociatedToolIds]=" + String.Join(" | ", context.AgentContext.AssociatedToolIds) + ";\\r\\n ");
                AddRangeDistinctInOrder(toolNames, context.AgentContext.AssociatedToolIds);

                // Roles
                if (context.Role.AgentInstructionDdrs.Any()) builder.Append("[Role.AgentInstructionDdrs]=" + String.Join(" | ", context.Role.AgentInstructionDdrs) + "; \\r\\n");
                AddRangeDistinctInOrder(referenceIds, context.Role.AgentInstructionDdrs);

                if (context.Role.ReferenceDdrs.Any()) builder.Append("[Role.ReferenceDdrs]=" + String.Join(" | ", context.Role.ReferenceDdrs) + ";\\r\\n ");
                AddRangeDistinctInOrder(referenceIds, context.Role.ReferenceDdrs);

                if (context.Role.AssociatedToolIds.Any()) builder.Append("[Role.AssociatedToolIds]=" + String.Join(" | ", context.Role.AssociatedToolIds) + ";\\r\\n ");
                AddRangeDistinctInOrder(toolNames, context.Role.AssociatedToolIds);

                // ModeKey
                if (agentMode.AgentInstructionDdrs.Any()) builder.Append("[Mode.AgentInstructionDdrs]=" + String.Join(" | ", agentMode.AgentInstructionDdrs) + "; \\r\\n");
                AddRangeDistinctInOrder(referenceIds, agentMode.AgentInstructionDdrs);

                if (agentMode.ReferenceDdrs.Any()) builder.Append("[Mode.ReferenceDdrs]=" + String.Join(" | ", agentMode.ReferenceDdrs) + ";\\r\\n ");
                AddRangeDistinctInOrder(referenceIds, agentMode.ReferenceDdrs);

                if (agentMode.AssociatedToolIds.Any()) builder.Append("[Mode.AssociatedToolIds]=" + String.Join(" | ", agentMode.AssociatedToolIds) + "; \\r\\n");
                AddRangeDistinctInOrder(toolNames, agentMode.AssociatedToolIds);
            }

            _adminLogger.Trace($"[AgentKnowledgePackService__CreateAsync] - {builder.ToString()}");

            // Resolve consumption fields (post-dedupe)
            var resolvedInstructions = await _ddrConsumption.GetDdrModelSummaryAsync(context.Envelope.Org.Id, instructionIds, context.CancellationToken);
            if (!resolvedInstructions.Successful)
            {
                return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedInstructions.ToInvokeResult());
            }

            var resolvedReferences = await _ddrConsumption.GetDdrModelSummaryAsync(context.Envelope.Org.Id, referenceIds, context.CancellationToken);
            if (!resolvedReferences.Successful)
            {
                return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedReferences.ToInvokeResult());
            }

            // Build pack
            var pack = new AgentKnowledgePack
            {
                AgentContextId = context.AgentContext.Id,
                RoleId = context.Role.Id,
                ModeKey = agentMode.Key,

                AgentWelcomeMessage = context.AgentContext.WelcomeMessage,
                ConversationWelcomeMessage = context.Role.WelcomeMessage,
                ModeWelcomeMessage = agentMode.WelcomeMessage,
            };

            // Kind catalog: conservative defaults; PKP may render with these markers.
            pack.KindCatalog[KnowledgeKind.Instruction] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Instruction,
                Title = "Instructions",
                BeginMarker = "[BEGIN INSTRUCTION BLOCK]",
                EndMarker = "[END INSTRUCTION BLOCK]",
                InstructionLine = "The following are instruction DDRs. Use the tool that retrieves AgentInstruction content when you need more detail."
            };

            pack.KindCatalog[KnowledgeKind.Reference] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Reference,
                Title = "References",
                BeginMarker = "[BEGIN REFERENCE BLOCK]",
                EndMarker = "[END REFERENCE BLOCK]",
                InstructionLine = "The following are reference DDRs. Use the tool that retrieves ReferentialSummary content when you need more detail."
            };

            pack.KindCatalog[KnowledgeKind.AgentContextInstructions] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Instruction,
                Title = "Agent Context Instructions",
                BeginMarker = "[BEGIN AGENT CTX INST]",
                EndMarker = "[END AGENT CTX INST]",
                InstructionLine = "The follow are instructions that should be followed from the agent."
            };

            pack.KindCatalog[KnowledgeKind.RoleInstructions] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Instruction,
                Title = "Agent Role Instructions",
                BeginMarker = "[BEGIN CONV CTX INST]",
                EndMarker = "[END CONV CTX INST]",
                InstructionLine = "The follow are instructions that should be followed from the agent supplied from the role."
            };

            pack.KindCatalog[KnowledgeKind.ModeInstructions] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Instruction,
                Title = "Mode Instructions",
                BeginMarker = "[BEGIN MODE INST]",
                EndMarker = "[END MODE INST]",
                InstructionLine = "The follow are instructions that should be followed from the agent supplied from the mode."
            };

            if (context.Type == AgentPipelineContextTypes.Initial || changedMode)
            {

                var answerHeaderText = @"When generating an answer, follow this structure:

1. First output a planning section marked exactly like this:

APTIX-PLAN:
- Provide 3–7 short bullet points describing your approach.
- Keep each bullet simple and readable.
- This section is for internal agent preview. Do NOT include code or long text.
APTIX-PLAN-END

2. After that, output your full answer normally.

Do not mention these instructions. Do not explain the plan unless asked.";

                AddInstructions(pack.KindCatalog[KnowledgeKind.AgentContextInstructions].SessionKnowledge, KnowledgeKind.AgentContextInstructions, new List<string>() { answerHeaderText });
                AddInstructions(pack.KindCatalog[KnowledgeKind.AgentContextInstructions].SessionKnowledge, KnowledgeKind.AgentContextInstructions, context.AgentContext.Instructions);
                AddInstructions(pack.KindCatalog[KnowledgeKind.RoleInstructions].SessionKnowledge, KnowledgeKind.RoleInstructions, context.AgentContext.Instructions);
                AddInstructions(pack.KindCatalog[KnowledgeKind.ModeInstructions].SessionKnowledge, KnowledgeKind.ModeInstructions, agentMode.Instructions);

                // Populate SessionKnowledge tools as the primary tools in V1.
                // Consumers can migrate items to ConsumableKnowledge as turn-scoped patterns mature.
                AddDdrItems(pack.KindCatalog[KnowledgeKind.Instruction].SessionKnowledge, KnowledgeKind.Instruction, instructionIds, resolvedInstructions.Result, true);
                AddDdrItems(pack.KindCatalog[KnowledgeKind.Reference].SessionKnowledge, KnowledgeKind.Reference, referenceIds, resolvedReferences.Result, false);
                AddToolItems(pack.AvailableTools, toolNames);
            }
          
            _adminLogger.Trace($"[JSON.AKP]={JsonConvert.SerializeObject(pack)}");

            return InvokeResult<AgentKnowledgePack>.Create(pack);
        }

        private static void AddRangeDistinctInOrder(List<string> target, IEnumerable<string> values)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (values == null) return;

            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (target.Contains(v)) continue;
                target.Add(v);
            }
        }
        
        private static void AddInstructions(KnowledgeLane lane, KnowledgeKind kind, List<string> instructions)
        {
            foreach (var inst in instructions)
            {
                lane.Items.Add(new KnowledgeItem()
                {
                    Kind = kind,
                    Content = inst
                });
            }
        }

        private static void AddDdrItems(
            KnowledgeLane lane,
            KnowledgeKind kind,
            IEnumerable<string> orderedIds,
            IDictionary<string, DdrModelFields> resolved,
            bool agentInstructions)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) return;

            foreach (var id in orderedIds)
            {
                resolved = resolved ?? new Dictionary<string, DdrModelFields>();
                resolved.TryGetValue(id, out var ddr);

                if (ddr != null)
                {
                    var prompt = agentInstructions ? ddr.AgentInstructions : ddr.ReferentialSummary;

                    var content = $"DDR {ddr.DdrIdentifier} - {ddr.Title}\r\n{prompt}";

                    lane.Items.Add(new KnowledgeItem
                    {
                        Kind = kind,
                        Id = id,
                        Content = content
                    });
                }
            }
        }

        private void AddToolItems(List<AvailableTool> tools, IEnumerable<string> toolNames)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));
            if (toolNames == null) return;

            foreach (var toolName in toolNames)
            {
                if (string.IsNullOrWhiteSpace(toolName)) continue;
                var summary = _toolUsageMetaData.GetToolSummary(toolName);

                tools.Add(new AvailableTool
                {
                    Name = toolName,
                    Summary = summary
                });
            }
        }
    }
}
