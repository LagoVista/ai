using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// V1 implementation of AGN-035 Agent Knowledge Pack assembly.
    ///
    /// Responsibilities:
    /// - Load AgentContext
    /// - Resolve ConversationContext and Mode
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

        public AgentKnowledgePackService(IDdrConsumptionFieldProvider ddrConsumption)
        {
            _ddrConsumption = ddrConsumption ?? throw new ArgumentNullException(nameof(ddrConsumption));
        }

        public async Task<InvokeResult<AgentKnowledgePack>> CreateAsync(
            string orgId,
            AgentContext agentContext,
            string conversationContextId,
            string mode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orgId)) return InvokeResult<AgentKnowledgePack>.FromError("orgId is required.");
            if (agentContext == null) return InvokeResult<AgentKnowledgePack>.FromError("agentContextId is required.");
            if (string.IsNullOrWhiteSpace(mode)) return InvokeResult<AgentKnowledgePack>.FromError("mode is required.");

            // NOTE: IAgentContextManager requires EntityHeader org/user; this service signature uses orgId only.
            // The manager implementation is expected to allow read access with a system user.
            var org = LagoVista.Core.Models.EntityHeader.Create(orgId, orgId);
            var user = LagoVista.Core.Models.EntityHeader.Create("system", "system");

            
            var conversation = ResolveConversationContext(agentContext, conversationContextId);
            if (conversation == null)
            {
                return InvokeResult<AgentKnowledgePack>.FromError($"ConversationContext '{conversationContextId}' not found.");
            }

            var agentMode = agentContext.AgentModes?.SingleOrDefault(m => string.Equals(m.Key, mode, StringComparison.OrdinalIgnoreCase));
            if (agentMode == null)
            {
                return InvokeResult<AgentKnowledgePack>.FromError($"Mode '{mode}' not found on AgentContext '{agentContext.Name}'.");
            }

            // Collect in deterministic precedence order: Agent -> Conversation -> Mode
            // Dedup is done during collection so downstream DDR lookups are minimized.
            var instructionIds = new List<string>();
            var referenceIds = new List<string>();
            var toolNames = new List<string>();

            // AgentContext
            AddRangeDistinctInOrder(instructionIds, agentContext.AgentInstructionDdrs);
            AddRangeDistinctInOrder(referenceIds, agentContext.ReferenceDdrs);
            AddRangeDistinctInOrder(toolNames, agentContext.AssociatedToolIds);

            // ConversationContext
            AddRangeDistinctInOrder(instructionIds, conversation.AgentInstructionDdrs);
            AddRangeDistinctInOrder(referenceIds, conversation.ReferenceDdrs);
            AddRangeDistinctInOrder(toolNames, conversation.AssociatedToolIds);

            // Mode
            AddRangeDistinctInOrder(instructionIds, agentMode.AgentInstructionDdrs);
            AddRangeDistinctInOrder(referenceIds, agentMode.ReferenceDdrs);
            AddRangeDistinctInOrder(toolNames, agentMode.AssociatedToolIds);

            // Resolve consumption fields (post-dedupe)
            var resolvedInstructions = await _ddrConsumption.GetAgentInstructionsAsync(orgId, instructionIds, cancellationToken);
            if (!resolvedInstructions.Successful)
            {
                return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedInstructions.ToInvokeResult());
            }

            var resolvedReferences = await _ddrConsumption.GetReferentialSummariesAsync(orgId, referenceIds, cancellationToken);
            if (!resolvedReferences.Successful)
            {
                return InvokeResult<AgentKnowledgePack>.FromInvokeResult(resolvedReferences.ToInvokeResult());
            }

            // Build pack
            var pack = new AgentKnowledgePack
            {
                AgentContextId = agentContext.Id,
                ConversationContextId = conversationContextId,
                Mode = agentMode.Key,

                AgentWelcomeMessage = agentContext.WelcomeMessage,
                ConversationWelcomeMessage = conversation.WelcomeMessage,
                ModeWelcomeMessage = agentMode.WelcomeMessage,

                // EnabledToolNames is used by PKP to attach tool schemas.
                EnabledToolNames = toolNames
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

            pack.KindCatalog[KnowledgeKind.Tool] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Tool,
                Title = "Tools",
                BeginMarker = "[BEGIN TOOLS BLOCK]",
                EndMarker = "[END TOOLS BLOCK]",
                InstructionLine = "The following tools are available. Call tools only when needed and provide valid arguments."
            };

            // Populate SessionKnowledge lane as the primary lane in V1.
            // Consumers can migrate items to ConsumableKnowledge as turn-scoped patterns mature.
            AddDdrItems(pack.SessionKnowledge, KnowledgeKind.Instruction, instructionIds, resolvedInstructions.Result);
            AddDdrItems(pack.SessionKnowledge, KnowledgeKind.Reference, referenceIds, resolvedReferences.Result);
            AddToolItems(pack.SessionKnowledge, toolNames);

            return InvokeResult<AgentKnowledgePack>.Create(pack);
        }

        private static ConversationContext ResolveConversationContext(AgentContext agentContext, string conversationContextId)
        {
            if (agentContext == null) return null;

            if (!string.IsNullOrWhiteSpace(conversationContextId))
            {
                return agentContext.ConversationContexts?.SingleOrDefault(cc => string.Equals(cc.Id, conversationContextId, StringComparison.OrdinalIgnoreCase));
            }

            // Fall back to default if no explicit conversationContextId provided.
            if (agentContext.DefaultConversationContext != null && !string.IsNullOrWhiteSpace(agentContext.DefaultConversationContext.Id))
            {
                var id = agentContext.DefaultConversationContext.Id;
                return agentContext.ConversationContexts?.SingleOrDefault(cc => string.Equals(cc.Id, id, StringComparison.OrdinalIgnoreCase));
            }

            return agentContext.ConversationContexts?.FirstOrDefault();
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

        private static void AddDdrItems(
            KnowledgeLane lane,
            KnowledgeKind kind,
            IEnumerable<string> orderedIds,
            IDictionary<string, string> resolved)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) return;

            foreach (var id in orderedIds)
            {
                resolved = resolved ?? new Dictionary<string, string>();
                resolved.TryGetValue(id, out var content);

                if (string.IsNullOrWhiteSpace(content))
                {
                    // V1: fail-fast expectation. If the provider returned success but content is missing,
                    // we still include an empty entry for observability and determinism.
                    // Higher layers can decide whether to treat this as fatal.
                    content = string.Empty;
                }

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = kind,
                    Id = id,
                    Content = content
                });
            }
        }

        private static void AddToolItems(KnowledgeLane lane, IEnumerable<string> toolNames)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (toolNames == null) return;

            foreach (var toolName in toolNames)
            {
                if (string.IsNullOrWhiteSpace(toolName)) continue;

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = KnowledgeKind.Tool,
                    Id = toolName,
                    Content = null
                });
            }
        }
    }
}
