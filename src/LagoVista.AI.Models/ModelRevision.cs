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

    public enum ModelQuality
    {
        [EnumLabel(ModelRevision.ModelQuality_Unknown, AIResources.Names.ModelRevision_Quality_Unknown, typeof(AIResources))]
        Unknown,
        [EnumLabel(ModelRevision.ModelQuality_Poor, AIResources.Names.ModelRevision_Quality_Poor, typeof(AIResources))]
        Poor,
        [EnumLabel(ModelRevision.ModelQuality_Medium, AIResources.Names.ModelRevision_Quality_Medium, typeof(AIResources))]
        Medium,
        [EnumLabel(ModelRevision.ModelQuality_Good, AIResources.Names.ModelRevision_Quality_Good, typeof(AIResources))]
        Good,
        [EnumLabel(ModelRevision.ModelQuality_Excellent, AIResources.Names.ModelRevision_Quality_Excellent, typeof(AIResources))]
        Excellent,
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelRevision_Title, AIResources.Names.ModelRevision_Help, AIResources.Names.ModelRevision_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class ModelRevision
    {
        public const string ModelQuality_Unknown = "uknown";
        public const string ModelQuality_Poor = "Poor";
        public const string ModelQuality_Medium = "Medium";
        public const string ModelQuality_Good = "Good";
        public const string ModelQuality_Excellent = "Excellent";

        public const string Status_New = "new";
        public const string Status_Experimental = "experimental";
        public const string Status_Alpha = "alpha";
        public const string Status_Beta = "beta";
        public const string Status_Production = "production";
        public const string Status_Obsolete = "obsolete";

        public ModelRevision()
        {
            Labels = new List<Label>();
            Notes = new List<ModelNotes>();
            Status = EntityHeader<ModelRevisionStatus>.Create(ModelRevisionStatus.New);
            Quality = EntityHeader<ModelQuality>.Create(ModelQuality.Unknown);
        }


        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public String Key { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Version_Number, FieldType: FieldTypes.Integer, IsRequired:true, IsUserEditable:false, ResourceType: typeof(AIResources))]
        public int VersionNumber { get; set; }

        public String Datestamp { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Status, FieldType: FieldTypes.Picker, EnumType:typeof(ModelRevisionStatus), IsRequired: true, WaterMark: AIResources.Names.ModelRevision_Status_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelRevisionStatus> Status { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Quality, FieldType: FieldTypes.Picker, EnumType: typeof(ModelQuality), IsRequired: true, WaterMark: AIResources.Names.ModelRevision_Quality_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelQuality> Quality { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Settings, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public String Settings { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Labels, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<Label> Labels { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Notes, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<ModelNotes> Notes { get; set; }
    }
}
