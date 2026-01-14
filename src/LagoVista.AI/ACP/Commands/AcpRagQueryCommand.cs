using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

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
    [AcpCommand("acp.rag.search", "perform generic rag search \\qt (Query Text)", "Runs a RAG search over all RAG content.")]
    [AcpTriggers("\\qt")]
    [AcpArgs(1, 999)]
    public sealed class AcpRagQueryCommand : IAcpCommand
    {
        private const int TopK = 10;

        private readonly IRagContextBuilder _ragContextBuilder;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IAdminLogger _adminLogger;

        /// <summary>
        /// Placeholder until wired to configuration.
        /// </summary>
        public string CollectionName { get; set; } = "TODO_DDR_COLLECTION";

        public AcpRagQueryCommand(IRagContextBuilder ragContextBuilder, IAgentStreamingContext agentStreamingContext, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _ragContextBuilder = ragContextBuilder ?? throw new ArgumentNullException(nameof(ragContextBuilder));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext context, string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var topic = args == null ? null : String.Join(" ", args).Trim();
            if (String.IsNullOrWhiteSpace(topic))
                return InvokeResult<IAgentPipelineContext>.FromError("Topic is required. Usage: \\qt <TOPIC>");

            _adminLogger.Trace($"{this.Tag()} execute rag query with arg: [{(args == null ? "" : String.Join(", ", args))}]");

            // Optional: scope filter may be present on the request payload.
            // TODO: Replace with your actual request shape/property.
            //object ragScope = null;
            //try
            //{
            //    ragScope = context.Envelope.RagScope;
            //}
            //catch
            //{
            //    // ignore; scope is optional
            //}

            var sw = Stopwatch.StartNew();

            await _agentStreamingContext.AddMilestoneAsync("Consulting RAG knowledge base...");

            _adminLogger.Trace($"{this.Tag()} created embedding for: [{(args == null ? "" : String.Join(", ", args))}] in {sw.Elapsed.TotalMilliseconds}ms.");

            var buildResult = await _ragContextBuilder.BuildContextSectionAsync(context, topic);
         

            return buildResult; 
        }
    }
}
