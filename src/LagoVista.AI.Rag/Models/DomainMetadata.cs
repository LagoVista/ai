using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// Represents metadata extracted from a [DomainDescriptor] class.
    /// Structural issues during extraction are recorded in Errors so the
    /// orchestrator can treat them as failures without throwing exceptions.
    /// </summary>
    public class DomainMetadata
    {
        public string RepoId { get; set; }
        public string FullPath { get; set; }
        public string ClassName { get; set; }

        /// <summary>
        /// Normalized domain key (e.g. "AIAdmin"), extracted from the
        /// constant passed to the [DomainDescription] attribute.
        /// </summary>
        public string DomainKey { get; set; }

        /// <summary>
        /// DomainDescription.Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// DomainDescription.Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// All models/entities belonging to this domain for this run.
        /// </summary>
        public List<DomainEntitySummary> Entities { get; set; } = new List<DomainEntitySummary>();

        /// <summary>
        /// Structural issues detected during domain metadata extraction
        /// (e.g. multiple [DomainDescription] members, missing Name, etc.).
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        public bool HasErrors => Errors.Count > 0;
    }
}
