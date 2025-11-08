// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 04bc2a0ade915b4f5c7bbc31f752841642aba68d2bbbb497b35f677923314c63
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public class RagServices : IRagServices
    {
        OpenAIEmbedder _embedder;

        public RagServices(IAdminLogger adminLogger)
        {
            _embedder = new OpenAIEmbedder(adminLogger);
        }

        public Task<InvokeResult> AddTextContentAsync(IAIAgentContext agentContext, string path, string fileName, string content, string contentType)
        {
            throw new NotImplementedException();
        }

        public float[] GetEmbedingsAsync(IAIAgentContext agentContext, string text)
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
