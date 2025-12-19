using LagoVista.AI.Models.Resources;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDR_Title, AIResources.Names.DDR_Help, AIResources.Names.DDR_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DetailedDesignReview : EntityBase, IValidateable, ISummaryFactory
    {

        public string Goal { get; set; }

        public string GoalApprovedTimestamp { get; set; }

        public EntityHeader GoalApprovedBy { get; set; }

        public string Notes { get; set; }

        public string Tla { get; set; }

        public int Index { get; set; }

        public string DdrIdentifier { get; set; }

        public string Title { get => Name; }

        public string Summary { get; set; }

        public string Jsonl { get; set; }

        public string Status { get; set; }

        public string StatusTimestamp { get; set; }

        public List<DdrChapter> Chapters { get; set; } = new List<DdrChapter>();

        public string ApprovedTimestamp { get; set; }
        public EntityHeader ApprovedBy { get; set; }

        public string ContentDiscoveryCompletedTimestamp { get; set; }

        public string ContentDiscoverySummary { get; set; }

        public string BaselineResponseId { get; set; }

        public string FullDDRMarkDown { get; set; }

        public List<DdrContentDiscoveryArtifact> ContentDiscoveryArtifacts { get; set; } = new List<DdrContentDiscoveryArtifact>();

        public DetailedDesignReviewSummary CreateSummary()
        {
            var summary = new DetailedDesignReviewSummary();
            summary.Populate(this);
            summary.Summary = Summary;
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

    public class DdrContentDiscoveryArtifact
    {
        public string Id { get; set; }          // RAG asset id or URI
        public string BlobUri { get; set; }
        public string Title { get; set; }       // Human readable
        public string SourceKind { get; set; }  // DDR / Domain / Spec / Code / etc.
        public string Reason { get; set; }      // Why we pulled it in
    }



    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDRs_Title, AIResources.Names.DDR_Help, AIResources.Names.DDR_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DetailedDesignReviewSummary : SummaryData
    {
        public string DdrIdentifier { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string StatusTimestamp { get; set; }

    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.DDR_Chapter, AIResources.Names.DDR_Chapter_Help, AIResources.Names.DDR_Chapter_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIDomain))]
    public class DdrChapter
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
    
        public string Status { get; set; }

        public EntityHeader ApprovedBy { get; set; }
        public string ApprovedTimestamp { get; set; }

        public int DurableSummaryCount { get; set; }
        public string LastDurableSummaryTimestamp { get; set; }

        public List<DdrContentDiscoveryArtifact> ContentDiscoveryArtifacts { get; set; } = new List<DdrContentDiscoveryArtifact>();

    }

}
