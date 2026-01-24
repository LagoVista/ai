// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 5140eac6a0e90a0326d64aef721b423ec8c62bbf589684a193b6fa1ad96dc863
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using Newtonsoft.Json;
using System;
using LagoVista.Core;
using System.Collections.Generic;
using LagoVista.Core.Validation;
using System.Text.RegularExpressions;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI.Models
{
    public enum ModelRevisionStatus
    {
        [EnumLabel(AiModelRevision.Status_New, AIResources.Names.ModelRevision_Status_New, typeof(AIResources))]
        New,
        [EnumLabel(AiModelRevision.Status_Experimental, AIResources.Names.ModelRevision_Status_Experimental, typeof(AIResources))]
        Experimental,
        [EnumLabel(AiModelRevision.Status_Alpha, AIResources.Names.ModelRevision_Status_Alpha, typeof(AIResources))]
        Alpha,
        [EnumLabel(AiModelRevision.Status_Beta, AIResources.Names.ModelRevision_Status_Beta, typeof(AIResources))]
        Beta,
        [EnumLabel(AiModelRevision.Status_Production, AIResources.Names.ModelRevision_Status_Production, typeof(AIResources))]
        Production,
        [EnumLabel(AiModelRevision.Status_Obsolete, AIResources.Names.ModelRevision_Status_Obsolete, typeof(AIResources))]
        Obsolete,
    }

    public enum ModelQuality
    {
        [EnumLabel(AiModelRevision.ModelQuality_Unknown, AIResources.Names.ModelRevision_Quality_Unknown, typeof(AIResources))]
        Unknown,
        [EnumLabel(AiModelRevision.ModelQuality_Poor, AIResources.Names.ModelRevision_Quality_Poor, typeof(AIResources))]
        Poor,
        [EnumLabel(AiModelRevision.ModelQuality_Medium, AIResources.Names.ModelRevision_Quality_Medium, typeof(AIResources))]
        Medium,
        [EnumLabel(AiModelRevision.ModelQuality_Good, AIResources.Names.ModelRevision_Quality_Good, typeof(AIResources))]
        Good,
        [EnumLabel(AiModelRevision.ModelQuality_Excellent, AIResources.Names.ModelRevision_Quality_Excellent, typeof(AIResources))]
        Excellent,
    }

    public enum InputType
    {
        [EnumLabel(AiModelRevision.InputType_Image, AIResources.Names.InputType_Image, typeof(AIResources))]
        Image,
        [EnumLabel(AiModelRevision.InputType_DataPoints, AIResources.Names.InputType_DataPoints, typeof(AIResources))]
        DataPoints,
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelRevision_Title, AIResources.Names.ModelRevision_Help, 
        AIResources.Names.ModelRevision_Description,EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class AiModelRevision : IFormDescriptor
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

        public const string InputType_Image = "image";
        public const string InputType_DataPoints = "datapoints";

        public AiModelRevision()
        {
            Id = Guid.NewGuid().ToId();
            Labels = new List<ModelLabel>();
            Notes = new List<ModelNotes>();
            Preprocessors = new List<Preprocessor>();
            Status = EntityHeader<ModelRevisionStatus>.Create(ModelRevisionStatus.New);
            Quality = EntityHeader<ModelQuality>.Create(ModelQuality.Unknown);
            Datestamp = DateTime.UtcNow.ToJSONString();
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public String Key { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Version_Number, FieldType: FieldTypes.Integer, IsRequired: true, IsUserEditable: false, ResourceType: typeof(AIResources))]
        public int VersionNumber { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Minor_Version_Number, FieldType: FieldTypes.Integer, IsRequired: true, IsUserEditable: false, ResourceType: typeof(AIResources))]
        public int MinorVersionNumber { get; set; }

        public String Datestamp { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_ModelFile, IsFileUploadImage:false, FieldType: FieldTypes.FileUpload, IsRequired: true, ResourceType: typeof(AIResources))]
        public String FileName { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_InputShape, HelpResource: AIResources.Names.ModelRevision_InputShape_Help, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string InputShape { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_TrainingAccuracy, FieldType: FieldTypes.Decimal, ResourceType: typeof(AIResources))]
        public decimal TrainingAccuracy { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_ValidationAccuracy, FieldType: FieldTypes.Decimal, ResourceType: typeof(AIResources))]
        public decimal ValidationAccuracy { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_InputType, FieldType: FieldTypes.Picker, EnumType: typeof(InputType), IsRequired: true, WaterMark: AIResources.Names.ModelRevision_InputType_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<InputType> InputType { get; set; }


        [FormField(LabelResource: AIResources.Names.ModelRevision_Status, FieldType: FieldTypes.Picker, EnumType: typeof(ModelRevisionStatus), IsRequired: true, WaterMark: AIResources.Names.ModelRevision_Status_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelRevisionStatus> Status { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Quality, FieldType: FieldTypes.Picker, EnumType: typeof(ModelQuality), IsRequired: true, WaterMark: AIResources.Names.ModelRevision_Quality_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelQuality> Quality { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_TrainingSettings, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string TrainingSettings { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Settings, FieldType: FieldTypes.ChildListInline, ResourceType: typeof(AIResources))]
        public List<ModelSetting> Settings { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_LabelSet, HelpResource: AIResources.Names.ModelRevision_LabelSet_Help, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public EntityHeader LabelSet { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Labels, FieldType: FieldTypes.ChildListInline, ResourceType: typeof(AIResources))]
        public List<ModelLabel> Labels { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Notes, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<ModelNotes> Notes { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Preprocessors, FieldType: FieldTypes.ChildListInline, FactoryUrl: "/api/ml/model/preprocessor/factory", ResourceType: typeof(AIResources))]
        public List<Preprocessor> Preprocessors { get; set; }

        [CustomValidator]
        public void Validate(ValidationResult result)
        {
            if (!String.IsNullOrEmpty(InputShape))
            {
                var regEx = new Regex(@"^[0-9,]+$");
                if (!regEx.Match(InputShape).Success)
                {
                    result.AddUserError("Please enter a valid input shape, this should be a comma delimited set of integers that represent the dimmensions of the input.");
                }
            }
        }

        public ModelRevisionSummary ToSummary()
        {
            return new ModelRevisionSummary()
            {
                Id = Id,
                VersionNumber = VersionNumber,
                MinorVersionNumber = MinorVersionNumber,
                Datestamp = Datestamp,
                Status = Status.Text,
                StatusId = Status.Id,
                Quality = Quality.Text,
                QualityId = Quality.Id,
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
              nameof(Name),
              nameof(Key),
              nameof(FileName),
              nameof(VersionNumber),
              nameof(MinorVersionNumber),
              nameof(InputShape),
              nameof(TrainingAccuracy),
              nameof(ValidationAccuracy),
              nameof(InputType),
              nameof(Status),
              nameof(Quality),
              nameof(LabelSet),
              nameof(TrainingSettings),
              nameof(Labels),
              nameof(Settings),
              nameof(Preprocessors),
              nameof(Notes),
            };
        }
    }

    public class ModelRevisionSummary
    {
        public String Id { get; set; }
        public int VersionNumber { get; set; }
        public int MinorVersionNumber { set; get; }
        public String Datestamp { get; set; }
        public String Status { get; set; }
        public String StatusId { get; set; }
        public String Quality { get; set; }
        public String QualityId { get; set; }
    }
}
