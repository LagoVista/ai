// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 262b1722d6dc10e77d3cf8b1c9e42d187c6b4f301ac1a69c2fd089cc9978e7fd
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Label_Title, AIResources.Names.Label_Help, AIResources.Names.Label_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
       FactoryUrl: "/api/ml/modellabel/factory", Icon: "icon-fo-nerve")]
    public class ModelLabel : IFormDescriptor
    {
        public ModelLabel()
        {
            Icon = "icon-fo-nerve";
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Key, FieldType: FieldTypes.Key, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Icon, FieldType: FieldTypes.Icon, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Title, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Title { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }


        [FormField(LabelResource: AIResources.Names.Label_Index, FieldType: FieldTypes.Integer, IsRequired: true, ResourceType: typeof(AIResources))]
        public int Index { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Visible, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool Visible { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Enabled, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool Enabled { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Index),
                nameof(Key),
                nameof(Icon),
                nameof(Title),
                nameof(Description),
                nameof(Visible),
                nameof(Enabled)
            };
        }
    }
}
