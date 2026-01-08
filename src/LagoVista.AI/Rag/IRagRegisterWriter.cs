using System.Collections.Generic;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Writes hydrated RAG content into PromptKnowledgeProvider under KnowledgeKind.Rag.
    /// </summary>
    public interface IRagRegisterWriter
    {
        /// <summary>
        /// Formats a single combined multi-line block and adds it to the Rag register.
        /// </summary>
        void WriteToRagRegister(object agentPipelineContext, IReadOnlyList<RagHydratedItem> items);
    }
}
