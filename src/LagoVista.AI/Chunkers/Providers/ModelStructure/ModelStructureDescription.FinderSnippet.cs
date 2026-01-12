using System;
using System.Collections.Generic;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Chunkers.Providers.ModelStructure;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Finder Snippet oriented summary implementation for ModelStructureDescription
    /// aligned with IDX-068 EntityModelDescription. This can run in parallel with
    /// the existing, more narrative BuildSections implementation.
    /// </summary>
    public sealed partial class ModelStructureDescription
    {
        /// <summary>
        /// Builds a single unified Finder Snippet section for this model.
        ///
        /// In unified mode, callers can treat this as the canonical snippet
        /// for the entity model and keep existing sections as backing
        /// artifacts only.
        /// </summary>
        public IEnumerable<SummarySection> BuildFinderSnippetSections(
            DomainModelHeaderInformation headerInfo,
            bool hasUiMetadata = false,
            int maxTokens = 512)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 512;
            }

            var finderText = ModelStructuredFinderSnippetTextBuilder.BuildModelFinderSnippet(
                headerInfo,
                this,
                hasUiMetadata);

            // Artifact and PrimaryEntity mirror the Finder Snippet header
            // so that SummarySection identity stays consistent.
            var artifact = !string.IsNullOrWhiteSpace(QualifiedName)
                ? QualifiedName
                : !string.IsNullOrWhiteSpace(headerInfo?.ModelClassName)
                    ? headerInfo.ModelClassName
                    : !string.IsNullOrWhiteSpace(ModelName)
                        ? ModelName
                        : "(unknown-model)";

            var primaryEntity = !string.IsNullOrWhiteSpace(headerInfo?.ModelName)
                ? headerInfo.ModelName
                : !string.IsNullOrWhiteSpace(ModelName)
                    ? ModelName
                    : artifact;

            var section = new SummarySection
            {
                SectionKey = "entity-model-finder-snippet",
                SectionType = "FinderSnippet",
                Flavor = "EntityModelDescription",
                SymbolName = artifact,
                SymbolType = "Model",
                DomainKey = headerInfo?.DomainKey ?? BusinessDomainKey,
                ModelClassName = headerInfo?.ModelClassName ?? headerInfo?.ModelClassName ?? QualifiedName,
                ModelName = primaryEntity,
                SectionNormalizedText = this.FullSourceText,
                FinderSnippet = finderText
            };

            _summarySections = new[] { section };

            return _summarySections;
        }
    }
}
