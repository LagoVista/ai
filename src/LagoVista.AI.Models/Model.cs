// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 272b5ef02cea4231b8a093cbe0a29c247c7658e66b9b8a56b2d4a72452281699
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Linq;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public enum ModelType
    {
        [EnumLabel(Model.ModelType_PyTorch, AIResources.Names.Model_Type_PyTorch, typeof(AIResources))]
        PyTorch,

        [EnumLabel(Model.ModelType_TF, AIResources.Names.Model_Type_TensorFlow, typeof(AIResources))]
        TensorFlow,

        [EnumLabel(Model.ModelType_TF_Lite, AIResources.Names.Model_Type_TensorFlow_Lite, typeof(AIResources))]
        TensorFlowLite,

        [EnumLabel(Model.ModelType_Onnx, AIResources.Names.Model_Type_Onnx, typeof(AIResources))]
        Onyx,

    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Model_Title, AIResources.Names.Model_Help, AIResources.Names.Model_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        GetUrl: "/api/ml/model/{id}", GetListUrl: "/api/ml/models", FactoryUrl: "/api/ml/model/factory", SaveUrl: "/api/ml/model", DeleteUrl: "/api/ml/model/{id}",
        ListUIUrl: "/mlworkbench/models", EditUIUrl: "/mlworkbench/model/{id}", CreateUIUrl: "/mlworkbench/model/add", Icon: "icon-ae-database-3")]
    public class Model : EntityBase, IDescriptionEntity, IValidateable, IFormDescriptor, IIconEntity, ICategorized, IFormDescriptorCol2
    {
        public const string ModelType_TF = "tensorflow";
        public const string ModelType_TF_Lite = "tensorflow_lite";
        public const string ModelType_PyTorch = "pytorch";
        public const string ModelType_Onnx = "onnx";

        public Model()
        {
            Revisions = new List<ModelRevision>();
            Experiments = new List<Experiment>();
            Notes = new List<ModelNotes>();
            Icon = "icon-ae-database-3";
        }

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_ModelCategory, FieldType: FieldTypes.EntityHeaderPicker, EntityHeaderPickerUrl: "/api/ml/modelcategories", IsRequired: true, WaterMark: AIResources.Names.Model_ModelCategory_Select, ResourceType: typeof(AIResources))]
        public EntityHeader ModelCategory { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_LabelSet, EntityHeaderPickerUrl: "/api/ml/labelsets", HelpResource: AIResources.Names.Model_LabelSet_Help, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public EntityHeader ModelLabelSet { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_Revisions, FactoryUrl: "/api/ml/model/revision/factory", FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<ModelRevision> Revisions { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_ModelType, FieldType: FieldTypes.Picker, EnumType: typeof(ModelType), IsRequired: true, WaterMark: AIResources.Names.Model_Type_Select, ResourceType: typeof(AIResources))]
        public EntityHeader<ModelType> ModelType { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_Experiments, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<Experiment> Experiments { get; set; }

        [FormField(LabelResource: AIResources.Names.ModelRevision_Notes, FieldType: FieldTypes.ChildList, ResourceType: typeof(AIResources))]
        public List<ModelNotes> Notes { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_PreferredRevision, FieldType: FieldTypes.EntityHeaderPicker, IsRequired: false, WaterMark: AIResources.Names.Model_PreferredRevision_Select, ResourceType: typeof(AIResources))]
        public EntityHeader PreferredRevision { get; set; }

        [CustomValidator]
        public void Validate(ValidationResult result)
        {
            if (Revisions.GroupBy(rev => new { rev.VersionNumber, rev.MinorVersionNumber}).Count() != Revisions.Count)
            {
                result.AddUserError("Revision Indexes must be unique.");
            }
        }

        public ModelSummary CreateSummary()
        {
            return new ModelSummary()
            {
                Id = Id,
                Description = Description,
                IsPublic = IsPublic,
                Key = Key,
                Name = Name,
                Category = Category?.Text,
                CategoryId = Category?.Id,
                CategoryKey = Category?.Key,

                Revisions = new List<ModelRevisionSummary>(Revisions.Select(rev => rev.ToSummary()))
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Icon),
                nameof(Category),
                nameof(ModelType),
                nameof(PreferredRevision),
                nameof(Description),
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(Revisions),
                nameof(Experiments),
                nameof(Notes)
            };
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Models_Title, AIResources.Names.Model_Help, AIResources.Names.Model_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
        GetUrl: "/api/ml/model/{id}", GetListUrl: "/api/ml/models", FactoryUrl: "/api/ml/model/factory", SaveUrl: "/api/ml/model", DeleteUrl: "/api/ml/model/{id}",
        ListUIUrl: "/mlworkbench/models", EditUIUrl: "/mlworkbench/model/{id}", CreateUIUrl: "/mlworkbench/model/add", Icon: "icon-ae-database-3")]
    public class ModelSummary : SummaryData
    {
        public List<ModelRevisionSummary> Revisions { get; set; }
    }
}
