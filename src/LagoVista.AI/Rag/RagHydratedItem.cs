using System;

namespace LagoVista.AI.Rag
{
    /// <summary>
    /// Hydrated RAG content item suitable for inclusion in PromptKnowledgeProvider.
    /// V1 shape per AGN-000038.
    /// </summary>
    public sealed class RagHydratedItem
    {
        public string Content { get; set; }
        public string SummaryUrl { get; set; }
        public string DetailsUrl { get; set; }

        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(Content))
                throw new InvalidOperationException("RagHydratedItem.Content is required.");
        }
    }
}
