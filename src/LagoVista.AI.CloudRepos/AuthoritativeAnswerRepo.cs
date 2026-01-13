using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    /// <summary>
    /// Table Storage implementation for Authoritative Answers (AQ).
    /// 
    /// V1:
    /// - PartitionKey = OrgId
    /// - RowKey = AqId (globally unique)
    /// 
    /// NOTE: You mentioned org-based scoping at the table-name level in the future.
    /// This implementation keeps OrgId in the entity and uses it as PartitionKey for now.
    /// </summary>
    public class AuthoritativeAnswerRepo : TableStorageBase<AuthoritativeAnswerDTO>, IAuthoritativeAnswerRepository
    {
        public AuthoritativeAnswerRepo(IMLRepoSettings settings, IAdminLogger logger) :
            base(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey, logger)
        {
        }

        public async Task<List<AuthoritativeAnswerEntry>> SearchAsync(string orgId, string normalizedQuestion, IEnumerable<string> tags = null)
        {
            if (String.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
            if (String.IsNullOrWhiteSpace(normalizedQuestion)) throw new ArgumentNullException(nameof(normalizedQuestion));

            // V1: exact match on NormalizedQuestion within the org partition.
            // We'll add better search later (prefix, tag filtering, embeddings, etc.).
            var candidates = await GetByParitionIdAsync(orgId);

            var matches = candidates
                .Where(c => String.Equals(c.NormalizedQuestion, normalizedQuestion, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ToEntry())
                .ToList();

            return matches;
        }

        public Task UpsertAsync(AuthoritativeAnswerEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return InsertAsync(AuthoritativeAnswerDTO.FromEntry(entry));
        }
    }

    public class AuthoritativeAnswerDTO : TableStorageEntity
    {
        public static AuthoritativeAnswerDTO FromEntry(AuthoritativeAnswerEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            return new AuthoritativeAnswerDTO
            {
                PartitionKey = entry.OrgId,
                RowKey = entry.AqId,

                AqId = entry.AqId,
                OrgId = entry.OrgId,
                NormalizedQuestion = entry.NormalizedQuestion,
                HumanQuestion = entry.HumanQuestion,
                LlmQuestion = entry.LlmQuestion,
                HumanAnswer = entry.HumanAnswer,
                LlmAnswer = entry.LlmAnswer,
                Tags = entry.Tags == null ? null : String.Join(",", entry.Tags),
                SourceRef = entry.SourceRef,
                Scope = entry.Scope,
                Confidence = entry.Confidence,
                CreatedUtc = entry.CreatedUtc,
                UpdatedUtc = entry.UpdatedUtc,
            };
        }

        public AuthoritativeAnswerEntry ToEntry()
        {
            return new AuthoritativeAnswerEntry
            {
                AqId = this.AqId,
                OrgId = this.OrgId,
                NormalizedQuestion = this.NormalizedQuestion,
                HumanQuestion = this.HumanQuestion,
                LlmQuestion = this.LlmQuestion,
                HumanAnswer = this.HumanAnswer,
                LlmAnswer = this.LlmAnswer,
                Tags = String.IsNullOrEmpty(this.Tags) ? new List<string>() : this.Tags.Split(',').ToList(),
                SourceRef = this.SourceRef,
                Scope = this.Scope,
                Confidence = this.Confidence,
                CreatedUtc = this.CreatedUtc,
                UpdatedUtc = this.UpdatedUtc,
            };
        }

        public string AqId { get; set; }
        public string OrgId { get; set; }

        public string NormalizedQuestion { get; set; }
        public string HumanQuestion { get; set; }
        public string LlmQuestion { get; set; }

        public string HumanAnswer { get; set; }
        public string LlmAnswer { get; set; }

        public string Tags { get; set; }
        public string SourceRef { get; set; }
        public string Scope { get; set; }
        public string Confidence { get; set; }

        public string CreatedUtc { get; set; }
        public string UpdatedUtc { get; set; }
    }
}
