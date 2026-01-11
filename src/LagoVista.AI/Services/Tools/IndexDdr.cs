using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Models;
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
        private readonly ILLMContentRepo _llmContentRepo;
        private readonly IAdminLogger _logger;
        private readonly IDdrManager _ddrManager;
        private readonly IOrganizationManager _orgManager;
        private readonly IQdrantClient _qdrantClient;
        private readonly IEmbedder _embedder;
        public const string ToolSummary = "used to index a ddr into vector database for RAG";
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "index_ddr";
        public const string ToolUsageMetadata =
@"Indexes an existing DDR into the RAG/vector database for semantic search and retrieval.
Call this tool only when the user explicitly asks to index a DDR, or after a successful import_ddr when the assistant has asked “Would you like to index this DDR now?” and the user confirms yes. Do not call this tool automatically without user consent.
Provide a DDR identifier matching ^[A-Za-z]{3}-\d{1,6}$ (e.g., TUL-000011). 
The tool will normalize the identifier by uppercasing the 3-letter prefix and left-padding the numeric portion to 6 digits (e.g., tul-01 or TuL-1 -> TUL-000001). 
The tool indexes the stored canonical DDR content for the normalized identifier.
Return a small JSON result indicating success and any indexing details available (e.g., document/key and chunk count).";
        public IndexDdrTool(IEmbedder embeder, ILLMContentRepo llmContentRepo, IQdrantClient qdrantClient, IOrganizationManager orgManager, IDdrManager ddrManager, IAdminLogger logger)
        {
            _llmContentRepo = llmContentRepo ?? throw new ArgumentNullException(nameof(llmContentRepo));  
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
            public string PointId { get; set; }
            public string DocId { get; set; }
            public int? ChunkCount { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
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

                var ddrId = args.Identifier.NormalizeDdrid();

                var ddr = await _ddrManager.GetDdrByTlaIdentiferAsync(ddrId, context.Org, context.User, false);
                if (ddr == null)
                    return InvokeResult<string>.FromError($"index_ddr - could not find DDR {args.Identifier} to process.");
                if (String.IsNullOrEmpty(ddr.RagIndexCard))
                    return InvokeResult<string>.FromError("index_ddr - Summary is empty, can not index.");
                var ctx = new IndexFileContext()
                {
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

                var addContentResult = await _llmContentRepo.AddContentAsync(ctx.DocumentIdentity.OrgNamespace, $"ddrs/{ddr.DdrIdentifier.Replace("-",String.Empty).ToLower()}.model.txt", ddr.CondensedDdrContent);
                if(!addContentResult.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(addContentResult.ToInvokeResult());
                }

                var humanContentResult = await _llmContentRepo.AddContentAsync(ctx.DocumentIdentity.OrgNamespace, $"ddrs/{ddr.DdrIdentifier.Replace("-", String.Empty).ToLower()}.human.txt", ddr.CondensedDdrContent);
                if (!humanContentResult.Successful)
                {
                    return InvokeResult<string>.FromInvokeResult(humanContentResult.ToInvokeResult());   
                }

                var vector = await _embedder.EmbedAsync(ddr.RagIndexCard);
                var point = new RagPoint()
                {
                    PointId = Guid.NewGuid().ToString(),
                    Vector = vector.Result.Vector,
                    Payload = new RagVectorPayload()
                    {
                        Meta = new RagVectorPayloadMeta()
                        {
                            DocId = ddr.Id,
                            OrgNamespace = ctx.DocumentIdentity.OrgNamespace,
                            OrgId = ctx.DocumentIdentity.OrgId,
                            Title = $"{ddr.DdrIdentifier} - {ddr.Name}",
                            SectionKey = "RagIndexCard",
                            EmbeddingModel = vector.Result.EmbeddingModel,
                            BusinessDomainKey = "General",
                            ContentTypeId = RagContentType.Spec,
                            Subtype = "ddr",
                            SubtypeFlavor = "Default",
                            Language = "en-US",

                        },
                        Extra = new RagVectorPayloadExtra()
                        {
                            Repo = ctx.GitRepoInfo.RemoteUrl,
                            RepoBranch = ctx.GitRepoInfo.BranchRef,
                            CommitSha = "n/a",
                            FullDocumentBlobUri = addContentResult.Result.ToString(),
                            HumanContentUrl = humanContentResult.Result.ToString()
                        }
                    }
                };
                var payload = new IndexDdrResult
                {
                    Success = true,
                    Identifier = args.Identifier.Trim(),
                    PointId = point.PointId,
                    DocId = point.Payload.Meta.DocId,
                    ChunkCount = 1,
                };

                await _qdrantClient.DeleteByDocIdAsync(context.AgentContext.VectorDatabaseCollectionName, point.Payload.Meta.DocId);
                await _qdrantClient.UpsertAsync(context.AgentContext.VectorDatabaseCollectionName, new[] { point }, cancellationToken);
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                _logger.AddException("[IndexDdrTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError($"index_ddr failed to process arguments {ex.Message}");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Indexes an existing DDR into the RAG/vector database for semantic retrieval.", p =>
            {
                p.String("identifier", "DDR identifier to index (e.g., 'TUL-011').", required: true);
            });
        }
    }
}