using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Label_Title, AIResources.Names.Label_Help, AIResources.Names.Label_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class Label : IIDEntity, IKeyedEntity, INamedEntity, IDescriptionEntity, IAuditableEntity, IOwnedEntity, INoSQLEntity, IValidateable
    {
        public string DatabaseName { get; set; }
        public string EntityType { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Key, FieldType: FieldTypes.Key, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Title, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Title { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Index, FieldType: FieldTypes.Integer, IsRequired: true, ResourceType: typeof(AIResources))]
        public int Index { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Visible, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool Visible { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Enabled, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool Enabled { get; set; }


        [FormField(LabelResource: AIResources.Names.Label_Icon, FieldType: FieldTypes.Text, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }
        public EntityHeader CreatedBy { get; set; }
        public EntityHeader LastUpdatedBy { get; set; }
        public bool IsPublic { get; set; }
        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public LabelSummary CreateSummary()
        {
            return new LabelSummary()
            {
                Id = Id,
                Name = Name,
                Key = Key,
                Description = Description,
            };
        }
    }

    public class LabelSummary : SummaryData
    {

    }
}
