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
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.LabelSet_Title, AIResources.Names.LabelSet_Help, AIResources.Names.LabelSet_Help, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        FactoryUrl: "/api/ml/labelset/factory", GetUrl: "/api/ml/labelset/{id}", DeleteUrl: "/api/ml/labelset/{id}", SaveUrl: "/api/ml/labelset", GetListUrl: "/api/ml/labelsets",
        ListUIUrl: "/mlworkbench/settings/labels", CreateUIUrl: "/mlworkbench/settings/label/add",  EditUIUrl: "/mlworkbench/settings/label/{id}", Icon: "icon-pz-text-2")]
    public class ModelLabelSet : EntityBase, IDescriptionEntity, IValidateable, IFormDescriptor, ICategorized, IIconEntity
    {
       
        public ModelLabelSet()
        {
            Labels = new List<ModelLabel>();
            Icon = "icon-pz-text-2";
        }


        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.LabelSet_Labels, FieldType: FieldTypes.ChildListInline, FactoryUrl:  "/api/ml/modellabel/factory", ResourceType: typeof(AIResources))] 
        public List<ModelLabel> Labels { get; set; }

        public ModelLabelSetSummary CreateSummary()
        {
            return new ModelLabelSetSummary()
            {
                Description = Description,
                Id = Id,
                Key = Key,
                IsPublic = IsPublic,
                Name = Name,
                Category = Category?.Text,
                CategoryId = Category?.Id,
                CategoryKey = Category?.Key,
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
                nameof(Description),
                nameof(Labels)
            };
        }
    }


    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.LabelSets_Title, AIResources.Names.LabelSet_Help, AIResources.Names.LabelSet_Help, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
        FactoryUrl: "/api/ml/labelset/factory", GetUrl: "/api/ml/labelset/{id}", DeleteUrl: "/api/ml/labelset/{id}", SaveUrl: "/api/ml/labelset", GetListUrl: "/api/ml/labelsets",
        ListUIUrl: "/mlworkbench/settings/labels", CreateUIUrl: "/mlworkbench/settings/label/add", EditUIUrl: "/mlworkbench/settings/label/{id}", Icon: "icon-pz-text-2")]
    public class ModelLabelSetSummary : SummaryData
    {

    }
}
