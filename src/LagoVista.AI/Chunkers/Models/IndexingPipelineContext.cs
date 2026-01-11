using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Models
{
    public sealed class IndexingPipelineContext
    {
        private readonly List<IndexingWorkItem> _workItems = new List<IndexingWorkItem>();

        public IndexingPipelineContext()
        {
            Resources = new IndexingResources();
        }

        public string FullSource { get; set; }

        public string FullSourceUrl { get; set; }

        public IndexingResources Resources { get; }

        public IReadOnlyList<IndexingWorkItem> WorkItems => _workItems;

        public bool IsTerminal { get; set; }

        /// <summary>
        /// Adds an initial work item (e.g., seed item) to the pipeline.
        /// </summary>
        public void AddWorkItem(IndexingWorkItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _workItems.Add(item);
        }

        /// <summary>
        /// Clone-and-add helper intended for SegmentContent expansion.
        /// NOTE: Deep copy semantics are required; implementation will be finalized once RagPayload/Lenses types are concrete.
        /// </summary>
        public IndexingWorkItem CloneAndAddChild(IndexingWorkItem parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            // Deep copy TODO:
            // - Once RagPayload is RagVectorPayload, deep clone it and set ParentPointId = parent.PointId.
            // - Once Lenses is EntityIndexLenses, deep clone it.

            var child = new IndexingWorkItem
            {
                PointId = Guid.NewGuid(),
                Vector = null,
                RagPayload = parent.RagPayload,
                Lenses = parent.Lenses
            };

            _workItems.Add(child);
            return child;
        }
    }
}
