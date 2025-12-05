using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDR_Title, AIResources.Names.DDR_Help, AIResources.Names.DDR_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DetailedDesignReview : EntityBase, IValidateable, ISummaryFactory
    {

        public string Goal { get; set; }

        public string GoalApprovedTimestamp { get; set; }

        public EntityHeader GoalApprovedBy { get; set; }

        public string Tla { get; set; }

        public int Index { get; set; }

        public string DdrIdentifier { get; set; }

        public string Status { get; set; }

        public string StatusTimestamp { get; set; }

        public List<DdrChapter> Chapters { get; set; }

        public string ApprovedTimestamp { get; set; }
        public EntityHeader ApprovedBy { get; set; }

        public DetailedDesignReviewSummary CreateSummary()
        {
            var summary = new DetailedDesignReviewSummary();
            summary.Status = Status;
            summary.StatusTimestamp = StatusTimestamp;
            summary.DdrIdentifier = DdrIdentifier;
            return summary;
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return this.CreateSummary();
        }
    }


    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDRs_Title, AIResources.Names.DDR_Help, AIResources.Names.DDR_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DetailedDesignReviewSummary : SummaryData
    {

        public string DdrIdentifier { get; set; }

        public string Status { get; set; }

        public string StatusTimestamp { get; set; }

    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDR_Chapter, AIResources.Names.DDR_Chapter_Help, AIResources.Names.DDR_Chapter_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DdrChapter
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
    
        public EntityHeader ApprovedBy { get; set; }
        public string ApprovedTimestamp { get; set; }
    }

}
