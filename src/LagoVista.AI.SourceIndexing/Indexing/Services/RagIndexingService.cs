using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using LagoVista.UserAdmin.Models.Orgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static LagoVista.Core.Models.AdaptiveCard.MSTeams;

namespace LagoVista.AI.Indexing.Services
{

    public class RagIndexingService : IRagIndexingServices
    {
        private readonly IEntityIndexDocumentBuilder _documentBuilder;
        private readonly IEmbedder _embedder;
        private readonly ILLMContentRepo _llmContentRepo;
        private readonly IOrganizationLoaderRepo _orgRepo;
        private readonly IAgentContextLoaderRepo _agentContextLoaderRepo;
        private readonly IBackgroundServiceTaskQueue _taskService;
        private readonly IQdrantClient _vectorDbClient;
        private readonly IAdminLogger _adminLogger;

        public RagIndexingService(IEntityIndexDocumentBuilder documentBuilder, IEmbedder embedder, IAgentContextLoaderRepo agentContextLoaderRepo, IBackgroundServiceTaskQueue taskService,
                                  IAdminLogger adminLogger, IQdrantClient vectorDbClient, IOrganizationLoaderRepo orgRepo, ILLMContentRepo llmContentRepo)
        {
            _documentBuilder = documentBuilder ?? throw new ArgumentNullException(nameof(documentBuilder));
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
            _documentBuilder = documentBuilder ?? throw new ArgumentNullException(nameof(documentBuilder));
            _llmContentRepo = llmContentRepo ?? throw new ArgumentNullException(nameof(llmContentRepo));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _vectorDbClient = vectorDbClient ?? throw new ArgumentNullException(nameof(vectorDbClient));
            _agentContextLoaderRepo = agentContextLoaderRepo ?? throw new ArgumentNullException(nameof(agentContextLoaderRepo));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public float[] GetEmbedingsAsync(IAIAgentContext agentContext, string text)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult> IndexAsync(IEntityBase entity)
        {
            Organization org = entity as Organization;
            if (org == null && EntityHeader.IsNullOrEmpty(entity.OwnerOrganization))
            {
                _adminLogger.AddCustomEvent(Core.PlatformSupport.LogLevel.Warning, this.Tag(), $"Could not identify org for {entity.GetType().Name} with Id: {entity.Id}, can not index entity.");
                return InvokeResult.Success;
            }
            
            if(org == null)
                org = await _orgRepo.GetOrganizationAsync(entity.OwnerOrganization.Id);

            if (org == null)
            {
                return InvokeResult.Success;
            }

            if (EntityHeader.IsNullOrEmpty(org.DefaultAgentContext))
            {
                _adminLogger.AddCustomEvent(Core.PlatformSupport.LogLevel.Warning, this.Tag(), $"Organization {org.Name} ({org.Id}) does not have a default agent context defined, content can not be indexed.");
                return InvokeResult.Success;
            }

            _adminLogger.Trace($"{this.Tag()} Queue index for {entity.Name} ({entity.EntityType})");

            await _taskService.QueueBackgroundWorkItemAsync(async (token) =>
            {
                var sw = Stopwatch.StartNew();

                var agentContext = await _agentContextLoaderRepo.GetAgentContextAsync(org.DefaultAgentContext.Id);

                var entityType = entity.GetType();
                var attr = entityType.GetTypeInfo().GetCustomAttributes<EntityDescriptionAttribute>().FirstOrDefault();
                var entityDescription = EntityDescription.Create(entityType, attr);


                var ragEntity = entity as IRagableEntity;
                if (ragEntity != null)
                {
                    _adminLogger.Trace($"{this.Tag()} Background task startd for indexing raggable entity {entity.Name} ({entity.EntityType})");

                    var points = new List<RagPoint>();

                    var ragContentItems = await ragEntity.GetRagContentAsync();
                    _adminLogger.Trace($"{this.Tag()} Found {ragContentItems.Count} points for raggable entity {entity.Name} ({entity.EntityType})");

                    foreach (var ragContent in ragContentItems)
                    {
                        var point = new RagPoint();
                        point.PointId = Guid.NewGuid().ToString();
                        point.Payload = ragContent.Payload;
                        var modelFileName = ragContent.Payload.Meta.DocId == entity.Id ? $"{entity.Id}.model.json" : $"{entity.Id}.{ragContent.Payload.Meta.DocId}.model.json";
                        var userFileName = ragContent.Payload.Meta.DocId == entity.Id ? $"{entity.Id}.user.json" : $"{entity.Id}.{ragContent.Payload.Meta.DocId}.user.json";

                        var modelSummaryUrl = await _llmContentRepo.AddContentAsync(org.Namespace, modelFileName, ragContent.ModelDescription);
                        var userDetails = await _llmContentRepo.AddContentAsync(org.Namespace, userFileName, ragContent.HumanDescription);

                        point.Payload.Extra.ModelContentUrl = modelSummaryUrl.Result.ToString();
                        point.Payload.Extra.HumanContentUrl = userDetails.Result.ToString();
                        point.Payload.Extra.Path = entity.EntityType;
                        point.Payload.Meta.EmbeddingModel = agentContext.EmbeddingModel;
                        point.Payload.Meta.OrgNamespace = org.Namespace;
                        point.Payload.Meta.Deleted = entity.IsDeleted.HasValue && entity.IsDeleted.Value;

                        if (string.IsNullOrEmpty(point.Payload.Extra.EditorUrl))
                            point.Payload.Extra.EditorUrl = entityDescription.EditUIUrl;

                        if (string.IsNullOrEmpty(point.Payload.Extra.PreviewUrl))
                            point.Payload.Extra.PreviewUrl = entityDescription.PreviewUIUrl;

                        if (!string.IsNullOrEmpty(ragContent.Issues))
                        {
                            var issuesFilename = ragContent.Payload.Meta.DocId == entity.Id ? $"{entity.Id}.issues.json" : $"{entity.Id}.{ragContent.Payload.Meta.DocId}.issues.json";
                            await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, fileName: issuesFilename, content: ragContent.Issues, contentType: "application/json");
                            point.Payload.Extra.IssuesContentUrl = issuesFilename;
                            point.Payload.Meta.HasIssues = true;
                        }
                        var validationResult = point.Payload.ValidateForIndex();
                        if (validationResult.Successful)
                        {
                            var vector = await _embedder.EmbedAsync(ragContent.EmbeddingContent);
                            point.Vector = vector.Result.Vector;
                            point.Payload.Meta.EmbeddingModel = vector.Result.EmbeddingModel;
                            points.Add(point);
                        }
                        else
                        {
                            _adminLogger.AddError(this.Tag(), $"Validation failed for entity {entity.Name} ({entity.EntityType}): {validationResult.Errors}");
                        }

                    }

                    _adminLogger.Trace($"{this.Tag()} Added {ragContentItems.Count} points for raggable entity {entity.Name} ({entity.EntityType})");

                    await _vectorDbClient.DeleteByDocIdsAsync(agentContext.VectorDatabaseCollectionName, points.Select(pt => pt.Payload.Meta.DocId), CancellationToken.None);
                    await _vectorDbClient.UpsertAsync(agentContext.VectorDatabaseCollectionName, points.Cast<IRagPoint>().ToList(), CancellationToken.None);


                    _adminLogger.Trace($"{this.Tag()} Completed {ragContentItems.Count} and indexed points for raggable entity {entity.Name} ({entity.EntityType}) in {sw.Elapsed.TotalMilliseconds}ms (in background task)");
                }
                else
                {
                    _adminLogger.Trace($"{this.Tag()} Background task startd for indexing non-raggable entity {entity.Name} ({entity.EntityType})");

                    var lensResult = await _documentBuilder.BuildAsync(entity);
                    if (!lensResult.Successful)
                    {
                        _adminLogger.AddError(this.Tag(), $"Failed to build document for entity {entity.Name} ({entity.EntityType}): {lensResult.ErrorMessage}");
                        return;
                    }

                    var lens = lensResult.Result;

                    if (string.IsNullOrEmpty(lens.Lenses.EmbedSnippet))
                    {
                        _adminLogger.AddError(this.Tag(), $"No content to embed for entity {entity.Name} ({entity.EntityType})");
                        return;
                    }

                    var vectors = await _embedder.EmbedAsync(lens.Lenses.EmbedSnippet);
                    if (!vectors.Successful)
                    {
                        _adminLogger.AddError(this.Tag(), $"Failed to embed snippet {entity.Name} ({entity.EntityType}): {lensResult.ErrorMessage}",
                            lens.Lenses.EmbedSnippet.Length.ToString().ToKVP("len"),
                            lens.Lenses.EmbedSnippet.Replace("\r", "\\r").Replace("\n", "\\n").ToKVP("embedding"));
                        return;
                    }

                    _adminLogger.Trace($"{this.Tag()} Created Embedding {entity.Name} ({entity.EntityType})");

                    var modelFileName = $"{entity.Id}.model.json";
                    var userFileName = $"{entity.Id}.user.json";
                    var cleanUpFileName = $"{entity.Id}.issues.json";

                    await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, modelFileName, content: lens.Lenses.ModelSummary, contentType: "application/json");
                    await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, fileName: userFileName, content: lens.Lenses.UserDetail, contentType: "application/json");
                    if (!string.IsNullOrEmpty(lens.Lenses.CleanupGuidance))
                        await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, fileName: cleanUpFileName, content: lens.Lenses.UserDetail, contentType: "application/json");

                    var point = new RagPoint();
                    point.Payload = RagVectorPayload.FromEntity(entity);
                    point.PointId = entity.Id.ToGuidString();
                    point.Payload.Extra.Path = entity.EntityType;
                    point.Payload.Meta.OrgNamespace = org.Namespace;
                    point.Payload.Extra.ModelContentUrl = modelFileName;
                    point.Payload.Extra.HumanContentUrl = userFileName;
                    if (!string.IsNullOrEmpty(lens.Lenses.CleanupGuidance))
                        point.Payload.Extra.IssuesContentUrl = cleanUpFileName;

                    point.Payload.Meta.HasIssues = !string.IsNullOrEmpty(lens.Lenses.CleanupGuidance);
                    point.Vector = vectors.Result.Vector;

                    point.Payload.Meta.EmbeddingModel = vectors.Result.EmbeddingModel;

                    _adminLogger.Trace($"{this.Tag()} Populated Rag Point {entity.Name} ({entity.EntityType})");

                    var validationReslut = point.Payload.ValidateForIndex();
                    if (validationReslut.Successful)
                    {
                        _adminLogger.Trace($"{this.Tag()} Successfl validation, adding point {entity.Name} ({entity.EntityType})");
                        await _vectorDbClient.DeleteByDocIdAsync(agentContext.VectorDatabaseCollectionName, point.Payload.Meta.DocId, CancellationToken.None);
                        await _vectorDbClient.UpsertAsync(agentContext.VectorDatabaseCollectionName, new List<IRagPoint>() { point }, CancellationToken.None);
                        _adminLogger.Trace($"{this.Tag()} Completed indexing non-raggable entity {entity.Name} ({entity.EntityType}) in {sw.Elapsed.TotalMilliseconds}ms (in background task)");
                    }
                    else
                    {
                        _adminLogger.AddError(this.Tag(), $"Validation failed for entity {entity.Name} ({entity.EntityType}): {validationReslut.Errors}");
                    }
                }
            });

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> RemoveIndexAsync(string orgId, string docId)
        {
            var org = await _orgRepo.GetOrganizationAsync(orgId);
            if (EntityHeader.IsNullOrEmpty(org.DefaultAgentContext))
                return InvokeResult.FromError("Organization does not have a default agent context defined, content can not be indexed.");

            await _taskService.QueueBackgroundWorkItemAsync(async (token) =>
            {
                var agentContext = await _agentContextLoaderRepo.GetAgentContextAsync(org.DefaultAgentContext.Id);
                await _vectorDbClient.DeleteByDocIdAsync(agentContext.VectorDatabaseCollectionName, docId, token);
            });

            return InvokeResult.Success;
        }

        public Task RemoveStaleVectorsAsync(IAIAgentContext agentContext, string docId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task UpsertInBatchesAsync(IAIAgentContext agentContext, IReadOnlyList<PayloadBuildResult> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
