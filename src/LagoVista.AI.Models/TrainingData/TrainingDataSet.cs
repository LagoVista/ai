// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 6a89e26d2cfb4ab4fbe1c0b08b48805df52d68cf442e1f5c636e9769ddd9bc9e
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Collections.Generic;

namespace LagoVista.AI.Models.TrainingData
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.TrainingDataSet_Title, AIResources.Names.TrainingDataSet_Help, AIResources.Names.TrainingDataSet_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class TrainingDataSet : EntityBase,  IDescriptionEntity, IValidateable
    {
        public TrainingDataSet()
        {
            Labels = new List<EntityHeader>();
        }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public List<EntityHeader> Labels { get; set; }

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
