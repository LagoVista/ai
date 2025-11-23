using System;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0052 – In-memory representation of a single resource usage.
    ///
    /// This is produced by the ResourceUsageDetector and then mapped
    /// to ResourceUsageEntity for persistence in Azure Table Storage.
    /// </summary>
    public sealed class ResourceUsageRecord
    {
        // ---------- Tenant / Project Identity ----------

        public string OrgId { get; set; }
        public string ProjectId { get; set; }
        public string RepoId { get; set; }

        // ---------- Resource Identity (soft foreign key) ----------

        /// <summary>
        /// Fully qualified name of the resource container type
        /// (e.g. LagoVista.AI.Models.Resources.CommonStrings).
        /// </summary>
        public string ResourceContainerFullName { get; set; }

        /// <summary>
        /// Short name of the resource container type (e.g. CommonStrings).
        /// </summary>
        public string ResourceContainerShortName { get; set; }

        /// <summary>
        /// Resource key name (e.g. Address, Device_Status_Offline).
        /// </summary>
        public string ResourceKey { get; set; }

        /// <summary>
        /// Culture associated with this usage (usually empty / invariant).
        /// </summary>
        public string Culture { get; set; }

        // ---------- Code Location ----------

        /// <summary>
        /// Repository-relative file path where the usage occurs.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Simple symbol name in which the usage occurs
        /// (type name, property name, method name, etc.).
        /// </summary>
        public string SymbolName { get; set; }

        /// <summary>
        /// Fully qualified symbol name (namespace + type [+ member]).
        /// </summary>
        public string SymbolFullName { get; set; }

        /// <summary>
        /// Logical classification of the symbol kind
        /// (Property, Method, Type, etc.).
        /// </summary>
        public string SymbolKind { get; set; }

        /// <summary>
        /// Optional sub-kind (can be aligned with your SubKindDetector).
        /// </summary>
        public string SubKind { get; set; }

        // ---------- Usage Target (Model / Property) ----------

        /// <summary>
        /// Fully qualified model/type name when the resource
        /// is tied to a model via a metadata attribute.
        /// </summary>
        public string TargetModelFullName { get; set; }

        /// <summary>
        /// Property name when the resource is tied to a specific
        /// model property; otherwise null.
        /// </summary>
        public string TargetModelPropertyName { get; set; }

        // ---------- Attribute Context ----------

        /// <summary>
        /// Name of the attribute type that carried the resource reference
        /// (FormField, EntityDescription, EnumLabel, etc.).
        /// </summary>
        public string AttributeTypeName { get; set; }

        /// <summary>
        /// Name of the attribute property (actual or synthetic for positional),
        /// e.g. LabelResource, HelpResource, TitleResource, TextResource, etc.
        /// </summary>
        public string AttributePropertyName { get; set; }

        // ---------- Usage Classification ----------

        /// <summary>
        /// Categorization of how the resource is used.
        /// </summary>
        public ResourceUsageKind UsageKind { get; set; }

        /// <summary>
        /// True if the usage comes from ResourceLib.Names.ResourceKey
        /// rather than ResourceLib.ResourceKey.
        /// </summary>
        public bool IsNameConstant { get; set; }

        /// <summary>
        /// True if this usage occurred in a test project/file.
        /// </summary>
        public bool IsTestCode { get; set; }

        /// <summary>
        /// A short snippet of code showing the usage (for diagnostics).
        /// </summary>
        public string UsageContextSnippet { get; set; }

        /// <summary>
        /// Describes how it was detected
        /// (Attribute, ResourceProperty, NamesConstant).
        /// </summary>
        public string UsagePattern { get; set; }

        // ---------- Diagnostic / Debug Output ----------

        /// <summary>
        /// CSV serialization of all properties for debugging and inspection.
        /// </summary>
        public override string ToString()
        {
            return string.Join(",",
                Csv(OrgId),
                Csv(ProjectId),
                Csv(RepoId),

                Csv(ResourceContainerFullName),
                Csv(ResourceContainerShortName),
                Csv(ResourceKey),
                Csv(Culture),

                Csv(RelativePath),
                Csv(SymbolName),
                Csv(SymbolFullName),
                Csv(SymbolKind),
                Csv(SubKind),

                Csv(TargetModelFullName),
                Csv(TargetModelPropertyName),

                Csv(AttributeTypeName),
                Csv(AttributePropertyName),

                Csv(UsageKind.ToString()),
                Csv(IsNameConstant.ToString()),
                Csv(IsTestCode.ToString()),

                Csv(UsagePattern),
                Csv(UsageContextSnippet)
            );
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escape quotes for CSV
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// CSV header corresponding to ToString() output order.
        /// </summary>
        public static string CsvHeader =>
            "OrgId,ProjectId,RepoId," +
            "ResourceContainerFullName,ResourceContainerShortName,ResourceKey,Culture," +
            "RelativePath,SymbolName,SymbolFullName,SymbolKind,SubKind," +
            "TargetModelFullName,TargetModelPropertyName," +
            "AttributeTypeName,AttributePropertyName," +
            "UsageKind,IsNameConstant,IsTestCode," +
            "UsagePattern,UsageContextSnippet";
    }



    /// <summary>
    /// IDX-0052 – Classification of how a resource is used in code.
    /// </summary>
    public enum ResourceUsageKind
    {
        Unknown = 0,

        // UI text placements
        ButtonLabel = 1,
        MenuItemLabel = 2,
        DialogTitle = 3,
        DialogBody = 4,
        ColumnHeader = 5,
        FormFieldLabel = 6,
        FormFieldHelp = 7,
        Watermark = 8,

        // Messages
        ErrorMessage = 20,
        ValidationMessage = 21,
        StatusMessage = 22,

        // Models
        ModelTitle = 31,
        ModelHelp = 32,
        ModelDescription = 33,

        // Enums
        EnumLabel = 40,
        EnumHelp = 41,

        // Other
        Tooltip = 50,
        Other = 99
    }
}