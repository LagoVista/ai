using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.LabelSet_Title, AIResources.Names.LabelSet_Help, AIResources.Names.LabelSet_Help, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
        FactoryUrl: "/api/ml/labelset/factory", GetUrl: "/api/ml/labelset/{id}", DeleteUrl: "/api/ml/labelset/{id}", SaveUrl: "/api/ml/labelset", GetListUrl: "/api/ml/labelsets")]
    public class ModelLabelSet : EntityBase, IDescriptionEntity, IValidateable, IFormDescriptor
    {
       
        public ModelLabelSet()
        {
            Labels = new List<ModelLabel>();
        }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }


        [FormField(LabelResource: AIResources.Names.LabelSet_Labels, FieldType: FieldTypes.ChildListInline, ResourceType: typeof(AIResources))] 
        public List<ModelLabel> Labels { get; set; }

        public ModelLabelSetSummary CreateSummary()
        {
            return new ModelLabelSetSummary()
            {
                Description = Description,
                Id = Id,
                Key = Key,
                IsPublic = IsPublic,
                Name = Name
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Description),
                nameof(Labels)
            };
        }
    }

    public class ModelLabelSetSummary : SummaryData
    {

    }
}
