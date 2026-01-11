using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.PipeLine
{
    /// <summary>
    /// Base class for indexing pipeline steps.
    ///
    /// Contract:
    /// - ExecuteAsync(ctx) runs once per step.
    /// - Base class iterates ctx.WorkItems and calls ExecuteAsync(ctx, workItem).
    /// - Steps return InvokeResult.
    /// - Steps may expand work items (e.g., segmentation) by adding new items to the context.
    ///
    /// Notes:
    /// - Logging/validation hooks can be added later (kept minimal for now).
    /// </summary>
    public abstract class IndexingPipelineStepBase : IIndexingPipelineStep
    {
        private readonly IIndexingPipelineStep _next;

        protected IndexingPipelineStepBase(IIndexingPipelineStep next = null)
        {
            _next = next;
        }

        public abstract IndexingPipelineSteps StepType { get; }

        public async Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (ctx == null) throw new ArgumentNullException(nameof(ctx));

                // Snapshot iteration to allow steps to add work items during execution.
                // Newly added items are NOT processed by this step unless the step itself does so explicitly.
                var items = ctx.WorkItems;
                var count = items.Count;

                for (var i = 0; i < count; i++)
                {
                    if (ctx.IsTerminal) return InvokeResult.Success;

                    var item = items[i];
                    var result = await ExecuteAsync(ctx, item);
                    if (!result.Successful) return result;
                }

                if (ctx.IsTerminal || _next == null) return InvokeResult.Success;

                return await _next.ExecuteAsync(ctx);
            }
            catch (Exception ex)
            {
                return InvokeResult.FromError(ex.Message, ex.GetType().ToString());
            }
            finally
            {
                sw.Stop();
            }
        }

        protected abstract Task<InvokeResult> ExecuteAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem);
    }
}
