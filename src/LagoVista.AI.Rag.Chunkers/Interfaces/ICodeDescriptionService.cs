using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    /// <summary>
    /// Facade interface that wraps the various description builders
    /// (Models, Managers, Repositories, Interfaces, Controllers/Endpoints).
    ///
    /// This provides a single DI-friendly entry point for code description
    /// operations, while the underlying builders remain focused and static.
    /// </summary>
    public interface ICodeDescriptionService
    {
        /// <summary>
        /// Build a semantic structure description (IDX-0037) of a Model class
        /// from its C# source.
        /// </summary>
        /// <param name="sourceText">Full C# source text for the model file.</param>
        /// <returns>A populated <see cref="ModelStructureDescription"/>.</returns>
        ModelStructureDescription BuildModelStructureDescription(string sourceText, IReadOnlyDictionary<string, string> resources);

        /// <summary>
        /// Build a semantic metadata description (IDX-0038) of a Model class
        /// from its C# source.
        /// </summary>
        /// <param name="sourceText">Full C# source text for the model file.</param>
        /// <returns>A populated <see cref="ModelMetadataDescription"/>.</returns>
        ModelMetadataDescription BuildModelMetadataDescription(string sourceText, IReadOnlyDictionary<string, string> resources);

        /// <summary>
        /// Build a semantic description of a Manager class from its C# source
        /// (IDX-0039 ManagerDescription).
        /// </summary>
        /// <param name="sourceText">Full C# source text for the manager file.</param>
        /// <returns>A populated <see cref="ManagerDescription"/>.</returns>
        ManagerDescription BuildManagerDescription(string sourceText);

        /// <summary>
        /// Build a semantic description of a Repository class from its C# source
        /// (IDX-0040 RepositoryDescription).
        /// </summary>
        /// <param name="sourceText">Full C# source text for the repository file.</param>
        /// <returns>A populated <see cref="RepositoryDescription"/>.</returns>
        RepositoryDescription BuildRepositoryDescription(string sourceText);

        /// <summary>
        /// Build a semantic description of an interface from its C# source
        /// (IDX-0042 InterfaceDescription).
        /// </summary>
        /// <param name="sourceText">Full C# source text containing the interface.</param>
        /// <returns>A populated <see cref="InterfaceDescription"/>.</returns>
        InterfaceDescription BuildInterfaceDescription(string sourceText);

        /// <summary>
        /// Build EndpointDescription records for all HTTP endpoints defined in
        /// a controller source file (IDX-0041).
        /// </summary>
        /// <param name="sourceText">Full C# source text for the controller file.</param>
        /// <returns>A read-only list of <see cref="EndpointDescription"/> items, one per endpoint.</returns>
        IReadOnlyList<EndpointDescription> BuildEndpointDescriptions(string sourceText);
    }
}
