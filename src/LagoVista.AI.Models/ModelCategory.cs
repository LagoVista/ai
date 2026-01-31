// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 751ef6969dfb0e1ddfb535329d33b72b498729090d357aa326e51fdd82e19817
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.ModelCategory_Title, AIResources.Names.ModelCategory_Help, AIResources.Names.ModelCategory_Description,
        EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),

        FactoryUrl: "/api/ml/modelcategory/factory", GetListUrl: "/api/ml/modelcategories", GetUrl: "/api/ml/modelcategory/{id}",
        SaveUrl: "/api/ml/modelcategory", DeleteUrl: "/api/ml/modelcategory/{id}",

        ListUIUrl: "/mlworkbench/settings/categories", EditUIUrl: "/mlworkbench/settings/category/{id}", CreateUIUrl: "/mlworkbench/settings/category/add",

        ClusterKey: "taxonomy", ModelType: EntityDescriptionAttribute.ModelTypes.Taxonomy, Shape: EntityDescriptionAttribute.EntityShapes.Entity,
        Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime, Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true,
        IndexTier: EntityDescriptionAttribute.IndexTiers.Secondary, IndexPriority: 65, IndexTagsCsv: "ai,taxonomy,category")]
    public class ModelCategory : EntityBase, IDescriptionEntity, IValidateable, IFormDescriptor, ISummaryFactory, IIconEntity, ICategorized
    {
        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        public ModelCategorySummary CreateSummary()
        {
            var summary = new ModelCategorySummary();
            summary.Populate(this);
            return summary;
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
            return this.CreateSummary();
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.ModelCategories_Title, AIResources.Names.ModelCategory_Help, AIResources.Names.ModelCategory_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
         GetListUrl: "/api/ml/modelcategories", GetUrl: "/api/ml/modelcategory/{id}", SaveUrl: "/api/ml/modelcategory", FactoryUrl: "/api/ml/modellabel/factory", DeleteUrl: "/api/ml/modelcategory/{id}")]
    public class ModelCategorySummary : SummaryData
    {
    }
}
