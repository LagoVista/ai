using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Default implementation of <see cref="ICodeDescriptionService"/>.
    ///
    /// This class is a thin façade over the static description builders:
    /// - ModelStructureDescriptionBuilder (IDX-0037)
    /// - ModelMetadataDescriptionBuilder (IDX-0038)
    /// - ManagerDescriptionBuilder (IDX-0039)
    /// - RepositoryDescriptionBuilder (IDX-0040)
    /// - InterfaceDescriptionBuilder (IDX-0042)
    /// - EndpointDescriptionBuilder (IDX-0041)
    ///
    /// It provides a DI-friendly entry point that is easy to mock and swap.
    /// </summary>
    public class CodeDescriptionService : ICodeDescriptionService
    {
        /// <inheritdoc />
        public ModelStructureDescription BuildModelStructureDescription(string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelStructureDescriptionBuilder.FromSource(sourceText, resources);
        }

        /// <inheritdoc />
        public ModelMetadataDescription BuildModelMetadataDescription(string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelMetadataDescriptionBuilder.FromSource(sourceText, resources);
        }

        /// <inheritdoc />
        public ManagerDescription BuildManagerDescription(string sourceText)
        {
            return ManagerDescriptionBuilder.CreateManagerDescription(sourceText);
        }

        /// <inheritdoc />
        public RepositoryDescription BuildRepositoryDescription(string sourceText)
        {
            return RepositoryDescriptionBuilder.CreateRepositoryDescription(sourceText);
        }

        /// <inheritdoc />
        public InterfaceDescription BuildInterfaceDescription(string sourceText)
        {
            return InterfaceDescriptionBuilder.CreateInterfaceDescription(sourceText);
        }

        /// <inheritdoc />
        public IReadOnlyList<EndpointDescription> BuildEndpointDescriptions(string sourceText)
        {
            return EndpointDescriptionBuilder.CreateEndpointDescriptions(sourceText);
        }
    }
}
