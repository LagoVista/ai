using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Default writer that builds a single multi-line block and adds it to the Rag register.
    ///
    /// NOTE: This is intentionally loosely coupled (object context) to avoid namespace/type
    /// conflicts during initial generation. Wire to IAgentPipelineContext in your solution.
    /// </summary>
    public sealed class RagRegisterWriter : IRagRegisterWriter
    {
        public const string DefaultBeginMarker = "--- BEGIN RAG CONTEXT ---";
        public const string DefaultEndMarker = "--- END RAG CONTEXT ---";
        public const string DefaultInstructionLine = "Use the following retrieved context when answering.";

        public string BeginMarker { get; set; } = DefaultBeginMarker;
        public string EndMarker { get; set; } = DefaultEndMarker;
        public string InstructionLine { get; set; } = DefaultInstructionLine;

        public void WriteToRagRegister(object agentPipelineContext, IReadOnlyList<RagHydratedItem> items)
        {
            if (agentPipelineContext == null) throw new ArgumentNullException(nameof(agentPipelineContext));
            if (items == null) throw new ArgumentNullException(nameof(items));

            // TODO: Replace reflection/dynamic usage with direct IAgentPipelineContext reference.
            // Expected shape:
            //   ctx.PromptKnowledgeProvider.GetOrCreateRegister(KnowledgeKind.Rag, ContextClassification.Consumable).Add(block)
            dynamic ctx = agentPipelineContext;

            var sb = new StringBuilder();
            sb.AppendLine(BeginMarker);
            sb.AppendLine(InstructionLine);

            foreach (var item in items)
            {
                if (item == null) continue;
                item.Validate();

                // V1: content-only is acceptable; URLs are included as additional lines when present.
                sb.AppendLine(item.Content);

                if (!String.IsNullOrWhiteSpace(item.SummaryUrl))
                    sb.AppendLine($"Summary: {item.SummaryUrl}");

                if (!String.IsNullOrWhiteSpace(item.DetailsUrl))
                    sb.AppendLine($"Details: {item.DetailsUrl}");

                sb.AppendLine();
            }

            sb.AppendLine(EndMarker);

            var register = ctx.PromptKnowledgeProvider.GetOrCreateRegister(
                LagoVista.AI.Models.KnowledgeKind.Rag,
                LagoVista.AI.Models.Context.ContextClassification.Consumable);

            register.Add(sb.ToString());
        }
    }
}
