using LagoVista.AI.Interfaces;
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
using LagoVista.Core.Validation;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Services
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

        public RagIndexingService(IEntityIndexDocumentBuilder documentBuilder, IEmbedder embedder, IAgentContextLoaderRepo agentContextLoaderRepo, IBackgroundServiceTaskQueue taskService, 
                           IQdrantClient vectorDbClient, IOrganizationLoaderRepo orgRepo, ILLMContentRepo llmContentRepo) 
        {
            _documentBuilder = documentBuilder ?? throw new ArgumentNullException(nameof(documentBuilder));
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
            _documentBuilder = documentBuilder ?? throw new ArgumentNullException(nameof(documentBuilder));
            _llmContentRepo = llmContentRepo ?? throw new ArgumentNullException(nameof(llmContentRepo));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _vectorDbClient = vectorDbClient ?? throw new ArgumentNullException(nameof(vectorDbClient));
            _agentContextLoaderRepo = agentContextLoaderRepo ?? throw new ArgumentNullException(nameof(agentContextLoaderRepo));
        }


        public float[] GetEmbedingsAsync(IAIAgentContext agentContext, string text)
        {
            throw new NotImplementedException();
        }



        public async Task<InvokeResult> IndexAsync(IEntityBase entity)
        {
            var org = await _orgRepo.GetOrganizationAsync(entity.OwnerOrganization.Id);
            if(EntityHeader.IsNullOrEmpty(org.DefaultAgentContext))
                return InvokeResult.FromError("Organization does not have a default agent context defined, content can not be indexed.");

            var agentContext = await _agentContextLoaderRepo.GetAgentContextAsync(org.DefaultAgentContext.Id);
            
            await _taskService.QueueBackgroundWorkItemAsync(async (token) =>
            {
                var entityType = entity.GetType();
                var attr = entityType.GetTypeInfo().GetCustomAttributes<EntityDescriptionAttribute>().FirstOrDefault();
                var entityDescription = EntityDescription.Create(entityType, attr);


                if (entity is IRagableEntity)
                {
                    
                }
                else
                {
                    var lens = await _documentBuilder.BuildAsync(entity);
                    var vectors = await _embedder.EmbedAsync(lens.Lenses.EmbedSnippet);

                    var modelFileName = $"{entity.Id}.cleanup.json";
                    var userFileName = $"{entity.Id}.user.json";
                    var cleanUpFileName = $"{entity.Id}.cleanup.json";

                    var modelSummaryUrl = await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, modelFileName, content: lens.Lenses.ModelSummary, contentType: "application/json");
                    var userDetails = await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, fileName: userFileName, content: lens.Lenses.UserDetail, contentType: "application/json");
                    if(!String.IsNullOrEmpty(lens.Lenses.CleanupGuidance))  
                        await _llmContentRepo.AddTextContentAsync(agentContext, path: entity.EntityType, fileName: cleanUpFileName, content: lens.Lenses.UserDetail, contentType: "application/json");

                    var point = new RagPoint();
                    point.PointId = entity.Id.ToGuidString();
                    point.Payload.Meta.DocId = entity.Id;
                    point.Payload.Meta.Title = entity.Name;
                    point.Payload.Extra.EditorUrl = entityDescription.EditUIUrl;
                    point.Payload.Meta.SectionKey = "main";
                    point.Payload.Meta.PartIndex = 1;
                    point.Payload.Meta.PartTotal = 1;
                    point.Payload.Meta.SemanticId = $"{entityDescription.DomainName}.{entityType.Name}.{entity.Id}";
                    point.Payload.Meta.EmbeddingModel = agentContext.EmbeddingModel;
                    point.Payload.Meta.ContentTypeId = Core.Utils.Types.Nuviot.RagIndexing.RagContentType.DomainDocument;
                    point.Payload.Meta.Subtype = entity.EntityType;
                    point.Payload.Meta.ProjectId = "default";
                    point.Payload.Meta.OrgNamespace = entity.OwnerOrganization.Id;
                    point.Payload.Extra.Path = entity.EntityType;
                    point.Payload.Extra.ModelContentFileName = modelFileName;
                    point.Payload.Extra.HumanContentFileName = userFileName;
                    if(!String.IsNullOrEmpty(lens.Lenses.CleanupGuidance))
                        point.Payload.Extra.IssuesFileName = cleanUpFileName;

                    point.Payload.Meta.HasIssues = !String.IsNullOrEmpty(lens.Lenses.CleanupGuidance);
                    point.Vector = vectors.Result.Vector;

                    await _vectorDbClient.UpsertAsync(agentContext.VectorDatabaseCollectionName, new List<IRagPoint>() { point }, CancellationToken.None);

                }
            });


            throw new NotImplementedException();
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
