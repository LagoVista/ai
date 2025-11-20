using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// DTOs for reporting facet values to the metadata registry.
    /// Implements IDX-033: Metadata Registry & Facet Discovery.
    /// </summary>
    public class FacetValue
    {
        /// <summary>
        /// Name of the metadata property, e.g. "Kind", "SubKind", "Repo".
        /// </summary>
        public string Property { get; set; }

        /// <summary>
        /// Concrete value of the property, e.g. "SourceCode", "Manager".
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// Single-dimension facet entry, e.g. Kind=SourceCode.
    /// Mirrors IDX-033 SingleFacetShape.
    /// </summary>
    public class SingleFacet
    {
        public string Property { get; set; }
        public string Value { get; set; }

        /// <summary>
        /// Number of distinct documents/chunks that had this facet.
        /// Aggregation policy is decided by the ingestor; the registry is agnostic.
        /// </summary>
        public long Count { get; set; }
    }

    /// <summary>
    /// Multi-dimension combination facet, e.g. Kind=SourceCode,SubKind=Model,ChunkFlavor=Raw.
    /// Mirrors IDX-033 ComboFacetShape.
    /// </summary>
    public class ComboFacet
    {
        /// <summary>
        /// Ordered list of property/value pairs that define this combination.
        /// </summary>
        public List<FacetValue> Dimensions { get; set; } = new List<FacetValue>();

        /// <summary>
        /// Number of items that matched this combination.
        /// </summary>
        public long Count { get; set; }
    }

    /// <summary>
    /// Top-level payload sent from the ingestor to the metadata registry after a run.
    /// One instance corresponds to a single ingestion run per project/repo.
    /// </summary>
    public class MetadataRegistryReport
    {
        /// <summary>
        /// Logical project identifier, typically the projectId / repo short name.
        /// Example: "co.core".
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Normalized repository URL (same value used for DocId computation).
        /// </summary>
        public string Repo { get; set; }

        /// <summary>
        /// Optional organization identifier (OrgId) when available.
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        /// Index version used for this run (IDX-012, IDX-013, etc.).
        /// </summary>
        public int IndexVersion { get; set; }

        /// <summary>
        /// UTC timestamp when the ingestion run finished.
        /// </summary>
        public DateTime CompletedUtc { get; set; }

        /// <summary>
        /// Single-dimension facet values discovered during the run.
        /// </summary>
        public List<SingleFacet> SingleFacets { get; set; } = new List<SingleFacet>();

        /// <summary>
        /// Combination facet values (Kind+SubKind+ChunkFlavor, etc.).
        /// </summary>
        public List<ComboFacet> ComboFacets { get; set; } = new List<ComboFacet>();

        /// <summary>
        /// Optional free-form metadata for diagnostics and future extensibility.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Simple configuration for the metadata registry endpoint.
    /// This can later be surfaced in IngestionConfig when we are ready to wire it in.
    /// </summary>
    public class MetadataRegistryConfig
    {
        /// <summary>
        /// Base URL of the metadata registry service, e.g. "https://metadata.nuviot.com".
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Relative path for the facet reporting endpoint, e.g. "/api/metadata/facets".
        /// </summary>
        public string ReportPath { get; set; } = "/api/metadata/facets";

        /// <summary>
        /// Optional API key or bearer token when the registry is secured.
        /// </summary>
        public string ApiKey { get; set; }
    }
}
