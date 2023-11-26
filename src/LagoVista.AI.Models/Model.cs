﻿using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
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
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Model_Title, AIResources.Names.Model_Help, AIResources.Names.Model_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class Model : EntityBase,  IDescriptionEntity, IValidateable, IFormDescriptor
    {
        public const string ModelType_TF = "tensorflow";
        public const string ModelType_TF_Lite = "tensorflow_lite";
        public const string ModelType_PyTorch = "pytorch";

        public Model()
        {
            Revisions = new List<ModelRevision>();
            Experiments = new List<Experiment>();
            Notes = new List<ModelNotes>();
        }

        
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
                Revisions = new List<ModelRevisionSummary>(Revisions.Select(rev => rev.ToSummary()))
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(PreferredRevision),
                nameof(Description),
                nameof(ModelCategory),
                nameof(ModelType),
                nameof(LabelSet),
                nameof(Revisions),
                nameof(Experiments),
                nameof(Notes)
            };
        }
    }

    public class ModelSummary : SummaryData
    {

        public List<ModelRevisionSummary> Revisions { get; set; }
    }
}
