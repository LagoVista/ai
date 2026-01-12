using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    public sealed class UploadContentStep : IndexingPipelineStepBase, IUploadContentStep
    {
        private readonly ILLMContentRepo _llmContentRepo;

        public UploadContentStep(IEmbedStep next, ILLMContentRepo contentRepo) : base(next) { 
            _llmContentRepo = contentRepo ?? throw new ArgumentNullException(nameof(contentRepo));
        }

        public override IndexingPipelineSteps StepType => IndexingPipelineSteps.UploadContent;

        protected override async Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {

           var fileAddResult = await _llmContentRepo.AddContentAsync(ctx.Resources.FileContext.DocumentIdentity.OrgNamespace, ctx.Resources.FileContext.RelativePath, ctx.Resources.FileContext.Contents);
            if (!fileAddResult.Successful) return fileAddResult.ToInvokeResult();

            ctx.Resources.FileContext.BlobUri = fileAddResult.Result.ToString();

            return InvokeResult.Success;
        }
    }
}
