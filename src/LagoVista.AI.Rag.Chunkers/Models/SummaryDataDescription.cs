using System;
using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Interfaces;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0052: Semantic description of a SummaryData-based list surface.
    ///
    /// This is a pure description model – it captures what a list of
    /// SummaryData rows represents, how it behaves, and which fields are
    /// exposed, without encoding any chunking-specific concepts
    /// (no PartIndex, no ContentHash, etc.).
    ///
    /// Downstream components are responsible for projecting this into
    /// NormalizedChunk / RagVectorPayload instances.
    ///
    /// The partial class also implements ISummarySectionBuilder in a
    /// companion file, which converts this structured description into
    /// human-readable SummarySection blocks suitable for embedding.
    /// </summary>
    public sealed partial class SummaryDataDescription : SummaryFacts, ISummarySectionBuilder
    {
        /// <summary>
        /// Subtype used by higher-level systems to identify this as a
        /// SummaryData-based list description.
        /// </summary>
        public override string Subtype => "SummaryList";

        // -------------------- Identity --------------------

        /// <summary>
        /// Human-friendly name for the list surface (e.g. "Devices",
        /// "Users").
        /// </summary>
        public string ListName { get; set; }

        /// <summary>
        /// CLR type name of the summary row type, e.g.
        /// "MyNamespace.DeviceSummary".
        /// </summary>
        public string SummaryTypeName { get; set; }

        /// <summary>
        /// Logical entity name that the summary rows represent,
        /// e.g. "Device" for a DeviceSummary.
        /// May be null when not inferable.
        /// </summary>
        public string UnderlyingEntityTypeName { get; set; }

        /// <summary>
        /// Business/domain classification (billing, customers, iot, hr, etc.).
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Fully qualified name that uniquely identifies the list surface
        /// or its backing summary type.
        /// </summary>
        public string QualifiedName { get; set; }

        // -------------------- Human Text --------------------

        /// <summary>
        /// Short human-readable title or label for the list.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Plain-language description of what this list shows and how it is
        /// typically used.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional extended help text for documentation or UI help.
        /// </summary>
        public string Help { get; set; }

        /// <summary>
        /// Optional plain-text description that focuses specifically on
        /// behavior (soft-delete, draft/published, ratings, discussions,
        /// etc.). This can be a richer narrative used directly for RAG.
        /// </summary>
        public string BehaviorDescription { get; set; }

        // -------------------- Navigation --------------------

        /// <summary>
        /// UI entry point for this list surface when known, typically
        /// obtained from model/entity descriptions.
        /// </summary>
        public string ListUIUrl { get; set; }

        /// <summary>
        /// API endpoint or logical URL used to obtain the
        /// ListResponse&lt;SummaryData&gt; for this list when known.
        /// </summary>
        public string GetListUrl { get; set; }

        // -------------------- Fields / Columns --------------------

        /// <summary>
        /// Column/field-level description for the SummaryData type used by
        /// this list, including visibility, headers, and whether the field
        /// comes from the base SummaryData class or an extension.
        /// </summary>
        public IReadOnlyList<SummaryDataFieldDescription> Fields { get; set; }
    }

    /// <summary>
    /// Describes a single field/column on a SummaryData-derived type as it
    /// appears in a list surface.
    /// </summary>
    public sealed class SummaryDataFieldDescription
    {
        /// <summary>
        /// CLR property name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Fully-qualified CLR type name for the property.
        /// </summary>
        public string ClrType { get; set; }

        /// <summary>
        /// True when the column is visible in the list by default, based on
        /// ListColumn metadata when available.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Optional column header text, when available from ListColumn or
        /// related metadata.
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// True when this field is part of the base SummaryData shape
        /// (Id, Name, Key, Description, etc.).
        /// False when the field is defined on a concrete summary type.
        /// </summary>
        public bool IsBaseSummaryDataField { get; set; }
    }
}
