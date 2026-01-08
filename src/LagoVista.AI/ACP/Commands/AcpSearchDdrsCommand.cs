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

        private readonly LagoVista.AI.Rag.IRagIndexSearcher _indexSearcher;
        private readonly LagoVista.AI.Rag.IRagPayloadHydrator _payloadHydrator;
        private readonly LagoVista.AI.Rag.IRagRegisterWriter _registerWriter;
        private readonly IEmbedder _embedder;

        /// <summary>
        /// Placeholder until wired to configuration.
        /// </summary>
        public string CollectionName { get; set; } = "TODO_DDR_COLLECTION";

        public AcpSearchDdrsCommand(
            LagoVista.AI.Rag.IRagIndexSearcher indexSearcher,
            LagoVista.AI.Rag.IRagPayloadHydrator payloadHydrator,
            LagoVista.AI.Rag.IRagRegisterWriter registerWriter,
            IEmbedder embedder)
        {
            _indexSearcher = indexSearcher ?? throw new ArgumentNullException(nameof(indexSearcher));
            _payloadHydrator = payloadHydrator ?? throw new ArgumentNullException(nameof(payloadHydrator));
            _registerWriter = registerWriter ?? throw new ArgumentNullException(nameof(registerWriter));
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
                dynamic ctx = context;
                ragScope = ctx?.Request?.RagScope;
            }
            catch
            {
                // ignore; scope is optional
            }

            var embedResult = await _embedder.EmbedAsync(topic);
            if (embedResult == null || embedResult.Result == null || embedResult.Result.Vector == null)
                return InvokeResult<IAgentPipelineContext>.FromError("Failed to embed query text.");

            var scoredPoints = await _indexSearcher.SearchAsync(CollectionName, embedResult.Result.Vector, TopK, ragScope);

            // Extract payload dictionaries from scored points.
            var payloads = new List<Dictionary<string, object>>();
            foreach (var sp in scoredPoints ?? Enumerable.Empty<object>())
            {
                if (sp == null) continue;
                try
                {
                    dynamic d = sp;
                    Dictionary<string, object> payload = d.Payload;
                    if (payload != null)
                        payloads.Add(payload);
                }
                catch
                {
                    // ignore malformed entries
                }
            }

            var hydrated = await _payloadHydrator.HydrateAsync(payloads);

            _registerWriter.WriteToRagRegister(context, hydrated);

            // Non-terminal: allow pipeline to continue to model.
            return InvokeResult<IAgentPipelineContext>.Create(context);
        }
    }
}
