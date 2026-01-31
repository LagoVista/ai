using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.DdrTla_Catalog, AIResources.Names.DdrTla_Catalog_Help, AIResources.Names.DdrTla_Catalog_Description,
        EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain),

        ClusterKey: "knowledge", ModelType: EntityDescriptionAttribute.ModelTypes.Taxonomy, Shape: EntityDescriptionAttribute.EntityShapes.Entity,
        Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime, Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true,
        IndexTier: EntityDescriptionAttribute.IndexTiers.Secondary, IndexPriority: 60, IndexTagsCsv: "ai,knowledge,tla,catalog")]
    public class DdrTlaCatalog : EntityBase, IValidateable
    {
        public List<DdrTla> Tlas { get; set; } = new List<DdrTla>();
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Ddr_Tla, AIResources.Names.Ddr_Tla_Help, AIResources.Names.Ddr_Tla_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DdrTla : IValidateable
    {
        public string Tla { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public int CurrentIndex { get; set; }
    }
}