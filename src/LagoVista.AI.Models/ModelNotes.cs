﻿using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelNotes_Title, AIResources.Names.ModelNotes_Help, AIResources.Names.ModelNotes_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class ModelNotes
    {
        [FormField(LabelResource: AIResources.Names.ModelRevision_DateStamp, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Datestamp { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelNotes_AddedBy, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public EntityHeader AddedBy { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Notes, FieldType: FieldTypes.MultiLineText, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Note { get; set; }
    }
}