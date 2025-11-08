// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: b9895db36b8a3281df6309243a7beaf3a587bdc77d5e67b352b7ccb28f23f731
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.CloudRepos.Models
{
    public class ExperimentResultDTO : TableStorageEntity
    {
        public string Id { get; set; }
        public string ModelName { get; set; }
        public string Revision { get; set; }
        public string RevisionId { get; set; }
        public int VersionNumber { get; set; }
        public string ExperimentId { get; set; }
        public string ExperimentName { get; set; }
        public string PerformedBy { get; set; }
        public string PerformedById { get; set; }
        public string Datestamp { get; set; }
        public bool Success { get; set; }
        public int SuccessPercent { get; set; }
        public int Accuracy { get; set; }
        public bool Subjective { get; set; }

        public static string GetPartitionKey(ExperimentResult result)
        {
            return GetPartitionKey(result.Model.Id, result.Revision.Value);
        }

        public static string GetPartitionKey(String modelId, int versionNumber)
        {
            return $"{modelId}-{versionNumber}";
        }

        public static ExperimentResultDTO Create(ExperimentResult result)
        {
            return new ExperimentResultDTO()
            {
                RowKey = DateTime.UtcNow.ToInverseTicksRowKey(),
                PartitionKey = ExperimentResultDTO.GetPartitionKey(result),
                Id = result.Id,
                Revision = result.Revision.Text,
                RevisionId = result.Revision.Id,
                VersionNumber = result.Revision.Value,
                ModelName = result.Model.Text,
                Datestamp = result.Datestamp,
                ExperimentId = result.Experiment.Id,
                ExperimentName = result.Experiment.Text,
                Success = result.Success,
                SuccessPercent = result.SuccessPercent,
                Accuracy = result.Accuracy,
                Subjective = result.Subjective,
                PerformedBy = result.PerformedBy.Text,
                PerformedById = result.PerformedBy.Id,
            };
        }

        public ExperimentResult ToExpermentResult()
        {
            var result = new ExperimentResult()
            {
                Id = Id,
                Datestamp = Datestamp,
                Revision = EntityHeader<int>.Create(VersionNumber),
                Model = EntityHeader.Create(PartitionKey, ModelName),
                Experiment = EntityHeader.Create(ExperimentId, ExperimentName),
                Subjective = Subjective,
                Success = Success,
                Accuracy = Accuracy,
                PerformedBy = EntityHeader.Create(PerformedById, PerformedBy),
            };

            result.Revision.Id = RevisionId;
            result.Revision.Text = Revision;

            return result;
        }
    }
}
