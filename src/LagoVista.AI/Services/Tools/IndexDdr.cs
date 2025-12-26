using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Managers;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Stub tool: indexes an existing DDR into a RAG/vector database.
    /// You will wire the actual indexing implementation.
    /// </summary>
    public sealed class IndexDdrTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly ISourceFileProcessor _processor;
        private readonly IDdrManager _ddrManager;
        private readonly IOrganizationManager _orgManager;
        private readonly IQdrantClient _qdrantClient;
        private readonly IEmbedder _embedder;


        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "index_ddr";

        public const string ToolUsageMetadata =
            "Indexes an existing DDR into the RAG/vector database for semantic search and retrieval. " +
            "Call this tool only when the user explicitly asks to index a DDR, or after a successful import_ddr when the assistant has asked 'Would you like to index this DDR now?' and the user confirms yes. " +
            "Do not call this tool automatically without user consent. " +
            "Provide the DDR identifier (e.g., 'TUL-011'); the tool should index the stored canonical DDR content and return a small JSON result indicating success and any indexing details (such as document/key and chunk count) if available.";

        public IndexDdrTool(IEmbedder embeder, IQdrantClient qdrantClient, IOrganizationManager orgManager, IDdrManager ddrManager, IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
            _embedder = embeder ?? throw new ArgumentNullException(nameof(embeder));
            _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
            _orgManager = orgManager ?? throw new ArgumentNullException(nameof(orgManager));
        }

        private sealed class IndexDdrArgs
        {
            public string Identifier { get; set; }
        }

        private sealed class IndexDdrResult
        {
            public bool Success { get; set; }
            public string Identifier { get; set; }

            // Stub fields you can later populate from the real indexer
            public string VectorDocumentId { get; set; }
            public int? ChunkCount { get; set; }

            public string SessionId { get; set; }
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("index_ddr requires a non-empty arguments object.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<IndexDdrArgs>(argumentsJson) ?? new IndexDdrArgs();

                if (string.IsNullOrWhiteSpace(args.Identifier))
                {
                    return InvokeResult<string>.FromError("index_ddr requires 'identifier'.");
                }

                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(args.Identifier, context.Org, context.User, false);
                if(ddr == null)
                    return InvokeResult<string>.FromError($"index_ddr - could not find DDR {args.Identifier} to process.");

                if (String.IsNullOrEmpty(ddr.RagIndexCard))
                    return InvokeResult<string>.FromError("index_ddr - Summary is empty, can not index.");

                var ctx = new IndexFileContext() {
                    Contents = System.Text.UTF32Encoding.UTF32.GetBytes(ddr.FullDDRMarkDown),
                    FullPath = $"/{context.Org.Id.ToLower()}/{args.Identifier}.md",
                    RelativePath = $"/{context.Org.Id.ToLower()}/{args.Identifier}.md",
                    DocumentIdentity = new DocumentIdentity()
                    {
                         OrgId = context.Org.Id,
                         OrgNamespace = await _orgManager.GetOrgNameSpaceAsync(context.Org.Id),
                         DocId = ddr.Id,
                    },
                    GitRepoInfo = new GitRepoInfo()
                    {
                        RemoteUrl = "ddr-repo",
                        BranchRef = "main",
                    },
                    RepoId = "ddr-repo",
                };

                var ddrDescription = DdrDescriptionBuilder.FromSource(ctx, ddr.FullDDRMarkDown);

                if (!ddrDescription.Successful)
                {
                    return InvokeResult<string>.FromError($"index_ddr - DDR description build failed: {ddrDescription.ErrorMessage}");
                }


                var vector = await _embedder.EmbedAsync(ddr.RagIndexCard);

                var point = new RagPoint()
                {
                    PointId = Guid.NewGuid().ToString(),
                    Vector = vector.Result.Vector,
                    Payload = new RagVectorPayload()
                    {
                        DocId = ddr.Id,
                        OrgNamespace = ctx.DocumentIdentity.OrgNamespace,
                        Repo = ctx.GitRepoInfo.RemoteUrl,
                        RepoBranch = ctx.GitRepoInfo.BranchRef,
                        CommitSha = "n/a",
                        Title = $"{ddr.DdrIdentifier} - {ddr.Name}",
                        SectionKey = "DDR_Summary",
                        EmbeddingModel = vector.Result.EmbeddingModel,
                        BusinessDomainKey = "General",
                        ContentTypeId = RagContentType.Spec,
                        Subtype = "ddr",
                        SubtypeFlavor = "Default",
                        Language = "en-US",
                    }
                };

                var payload = new IndexDdrResult
                {
                    Success = true,
                    Identifier = args.Identifier.Trim(),
                    VectorDocumentId = point.PointId,
                    ChunkCount = 1,
                    SessionId = context?.SessionId
                };

                await _qdrantClient.UpsertAsync(context.AgentContext.VectorDatabaseCollectionName, new[] { point }, cancellationToken);

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[IndexDdrTool_ExecuteAsync__Exception]", ex);

                return InvokeResult<string>.FromError($"index_ddr failed to process arguments {ex.Message}");
            }
        }

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Indexes an existing DDR into the RAG/vector database for semantic retrieval.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        identifier = new
                        {
                            type = "string",
                            description = "DDR identifier to index (e.g., 'TUL-011')."
                        }
                    },
                    required = new[] { "identifier" }
                }
            };

            return schema;
        }
    }
}