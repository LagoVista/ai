using LagoVista.AI.Models;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Services
{
    public partial class AgentKnowledgePackService
    {
        private enum KnowledgeLevel
        {
            Agent,
            AgentRole,
            AgentMode
        }

        public class ProviderDescriptor
        {
            public string Label { get; }
            public IAgentKnowledgeProvider Provider { get; }
            public ProviderDescriptor(string label, IAgentKnowledgeProvider provider)
            {
                Label = label;
                Provider = provider;
            }
        }

        private static void AddInstructions(KnowledgeLane lane, KnowledgeKind kind, IEnumerable<string> instructions)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (instructions == null) return;

            foreach (var inst in instructions)
            {
                if (string.IsNullOrWhiteSpace(inst)) continue;

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = kind,
                    Content = inst
                });
            }
        }

        private static InvokeResult AddInstructionDDR(KnowledgeLane lane, KnowledgeKind kind, IEnumerable<string> orderedIds, IDictionary<string, DdrModelFields> resolved)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) throw new ArgumentNullException(nameof(orderedIds));
            if (resolved == null) throw new ArgumentNullException(nameof(resolved));

            foreach (var id in orderedIds)
            {
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("missing id value in ordered ids for instruction ddrs");

                resolved.TryGetValue(id, out var ddr);
                if (ddr == null) return InvokeResult.FromError($"Could not find resolved ddr for instruction ddr {id}");

                var builder = new StringBuilder();
                builder.AppendLine($"### {ddr.DdrIdentifier} - {ddr.Title}");
                builder.AppendLine(ddr.AgentInstructions);
                builder.AppendLine();

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = kind,
                    Id = id,
                    Content = builder.ToString()
                });
            }

            return InvokeResult.Success;
        }

        private static InvokeResult AddReferenceDDR(KnowledgeLane lane, KnowledgeKind kind, IEnumerable<string> orderedIds, IDictionary<string, DdrModelFields> resolved)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) throw new ArgumentNullException(nameof(orderedIds));
            if (resolved == null) throw new ArgumentNullException(nameof(resolved));

            foreach (var id in orderedIds)
            {
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("missing id value in ordered ids for reference ddrs");

                resolved.TryGetValue(id, out var ddr);
                if (ddr == null) return InvokeResult.FromError($"Could not find resolved ddr for reference ddr {id}");

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = kind,
                    Id = id,
                    Content = $"{ddr.DdrIdentifier}: {ddr.ReferentialSummary}"
                });
            }

            return InvokeResult.Success;
        }

        private InvokeResult AddAvailableTools(KnowledgeLane lane, IEnumerable<string> orderedIds)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) throw new ArgumentNullException(nameof(orderedIds)); ;

            foreach (var id in orderedIds)
            {
                var summary = _toolUsageMetaData.GetToolSummary(id);

                if (string.IsNullOrWhiteSpace(id)) return InvokeResult.FromError($"Could not find available tool {id}");

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = KnowledgeKind.ToolSummary,
                    Id = id,
                    Content = $"{id}: {summary}"
                });
            }

            return InvokeResult.Success;
        }

        private InvokeResult AddActiveTools(KnowledgeLane lane, IEnumerable<string> orderedIds)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (orderedIds == null) throw new ArgumentNullException(nameof(orderedIds)); ;

            foreach (var id in orderedIds)
            {
                if (string.IsNullOrWhiteSpace(id)) return InvokeResult.FromError($"Could not find active tool {id}");

                var usage = _toolUsageMetaData.GetToolUsageMetadata(id);

                var builder = new StringBuilder();
                builder.AppendLine($"### {id} Usage");
                builder.AppendLine(usage);
                builder.AppendLine();

                lane.Items.Add(new KnowledgeItem
                {
                    Kind = KnowledgeKind.ToolUsage,
                    Id = id,
                    Content = builder.ToString()
                });
            }

            return InvokeResult.Success;
        }


        private async Task CollectFullProviderAsync(string label, IAgentKnowledgeProvider provider, KnowledgeAccumulator acc, StringBuilder log, CancellationToken ct)
        {
            if (provider == null) return;

            // Direct fields first
            acc.AddDirect(provider);
            AppendDirectLog(label, provider, log);

            var container = provider as IToolBoxProvider;
            if (container != null)
            {
                foreach (var tb in container.ToolBoxes ?? new List<EntityHeader>())
                {
                    if (tb == null || string.IsNullOrWhiteSpace(tb.Id)) continue;

                    var toolBox = await _toolBoxRepo.GetAgentToolBoxAsync(tb.Id);
                    if (toolBox == null) continue;

                    acc.AddDirect(toolBox);
                    AppendToolBoxLog(label, toolBox, log);
                }
            }
        }

        private async Task CollectToolsOnlyProviderAsync(string label, IAgentKnowledgeProvider provider, KnowledgeAccumulator acc, StringBuilder log, CancellationToken ct)
        {
            if (provider == null) return;

            // Direct fields first
            acc.AddctiveToolOnly(provider);
            AppendDirectLog(label, provider, log);

            var container = provider as IToolBoxProvider;
            if (container != null)
            {
                foreach (var tb in container.ToolBoxes ?? new List<EntityHeader>())
                {
                    if (tb == null || string.IsNullOrWhiteSpace(tb.Id)) continue;

                    var toolBox = await _toolBoxRepo.GetAgentToolBoxAsync(tb.Id);
                    if (toolBox == null) continue;

                    acc.AddctiveToolOnly(toolBox);
                    AppendToolBoxLog(label, toolBox, log);
                }
            }
        }
    }
}
