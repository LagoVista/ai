// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: TBD
// IndexVersion: 1
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0037: Structured chunk for Kind=Model capturing identity, domain,
    /// structural graph, properties, entity header references, child objects,
    /// relationships, and operational affordances.
    /// </summary>
    public sealed partial class ModelStructureDescription
    {
        // ---------- Identity / Domain ----------
        public string ModelName { get; set; }
        public string Namespace { get; set; }
        public string QualifiedName { get; set; }   // Namespace + ModelName
        public string Domain { get; set; }          // e.g. "Devices", "Alerts"

        // ---------- UX Strings ----------
        public string Title { get; set; }
        public string Help { get; set; }
        public string Description { get; set; }

        // ---------- Capabilities ----------
        public bool Cloneable { get; set; }
        public bool CanImport { get; set; }
        public bool CanExport { get; set; }

        // ---------- UI / API Affordances ----------
        public string ListUIUrl { get; set; }
        public string EditUIUrl { get; set; }
        public string CreateUIUrl { get; set; }
        public string HelpUrl { get; set; }

        public string InsertUrl { get; set; }
        public string SaveUrl { get; set; }
        public string UpdateUrl { get; set; }
        public string FactoryUrl { get; set; }
        public string GetUrl { get; set; }
        public string GetListUrl { get; set; }
        public string DeleteUrl { get; set; }

        // ---------- Structural Graph ----------
        public List<ModelPropertyDescription> Properties { get; set; } =
            new List<ModelPropertyDescription>();

        public List<ModelEntityHeaderRefDescription> EntityHeaderRefs { get; set; } =
            new List<ModelEntityHeaderRefDescription>();

        public List<ModelChildObjectDescription> ChildObjects { get; set; } =
            new List<ModelChildObjectDescription>();

        public List<ModelRelationshipDescription> Relationships { get; set; } =
            new List<ModelRelationshipDescription>();
    }

    /// <summary>
    /// Structural description of a single model property.
    /// This is about shape and semantics, not full UI metadata (that lives in IDX-0038).
    /// </summary>
    public sealed class ModelPropertyDescription
    {
        public string Name { get; set; }            // C# property name
        public string ClrType { get; set; }         // e.g. "string", "int", "EntityHeader", "List<Device>"
        public bool IsCollection { get; set; }      // true for lists/arrays
        public bool IsValueType { get; set; }       // primitive/value semantics
        public bool IsEnum { get; set; }            // enum-backed
        public bool IsKey { get; set; }             // primary key / identity flag

        /// <summary>
        /// If this property is represented as an EntityHeader, points to an EntityHeader ref key.
        /// </summary>
        public string EntityHeaderRefKey { get; set; }

        /// <summary>
        /// If this property is a complex child object, points to a ChildObjects key.
        /// </summary>
        public string ChildObjectKey { get; set; }

        /// <summary>
        /// Optional domain or logical group (e.g. "Identity", "Address", "Audit").
        /// </summary>
        public string Group { get; set; }
    }

    /// <summary>
    /// Reference to an EntityHeader-based relationship (typical LagoVista pattern).
    /// </summary>
    public sealed class ModelEntityHeaderRefDescription
    {
        /// <summary>
        /// Stable key used to link from Properties[].EntityHeaderRefKey.
        /// </summary>
        public string Key { get; set; }

        public string PropertyName { get; set; }    // backing property on the model
        public string TargetType { get; set; }      // CLR type of the referenced entity
        public string Domain { get; set; }          // domain of the target entity
        public bool IsCollection { get; set; }      // collection of entity headers vs single
    }

    /// <summary>
    /// Description of a nested/child object on the model (owned type / value object).
    /// </summary>
    public sealed class ModelChildObjectDescription
    {
        /// <summary>
        /// Stable key used to link from Properties[].ChildObjectKey.
        /// </summary>
        public string Key { get; set; }

        public string PropertyName { get; set; }    // property on the parent model
        public string ClrType { get; set; }         // child CLR type
        public bool IsCollection { get; set; }

        /// <summary>
        /// Optional short title/label for the child group (for LLM reasoning).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Optional description of what this child object represents.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// High-level relationship between this model and other models.
    /// Typically derived from FKeyProperty, EntityHeader, collections, etc.
    /// </summary>
    public sealed class ModelRelationshipDescription
    {
        public string Name { get; set; }               // e.g. "DeviceToCustomer"
        public string FromModel { get; set; }          // usually this model (qualified name)
        public string ToModel { get; set; }            // qualified name of the related model

        /// <summary>
        /// Relationship cardinality, e.g. "OneToOne", "OneToMany", "ManyToMany".
        /// </summary>
        public string Cardinality { get; set; }

        /// <summary>
        /// Source property on the FromModel that carries the relationship (FK, header, collection).
        /// </summary>
        public string SourceProperty { get; set; }

        /// <summary>
        /// Optional target property on the ToModel (back-reference).
        /// </summary>
        public string TargetProperty { get; set; }

        /// <summary>
        /// Optional description for LLMs, derived from help text or domain knowledge.
        /// </summary>
        public string Description { get; set; }
    }
}
