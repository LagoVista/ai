using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models.TrainingData
{
    public class SampleDetail : IIDEntity, IKeyedEntity, IOwnedEntity, IAuditableEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }

        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public EntityHeader CreatedBy { get; set; }
        public EntityHeader LastUpdatedBy { get; set; }
        public bool IsPublic { get; set; }

        public List<EntityHeader> Labels { get; set; }

        public static SampleDetail FromSample(Sample sample)
        {
            return new SampleDetail()
            {
                CreatedBy = EntityHeader.Create(sample.CreatedById, sample.CreatedByName),
                LastUpdatedBy = EntityHeader.Create(sample.LastUpdatedById, sample.LastUpdatedByName),
                CreationDate = sample.CreationDate,
                LastUpdatedDate = sample.LastUpdatedDate,
                Id = sample.RowKey,
                Name = sample.Name,
                Key = sample.Key,
                OwnerOrganization = EntityHeader.Create(sample.OwnerOrganizationId, sample.OwnerOrganizationName),
            };
        }
    }

    public class SampleSummary
    {
        public string SampleId { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
        public string CreationDate { get; set; }
        public string ContentType { get; set; }
        public long ContentSize { get; set; }

        public static SampleSummary FromSampleLabel(SampleLabel lbl)
        {
            return new SampleSummary()
            {
                ContentSize = lbl.ContentSize,
                ContentType = lbl.ContentType,
                CreationDate = lbl.CreationDate,
                FileName = lbl.FileName,
                Name = lbl.Name,
                SampleId = lbl.SampleId
            };
        }
    }
}
