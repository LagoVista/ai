using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Interfaces
{
    /// <summary>
    /// Abstraction that extracts model (entity) metadata from the provided
    /// discovered files and resource dictionaries. Concrete implementations
    /// can use Roslyn, reflection, or existing description builders.
    /// </summary>
    public interface IModelMetadataSource
    {
        Task<IReadOnlyList<ModelMetadata>> GetModelsAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Summary of a single important field/property on a first-class model,
    /// derived from [FormField] attributes.
    /// </summary>
    public class FieldSummary
    {
        public string PropertyName { get; set; }
        public string Label { get; set; }
        public string Help { get; set; }
    }

    /// <summary>
    /// Minimal metadata required by IDX-066 for a first-class entity.
    /// </summary>
    public class ModelMetadata
    {
        public string RepoId { get; set; }
        public string FullPath { get; set; }
        public string ClassName { get; set; }
        public string DomainKey { get; set; }

        public string TitleResourceKey { get; set; }
        public string DescriptionResourceKey { get; set; }
        public string HelpResourceKey { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Help { get; set; }

        public List<FieldSummary> Fields { get; set; } = new List<FieldSummary>();

        /// <summary>
        /// Structural issues detected during metadata extraction (e.g. missing
        /// LabelResource, unresolved title resource, malformed attributes, etc.).
        /// The orchestrator treats any non-empty error list as a failure for
        /// this model and skips LLM refinement.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        public bool HasErrors => Errors.Count > 0;
    }
}
