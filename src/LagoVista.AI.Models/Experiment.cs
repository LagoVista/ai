// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 98cefe2eeabe6cbdce5a24160107a03c41716e4bf4fbb3bba17ba31c90e21c76
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Experiment_Title, AIResources.Names.Experiemnt_Help, AIResources.Names.Experiment_Description, 
        EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources), FactoryUrl: "/api/ml/model/experiment/factory")]
    public class Experiment : IFormDescriptor
    {
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]

        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public string Key { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.Experiment_Instructions, IsRequired:true, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Instructions { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Description),
                nameof(Instructions)
            };
        }
    }
}
