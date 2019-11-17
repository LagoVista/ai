using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class Label
    {
        [FormField(LabelResource: AIResources.Names.Label_Title, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Title { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Index, FieldType: FieldTypes.Integer, IsRequired: true, ResourceType: typeof(AIResources))]
        public int Index { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Key, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Icon, FieldType: FieldTypes.Text, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }
    }
}
