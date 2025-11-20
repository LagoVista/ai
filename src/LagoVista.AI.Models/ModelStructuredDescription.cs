using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Represents the structural description of a model/entity for IDX-0037
    /// (SubKind=Model, ChunkFlavor=Structured, ContentType=ModelStructure).
    /// This DTO maps 1:1 with the JSON payload for the structured chunk.
    /// </summary>
    public class ModelStructureDescription
    {
        public string ModelName { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        public string QualifiedName { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Help { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string SaveUrl { get; set; }


        public List<ModelProperty> Properties { get; set; } = new List<ModelProperty>();

        public List<EntityHeaderReference> EntityHeaderRefs { get; set; } = new List<EntityHeaderReference>();

        public List<ChildObjectDescription> ChildObjects { get; set; } = new List<ChildObjectDescription>();

        public List<ModelRelationship> Relationships { get; set; } = new List<ModelRelationship>();

        public ResourceMetadata ResourceMetadata { get; set; } = new ResourceMetadata();
    }

    
    /// <summary>
    /// Structural description of a single property on the model.
    /// </summary>
    public class ModelProperty
    {
        public string PropertyName { get; set; } = string.Empty;

        public string LanguageType { get; set; } = string.Empty;

        public bool IsNullable { get; set; }

        public bool IsCollection { get; set; }

        public bool IsEnum { get; set; }

        public string UnderlyingType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a relationship expressed via EntityHeader<T>
    /// or List<EntityHeader<T>>.
    /// </summary>
    public class EntityHeaderReference
    {
        public string FieldName { get; set; } = string.Empty;

        public string TargetType { get; set; } = string.Empty;

        public bool IsCollection { get; set; }

        public bool HasFKeyAttribute { get; set; }

        public string KeyField { get; set; } = string.Empty;

        public bool IsEnumHeader { get; set; }
    }

    /// <summary>
    /// Represents a child object owned exclusively by this model.
    /// </summary>
    public class ChildObjectDescription
    {
        public string FieldName { get; set; } = string.Empty;

        public string ChildType { get; set; } = string.Empty;

        public bool IsCollection { get; set; }

        public string RelationshipKind { get; set; } = "Owned";
    }

    /// <summary>
    /// ERD-level relationships between this model and other models.
    /// </summary>
    public class ModelRelationship
    {
        public string RelationshipKind { get; set; } = string.Empty;

        public string FieldName { get; set; } = string.Empty;

        public string TargetType { get; set; } = string.Empty;

        public bool IsCollection { get; set; }

        public bool HasFKeyAttribute { get; set; }
    }

    /// <summary>
    /// Optional resource metadata derived from EntityDescription attributes.
    /// </summary>
    public class ResourceMetadata
    {
        public string Title { get; set; } = string.Empty;

        public string Help { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
