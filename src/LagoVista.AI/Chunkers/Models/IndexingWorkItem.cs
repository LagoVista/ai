using System;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;

namespace LagoVista.AI.Indexing.Models
{
    public sealed class IndexingWorkItem
    {
        public Guid PointId { get; set; }

        public float[] Vector { get; set; }

        /// <summary>
        /// Strong type will be wired up once RagVectorPayload is moved into a stable reference.
        /// </summary>
        public RagVectorPayload RagPayload { get; set; }

        /// <summary>
        /// Placeholder for now; you indicated EntityIndexLenses already exists elsewhere.
        /// </summary>
        public EntityIndexLenses Lenses { get; set; }
    }
}
