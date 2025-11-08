// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8151427b5594c28281575af42be4827111c8efd75435c520f0cbc39f474a6f15
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Preprocessor_Title, AIResources.Names.Preprocessor_Help, AIResources.Names.Preprocessor_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
        FactoryUrl: "/api/ml/model/preprocessor/factory")]
    public class Preprocessor : IFormDescriptor
    {
        public Preprocessor()
        {
            Settings = new List<PreprocessorSetting>();
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Preprocessor_ClassName, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources), IsRequired: true)]
        public string ClassName { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.Preprocessor_Settings, FieldType: FieldTypes.ChildListInline, ResourceType: typeof(AIResources))]
        public List<PreprocessorSetting> Settings { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(ClassName),
                nameof(Key),
                nameof(Description),
                nameof(Settings)
            };
        }
    }
}
