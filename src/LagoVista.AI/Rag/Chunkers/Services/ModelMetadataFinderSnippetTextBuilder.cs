using System;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Helper for constructing IDX-068 compliant Finder Snippet text
    /// from existing model/metadata descriptions.
    ///
    /// Phase 1 focuses on the unified EntityModelDescription finder
    /// snippet using ModelStructureDescription (and optionally
    /// ModelMetadataDescription when available).
    /// </summary>
    public static class ModelMetadataFinderSnippetTextBuilder
    {
        /// <summary>
        /// Builds a canonical Model finder snippet for an entity model.
        ///
        /// Layout:
        ///   Domain: DomainName
        ///   DomainSummary: one sentence domain summary
        ///
        ///   Kind: Model
        ///
        ///   Artifact: QualifiedTypeName
        ///   PrimaryEntity: ModelName
        ///   Aspects: Structure[, UIMetadata]
        ///
        ///   Purpose: Defines the PrimaryEntity entity, including its
        ///            structural shape and any available UI metadata and
        ///            interaction rules.
        /// </summary>
        public static string BuildModelFinderSnippet(DomainModelHeaderInformation header, String sectiontype, ModelMetadataDescription model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var sb = new StringBuilder();

            // -----------------------------------------------------------------
            // Domain / DomainSummary
            // -----------------------------------------------------------------
            var domainName = header?.DomainName;
            var domainTagline = header?.DomainTagLine;

            if (string.IsNullOrWhiteSpace(domainName))
            {
                // Fall back to BusinessDomainKey when explicit domain name
                // is not available.
                domainName = model.BusinessDomainKey;
            }

            if (string.IsNullOrWhiteSpace(domainTagline))
            {
                // Prefer title/description as a pseudo-tagline before generic
                // fallbacks.
                if (!string.IsNullOrWhiteSpace(model.Title))
                {
                    domainTagline = model.Title;
                }
                else if (!string.IsNullOrWhiteSpace(model.Description))
                {
                    domainTagline = model.Description.Trim();
                }
                else if (!string.IsNullOrWhiteSpace(domainName))
                {
                    domainTagline = $"Domain for {domainName} models.";
                }
            }

            if (!string.IsNullOrWhiteSpace(domainName))
            {
                sb.Append("Domain: ");
                sb.AppendLine(domainName);
            }

            if (!string.IsNullOrWhiteSpace(domainTagline))
            {
                sb.Append("DomainSummary: ");
                sb.AppendLine(domainTagline);
            }

            sb.AppendLine();

            // -----------------------------------------------------------------
            // Kind
            // -----------------------------------------------------------------
            sb.AppendLine("Kind: Model");
            sb.AppendLine($"ViewType: {sectiontype}");
            sb.AppendLine();

            // -----------------------------------------------------------------
            // Artifact / PrimaryEntity / Aspects
            // -----------------------------------------------------------------
            var artifact = !string.IsNullOrWhiteSpace(model.QualifiedName)
                ? model.QualifiedName
                : !string.IsNullOrWhiteSpace(header?.ModelClassName)
                    ? header.ModelClassName
                    : !string.IsNullOrWhiteSpace(model.ModelName)
                        ? model.ModelName
                        : "(unknown-model)";

            var primaryEntity = !string.IsNullOrWhiteSpace(header?.ModelName)
                ? header.ModelName
                : !string.IsNullOrWhiteSpace(model.ModelName)
                    ? model.ModelName
                    : artifact;

            sb.AppendLine($"Artifact: {artifact}");
            sb.AppendLine($"Description: {model.Description}");
            sb.AppendLine($"PrimaryEntity: {primaryEntity}");
            sb.AppendLine($"Properties: {String.Join(", ", model.Fields.Select(fld => fld.Label))}");   
            sb.AppendLine();

            // -----------------------------------------------------------------
            // Purpose
            // -----------------------------------------------------------------
            sb.Append($"Purpose: Defines the {primaryEntity} including it's properties and structural shape.");

            return sb.ToString().Trim();
        }
    }
}
