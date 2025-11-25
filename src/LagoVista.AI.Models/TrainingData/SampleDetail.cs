// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: d1b2d0bb507de3eb5985a1c8550c4e33dac73cb0dc1eaa4ae13adfd4b277e342
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models.TrainingData
{
    public class SampleDetail : EntityBase, IIDEntity, IKeyedEntity, IOwnedEntity, IAuditableEntity
    {
        public string FileName { get; set; }

        public string ContentType { get; set; }

        public long ContentSize { get; set; }

        public new List<EntityHeader> Labels { get; set; }

        public static SampleDetail FromSample(Sample sample)
        {
            return new SampleDetail()
            {
                CreatedBy = EntityHeader.Create(sample.CreatedById, sample.CreatedByName),
                LastUpdatedBy = EntityHeader.Create(sample.LastUpdatedById, sample.LastUpdatedByName),
                CreationDate = sample.CreationDate,
                LastUpdatedDate = sample.LastUpdatedDate,
                ContentType = sample.ContentType,
                ContentSize = sample.ContentSize,
                Id = sample.RowKey,
                Name = sample.Name,
                FileName = sample.FileName,
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
