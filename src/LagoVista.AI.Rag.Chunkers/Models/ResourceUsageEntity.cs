using System;
using Azure;
using Azure.Data.Tables;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0052 – Azure Table Storage entity for resource usage.
    ///
    /// This is the persisted form of <see cref="ResourceUsageRecord"/>, designed
    /// specifically for Azure Table Storage (PartitionKey/RowKey + flat properties).
    /// </summary>
    public class ResourceUsageEntity : ITableEntity
    {
        // ---------- Azure Table Storage Required Properties ----------

        /// <inheritdoc />
        public string PartitionKey { get; set; }

        /// <inheritdoc />
        public string RowKey { get; set; }

        /// <inheritdoc />
        public DateTimeOffset? Timestamp { get; set; }

        /// <inheritdoc />
        public ETag ETag { get; set; }

        // ---------- Identity ----------

        public string OrgId { get; set; }
        public string ProjectId { get; set; }
        public string RepoId { get; set; }

        // ---------- Resource Identity (soft FK to RESX) ----------

        public string ResourceContainerFullName { get; set; }
        public string ResourceContainerShortName { get; set; }
        public string ResourceKey { get; set; }
        public string Culture { get; set; }

        // ---------- Usage Target (Model / Property) ----------

        /// <summary>
        /// Fully qualified model/type name when the usage comes from a metadata
        /// attribute applied to a model or property.
        /// </summary>
        public string TargetModelFullName { get; set; }

        /// <summary>
        /// Property name when the usage comes from a metadata attribute on a
        /// specific property. Null/empty for type-level attributes.
        /// </summary>
        public string TargetModelPropertyName { get; set; }

        // ---------- Attribute Context ----------

        /// <summary>
        /// Attribute type name that carried the resource reference.
        /// </summary>
        public string AttributeTypeName { get; set; }

        /// <summary>
        /// Attribute property name that carried the resource reference.
        /// </summary>
        public string AttributePropertyName { get; set; }

        // ---------- Symbol / File Location ----------

        public string RelativePath { get; set; }
        public string SymbolName { get; set; }
        public string SymbolFullName { get; set; }
        public string SymbolKind { get; set; }
        public string SubKind { get; set; }

        // ---------- Classification ----------

        /// <summary>
        /// Usage kind stored as int corresponding to <see cref="ResourceUsageKind"/>.
        /// </summary>
        public int UsageKind { get; set; }

        public bool IsTestCode { get; set; }
        public bool IsNameConstant { get; set; }

        // ---------- Trace / Diagnostics ----------

        public string UsageContextSnippet { get; set; }
        public string UsagePattern { get; set; }
    }
}
