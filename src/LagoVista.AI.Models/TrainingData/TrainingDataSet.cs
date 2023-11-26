using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models.TrainingData
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.TrainingDataSet_Title, AIResources.Names.TrainingDataSet_Help, AIResources.Names.TrainingDataSet_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class TrainingDataSet : EntityBase,  IDescriptionEntity, INoSQLEntity, IValidateable, IFormDescriptor
    {

        public TrainingDataSet()
        {
            Labels = new List<EntityHeader>();
        }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public List<EntityHeader> Labels { get; set; }

        [FormField(LabelResource: AIResources.Names.Model_LabelSet, EntityHeaderPickerUrl: "/api/ml/labelsets", HelpResource: AIResources.Names.Model_LabelSet_Help, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public EntityHeader ModelLabelSet { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(LabelSet),
                nameof(Description),
            };
        }

        public TrainingDataSetSummary GetSummary()
        {
            return new TrainingDataSetSummary()
            {
                Id = Id,
                Description = Description,
                IsPublic = IsPublic,
                Key = Key,
                Name = Name
            };
        }
    }

    public class TrainingDataSetSummary : SummaryData
    {

    }
}
