using LagoVista.AI.Models.Resources;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Models.AuthoritativeAnswers
{
    public enum ReferenceEntryConfidence
    {
        [EnumLabel(ReferenceEntry.ReferenceEntry_Unknown, AIResources.Names.Common_Unknown, typeof(AIResources))]
        Unknown,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Low, AIResources.Names.Common_Low, typeof(AIResources))]
        Low,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Medium, AIResources.Names.Common_Medium, typeof(AIResources))]
        Medium,
        [EnumLabel(ReferenceEntry.ReferenceEntry_High, AIResources.Names.Common_High, typeof(AIResources))]
        High,
    }

    public enum ReferenceEntrySource
    {
        [EnumLabel(ReferenceEntry.ReferenceEntry_Source_Inferred, AIResources.Names.ReferenceEntry_Source_Inferred, typeof(AIResources))]
        Inferred,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Source_Imported, AIResources.Names.ReferenceEntry_Source_Imported, typeof(AIResources))]
        Imported,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Source_UserProvided, AIResources.Names.ReferenceEntry_Source_UserProvided, typeof(AIResources))]
        UserProvided,
    }

    public enum ReferenceEntryMetadataQuality
    {
        [EnumLabel(ReferenceEntry.ReferenceEntry_Unknown, AIResources.Names.Common_Unknown, typeof(AIResources))]
        Unknown,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Low, AIResources.Names.Common_Low, typeof(AIResources))]
        Low,
        [EnumLabel(ReferenceEntry.ReferenceEntry_Medium, AIResources.Names.Common_Medium, typeof(AIResources))]
        Medium,
        [EnumLabel(ReferenceEntry.ReferenceEntry_High, AIResources.Names.Common_High, typeof(AIResources))]
        High,
    }


    /// <summary>
    /// Authoritative Answer (AQ) entry.
    ///
    /// Goal:
    /// - Capture settled clarifications that can be reused by both humans and the LLM.
    /// - Keep this lightweight; architectural invariants and long-term commitments remain DDRs.
    ///
    /// Notes:
    /// - Most metadata should be inferred automatically to keep human interaction low-friction.
    /// - Table Storage constraints apply (per-property ~64KB). Keep answers concise.
    /// </summary>
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.ReferenceEntry_Title, AIResources.Names.ReferenceEntry_Help, AIResources.Names.ReferenceEntry_Description,
        EntityDescriptionAttribute.EntityTypes.Ai, typeof(AIResources),

        GetUrl: "/api/referenceentry/{id}", GetListUrl: "/api/referenceentries", FactoryUrl: "/api/referenceentry/factory", SaveUrl: "/api/referenceentry",
        DeleteUrl: "/api/referenceentry/{id}",

        PreviewUIUrl: "/contentmanagement/reference/{id}/preview", ListUIUrl: "/contentmanagement/references", EditUIUrl: "/contentmanagement/reference/{id}",
        CreateUIUrl: "/contentmanagement/reference/add",

        Icon: "icon-fo-mobile-book", ClusterKey: "rag", ModelType: EntityDescriptionAttribute.ModelTypes.Document,
        Shape: EntityDescriptionAttribute.EntityShapes.Entity, Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime,
        Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true, IndexTier: EntityDescriptionAttribute.IndexTiers.Primary,
        IndexPriority: 90, IndexTagsCsv: "ai,rag,reference")]
    public class ReferenceEntry : EntityBase, IValidateable, ISummaryFactory, IFormDescriptor, IFormDescriptorCol2, IRagableEntity
    {
        public const string ReferenceEntry_Unknown = "unknown";
        public const string ReferenceEntry_Low = "low";
        public const string ReferenceEntry_Medium = "medium";
        public const string ReferenceEntry_High = "high";

        public const string ReferenceEntry_Source_UserProvided = "userprovided";
        public const string ReferenceEntry_Source_Imported = "imported";
        public const string ReferenceEntry_Source_Inferred = "inferred";


        /// <summary>
        /// The TLA from the mode that this was generated along 
        /// with a serial number, Entire string will always be 10 digits
        /// TLA-000123
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_ReferenceIdentifier, IsRequired: true, IsUserEditable: false, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string ReferenceIdentifier { get; set; }
        /// <summary>
        /// Primary topic / mode tag (e.g., COD, SYS). Typically inferred.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_TLA, HelpResource: AIResources.Names.ReferenceEntry_TLA_Help, IsRequired: true, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string PrimaryTla { get; set; }

        /// <summary>
        /// True if this entry should be used for retrieval.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.Common_IsActive, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// If this entry supersedes a previous AQ entry, store the prior AqId here
        /// entity header has both ID, Key and Text
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_Supersedes, IsUserEditable: false, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public EntityHeader Supersedes { get; set; }

        /* Question Section */
        /// <summary>
        /// Human-friendly question (optional). If not provided, NormalizedQuestion may be displayed.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_UserQuestion, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string HumanQuestion { get; set; }
        /// <summary>
        /// Canonical, normalized question used for lookup.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_NormalizedModelQuestion, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string NormalizedModelQuestion { get; set; }

        /// <summary>
        /// Internal not user editable.
        /// </summary>
        public string NormalizedModelQuestionHash    { get; set; }

        /// <summary>
        /// LLM-optimized question (optional). If not provided, NormalizedQuestion may be used.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_ModelFormattedQuestion, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string ModelQuestion { get; set; }

        /// <summary>
        /// Question as it was embedded for RAQ indexing.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_EmbedQuestion, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string EmbedQuestion { get; set; }


        /* Answer Section */
        /// <summary>
        /// Human-friendly answer (optional).
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_UserAnswer, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string HumanAnswer { get; set; }
        /// <summary>
        /// LLM-optimized answer (optional). Keep this concise and directly actionable.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_ModelAnswer, IsRequired: true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string ModelAnswer { get; set; }


        /// <summary>
        /// Extracted "applies to" tokens (symbols/types/properties) inferred from question/answer.
        /// This is intended to be a higher-signal retrieval booster than free-form tags.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_AppliesTo, IsRequired: true, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<string> AppliesTo { get; set; } = new List<string>();

        /// <summary>
        /// Optional source reference (e.g., ddr:SYS-000123, faq:XYZ, human).
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_SourceRef, IsRequired: false, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string SourceRef { get; set; }

        /// <summary>
        /// Optional scope hint (even though org is the primary partitioning mechanism).
        /// </summary>
        [FormField(LabelResource: AIResources.Names.Common_Scope, IsRequired: false, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<string> Scope { get; set; } = new List<string>();

        public ReferenceEntrySummary CreateSummary()
        {
            var summary = new ReferenceEntrySummary();
            summary.Populate(this);
            summary.ReferenceIdentifier = ReferenceIdentifier;
            summary.PrimaryTla = PrimaryTla;
            summary.AnswerConfidence = AnswerConfidence;
            summary.AnswerSource = AnswerSource;
            return summary;
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(ReferenceIdentifier),
                nameof(PrimaryTla),
                nameof(Category),
                nameof(IsActive),
                nameof(AnswerConfidence),
                nameof(AnswerSource),
                nameof(MetadataQuality),
                nameof(AppliesTo),
                nameof(SourceRef),
                nameof(Scope),
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(HumanQuestion),
                nameof(HumanAnswer),
                nameof(ModelQuestion),
                nameof(NormalizedModelQuestion),
                nameof(EmbedQuestion),
                nameof(ModelAnswer),
            };
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return CreateSummary();
        }

        public Task<List<EntityRagContent>> GetRagContentAsync()
        {
            var entityRagContent = new EntityRagContent();
            entityRagContent.EmbeddingContent = EmbedQuestion;
            entityRagContent.ModelDescription = ModelQuestion;
            entityRagContent.HumanDescription = HumanQuestion;
            entityRagContent.Payload = RagVectorPayload.FromEntity(this);
            entityRagContent.Payload.Meta.ContentTypeId = RagContentType.Spec;
            entityRagContent.Payload.Meta.Subtype = "referenceentry";

            return Task.FromResult(new List<EntityRagContent>() { entityRagContent });
        }

        /// <summary>
        /// Confidence hint. 
        /// Expected values: high | medium | low
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_AnswerConfidence, IsRequired: true, FieldType: FieldTypes.Picker, WaterMark: AIResources.Names.ReferenceEntry_AnswerConfidence_Select, EnumType: typeof(ReferenceEntryConfidence), ResourceType: typeof(AIResources))]
        public EntityHeader<ReferenceEntryConfidence> AnswerConfidence { get; set; }

        /// <summary>
        /// Indicates whether metadata was inferred or explicitly provided.
        /// Expected values: inferred | user_provided
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_AnswerSource, IsRequired: true, FieldType: FieldTypes.Picker, WaterMark: AIResources.Names.ReferenceEntry_AnswerSource_Select, EnumType: typeof(ReferenceEntrySource), ResourceType: typeof(AIResources))]
        public EntityHeader<ReferenceEntrySource> AnswerSource { get; set; }

        /// <summary>
        /// Confidence hint. 
        /// Expected values: high | medium | low
        /// </summary>
        [FormField(LabelResource: AIResources.Names.ReferenceEntry_MetadataQuality, IsRequired: true, FieldType: FieldTypes.Picker, WaterMark: AIResources.Names.ReferenceEntry_MetadataQuality_Select, EnumType: typeof(ReferenceEntryMetadataQuality), ResourceType: typeof(AIResources))]
        public EntityHeader<ReferenceEntryMetadataQuality> MetadataQuality { get; set; }

    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ReferenceEntries_Title, AIResources.Names.ReferenceEntry_Help, AIResources.Names.ReferenceEntry_Description, EntityDescriptionAttribute.EntityTypes.Ai, typeof(AIResources),
       GetUrl: "/api/referenceentry/{id}", GetListUrl: "/api/referenceentries", FactoryUrl: "/api/referenceentry/factory", SaveUrl: "/api/referenceentry", DeleteUrl: "/api/referenceentry/{id}",
       PreviewUIUrl: "/contentmanagement/reference/{id}/preview", ListUIUrl: "/contentmanagement/references", EditUIUrl: "/contentmanagement/reference/{id}", CreateUIUrl: "/contentmanagement/reference/add", Icon: "icon-ae-database-3")]
    public class ReferenceEntrySummary : SummaryData
    {
        public string ReferenceIdentifier { get; set; }
        public string PrimaryTla { get; set; }
        public EntityHeader<ReferenceEntryConfidence> AnswerConfidence { get; set; }
        public EntityHeader<ReferenceEntrySource> AnswerSource { get; set; }
    }
}

