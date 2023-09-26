using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelCategory_Title, AIResources.Names.ModelCategory_Help, AIResources.Names.ModelCategory_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
        FactoryUrl: "/api/ml/modellabel/factory", GetListUrl: "/api/ml/modelcategories", GetUrl: "/api/ml/modelcategory/{id}", SaveUrl: "/api/ml/modelcategory", DeleteUrl: "/api/ml/modelcategory/{id}")]
    public class ModelCategory : IIDEntity, IKeyedEntity, INamedEntity, IDescriptionEntity, IAuditableEntity, IOwnedEntity, INoSQLEntity, IValidateable, IFormDescriptor
    {
        public string DatabaseName { get; set; }
        public string EntityType { get; set; }


        [JsonProperty("id")]
        public string Id { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        
        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public String Key { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }
        public EntityHeader CreatedBy { get; set; }
        public EntityHeader LastUpdatedBy { get; set; }
        public bool IsPublic { get; set; }
        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public ModelCategorySummary CreateSummary()
        {
            return new ModelCategorySummary()
            {
                Description = Description,
                Id = Id,
                IsPublic = IsPublic,
                Key = Key,
                Name = Name,
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Description),
            };
        }
    }

    public class ModelCategorySummary : SummaryData
    {

    }
}
