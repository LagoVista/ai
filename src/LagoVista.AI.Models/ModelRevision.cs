using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public enum ModelRevisionStatus
    {
        [EnumLabel(ModelRevision.Status_New, AIResources.Names.ModelRevision_Status_New, typeof(AIResources))]
        New,
        [EnumLabel(ModelRevision.Status_Experimental, AIResources.Names.ModelRevision_Status_Experimental, typeof(AIResources))]
        Experimental,
        [EnumLabel(ModelRevision.Status_Alpha, AIResources.Names.ModelRevision_Status_Alpha, typeof(AIResources))]
        Alpha,
        [EnumLabel(ModelRevision.Status_Beta, AIResources.Names.ModelRevision_Status_Beta, typeof(AIResources))]
        Beta,
        [EnumLabel(ModelRevision.Status_Production, AIResources.Names.ModelRevision_Status_Production, typeof(AIResources))]
        Production,
        [EnumLabel(ModelRevision.Status_Obsolete, AIResources.Names.ModelRevision_Status_Obsolete, typeof(AIResources))]
        Obsolete,
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelRevision_Title, AIResources.Names.ModelRevision_Help, AIResources.Names.ModelRevision_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class ModelRevision
    {
        public const string Status_New = "new";
        public const string Status_Experimental = "experimental";
        public const string Status_Alpha = "alpha";
        public const string Status_Beta = "beta";
        public const string Status_Production = "production";
        public const string Status_Obsolete = "obsolete";
        
        [FormField(LabelResource: AIResources.Names.ModelRevision_Version_Number, FieldType: FieldTypes.Integer, IsRequired:true, IsUserEditable:false, ResourceType: typeof(AIResources))]
        public int VersionNumber { get; set; }

        public String Datestamp { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Status, FieldType: FieldTypes.Picker, IsRequired: true, WaterMark: AIResources.Names.ModelRevision_Status_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelRevisionStatus> Status { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Settings, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public String Settings { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Labels, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<Label> Labels { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Notes, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<ModelNotes> Notes { get; set; }
    }
}
