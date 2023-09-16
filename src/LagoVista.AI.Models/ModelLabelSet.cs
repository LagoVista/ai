using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.LabelSet_Title, AIResources.Names.LabelSet_Help, AIResources.Names.LabelSet_Help, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class ModelLabelSet : IIDEntity, IKeyedEntity, INamedEntity, IDescriptionEntity, IAuditableEntity, IOwnedEntity, INoSQLEntity, IValidateable, IFormDescriptor
    {
        public string DatabaseName { get; set; }
        public string EntityType { get; set; }

        public ModelLabelSet()
        {
            Labels = new List<ModelLabel>();
        }

        [JsonProperty("id")]
        public string Id { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public String Key { get; set; }


        [FormField(LabelResource: AIResources.Names.LabelSet_Labels, FieldType: FieldTypes.ChildListInline, ResourceType: typeof(AIResources))] 
        public List<ModelLabel> Labels { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }
        public EntityHeader CreatedBy { get; set; }
        public EntityHeader LastUpdatedBy { get; set; }
        public bool IsPublic { get; set; }
        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public ModelLabelSetSummary CreateSummary()
        {
            return new ModelLabelSetSummary()
            {
                Description = Description,
                Id = Id,
                Key = Key,
                IsPublic = IsPublic,
                Name = Name
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Description),
                nameof(Labels)
            };
        }
    }

    public class ModelLabelSetSummary : SummaryData
    {

    }
}
