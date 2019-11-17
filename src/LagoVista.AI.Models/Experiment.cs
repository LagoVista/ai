using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Experiment_Title, AIResources.Names.Experiemnt_Help, AIResources.Names.Experiment_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class Experiment
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
    }
}
