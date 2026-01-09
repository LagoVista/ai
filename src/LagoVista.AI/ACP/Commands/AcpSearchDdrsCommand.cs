using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;

namespace LagoVista.AI.ACP.Commands
{
    /// <summary>
    /// V1 RAG command per AGN-000038.
    /// Trigger form: "search ddrs for &lt;TOPIC&gt;"
    ///
    /// Flow:
    /// - parse topic
    /// - embed via IEmbedder
    /// - search Qdrant (payload index-cards)
    /// - hydrate payloads into Content/SummaryUrl/DetailsUrl
    /// - write combined block to PromptKnowledgeProvider Rag register
    /// </summary>
    [AcpCommand("acp.rag.search_ddrs", "Search DDRs", "Runs a RAG search over DDR content and attaches results to the Rag knowledge register.")]
    [AcpTriggers("search ddrs for")]
    [AcpArgs(1, 999)]
    public sealed class AcpSearchDdrsCommand : IAcpCommand
    {
        private const int TopK = 10;

        private readonly IEmbedder _embedder;
        private readonly IRagContextBuilder _ragContextBuilder;

        /// <summary>
        /// Placeholder until wired to configuration.
        /// </summary>
        public string CollectionName { get; set; } = "TODO_DDR_COLLECTION";

        public AcpSearchDdrsCommand(IEmbedder embedder)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        }

        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext context, string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var topic = args == null ? null : String.Join(" ", args).Trim();
            if (String.IsNullOrWhiteSpace(topic))
                return InvokeResult<IAgentPipelineContext>.FromError("Topic is required. Usage: search ddrs for <TOPIC>");

            // Optional: scope filter may be present on the request payload.
            // TODO: Replace with your actual request shape/property.
            object ragScope = null;
            try
            {
                ragScope = context.Envelope.RagScope;
            }
            catch
            {
                // ignore; scope is optional
            }

            var embedResult = await _embedder.EmbedAsync(topic);
            if (embedResult == null || embedResult.Result == null || embedResult.Result.Vector == null)
                return InvokeResult<IAgentPipelineContext>.FromError("Failed to embedfs query text.");

            return await _ragContextBuilder.BuildContextSectionAsync(context, topic);
        }
    }
}
