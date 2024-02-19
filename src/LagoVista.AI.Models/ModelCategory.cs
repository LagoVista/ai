using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelCategory_Title, AIResources.Names.ModelCategory_Help, AIResources.Names.ModelCategory_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
        FactoryUrl: "/api/ml/modellabel/factory", GetListUrl: "/api/ml/modelcategories", GetUrl: "/api/ml/modelcategory/{id}", SaveUrl: "/api/ml/modelcategory", DeleteUrl: "/api/ml/modelcategory/{id}",
        ListUIUrl: "/mlworkbench/settings/categories", EditUIUrl: "/mlworkbench/settings/category/{id}", CreateUIUrl: "/mlworkbench/settings/category/add")]
    public class ModelCategory : EntityBase, IDescriptionEntity, IValidateable, IFormDescriptor, ISummaryFactory, IIconEntity, ICategorized
    {
        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Category, FieldType: FieldTypes.Category, WaterMark: AIResources.Names.Common_SelectCategory, ResourceType: typeof(AIResources), IsRequired: false, IsUserEditable: true)]
        public EntityHeader Category { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public ModelCategorySummary CreateSummary()
        {
            return new ModelCategorySummary()
            {
                Description = Description,
                Id = Id,
                IsPublic = IsPublic,
                Key = Key,
                Name = Name,
                Category = Category
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
            };
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return CreateSummary();
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelCategories_Title, AIResources.Names.ModelCategory_Help, AIResources.Names.ModelCategory_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
         GetListUrl: "/api/ml/modelcategories", GetUrl: "/api/ml/modelcategory/{id}", SaveUrl: "/api/ml/modelcategory", FactoryUrl: "/api/ml/modellabel/factory", DeleteUrl: "/api/ml/modelcategory/{id}")]
    public class ModelCategorySummary : CategorizedSummaryData
    {
    }
}
