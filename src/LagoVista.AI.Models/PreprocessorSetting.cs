﻿using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.PreprocessorSetting_Title, AIResources.Names.PreprocessorSetting_Help, AIResources.Names.PreprocessorSetting_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, 
        typeof(AIResources), FactoryUrl: "/api/ml/model/preprocessor/setting/factory")]
    public class PreprocessorSetting : IFormDescriptor
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.PreprocessorSetting_Value, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
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
                nameof(Description)
            };
        }
    }
}
