using LagoVista.AI.Interfaces.Services;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Validation;
using LagoVista.UserAdmin.Interfaces.Managers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Services
{
    public class RagServices : IRagIndexingServices
    {
        private readonly IEntityIndexDocumentBuilder _documentBuilder;
        private readonly IEmbedder _embedder;
        private readonly ILLMContentRepo _llmContentRepo;
        private readonly IOrganizationManager _orgManager;

        public RagServices(IEntityIndexDocumentBuilder documentBuilder, IEmbedder embedder, IOrganizationManager orgManager) 
        { 
        
        }

        public Task<InvokeResult> AddTextContentAsync(IAIAgentContext agentContext, string path, string fileName, string content, string contentType)
        {
            throw new NotImplementedException();
        }

        public float[] GetEmbedingsAsync(IAIAgentContext agentContext, string text)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult> IndexAsync(IRagableEntity ragableEntity)
        {
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
