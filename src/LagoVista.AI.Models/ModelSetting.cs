// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 666944069fafe30e6a47ba0ba9366dc73de9bddae499939712bf0553d0bfea04
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelSetting_Title, AIResources.Names.ModelSetting_Help, AIResources.Names.ModelSetting_Description,
        EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources), FactoryUrl: "/api/ml/model/setting/factory")]
    public class ModelSetting : IFormDescriptor
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelSetting_Value, FieldType: FieldTypes.Text, IsRequired:true, ResourceType: typeof(AIResources))]
        public string Value { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                 nameof(Name),
                 nameof(Key),
                 nameof(Value),
                 nameof(Description),
            };
        }
    }
}
