using System;
using System.Collections.Generic;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Chunkers.Providers.Endpoints;
using LagoVista.AI.Chunkers.Providers.Interfaces;
using LagoVista.AI.Chunkers.Providers.Managers;
using LagoVista.AI.Chunkers.Providers.ModelMetaData;
using LagoVista.AI.Chunkers.Providers.ModelStructure;
using LagoVista.AI.Chunkers.Providers.Repositories;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;

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
        public InvokeResult<ModelStructureDescription> BuildModelStructureDescription(IndexFileContext ctx, string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelStructureDescriptionBuilder.FromSource(ctx, sourceText, resources);
        }

        /// <inheritdoc />
        public InvokeResult<ModelMetadataDescription> BuildModelMetadataDescription(IndexFileContext ctx, string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelMetadataDescriptionBuilder.FromSource(ctx, sourceText, resources);
        }

        public InvokeResult<SummaryDataDescription> BuildSummaryDescription(IndexFileContext ctx, string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return SummaryDataDescriptionBuilder.FromSource(ctx, sourceText, resources);
        }

        /// <inheritdoc />
        public InvokeResult<ManagerDescription> BuildManagerDescription(IndexFileContext ctx, string sourceText)
        {
            return ManagerDescriptionBuilder.CreateManagerDescription(ctx, sourceText);
        }

        /// <inheritdoc />
        public InvokeResult<RepositoryDescription> BuildRepositoryDescription(IndexFileContext ctx, string sourceText)
        {
            return RepositoryDescriptionBuilder.CreateRepositoryDescription(ctx, sourceText);
        }

        /// <inheritdoc />
        public InvokeResult<InterfaceDescription> BuildInterfaceDescription(IndexFileContext ctx, string sourceText)
        {
            return InterfaceDescriptionBuilder.CreateInterfaceDescription(ctx, sourceText);
        }

        /// <inheritdoc />
        public InvokeResult<IReadOnlyList<EndpointDescription>> BuildEndpointDescriptions(IndexFileContext ctx, string sourceText)
        {
            throw new NotImplementedException();
            //return EndpointDescriptionBuilder.CreateEndpointDescriptions(ctx, sourceText);
        }

        public InvokeResult<IReadOnlyList<DomainSummaryInfo>> ExtractDomainSummary(string source)
        {
            return DomainDescriptorSummaryExtractor.Extract(source);
        }

        public InvokeResult<ModelStructureDescription> BuildModelStructureDescription(string sourceText)
        {
            return ModelStructureDescriptionBuilder.FromSource(sourceText);
        }

        public InvokeResult<ModelStructureDescription> BuildModelStructureDescription(string sourceText, IReadOnlyDictionary<string, string> resources)
        {
            return ModelStructureDescriptionBuilder.FromSource(sourceText, resources);
        }

    }
}
