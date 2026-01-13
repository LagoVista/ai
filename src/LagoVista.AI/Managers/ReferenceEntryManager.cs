using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class ReferenceEntryManager : ManagerBase, IReferenceEntryManager
    {
        private readonly IReferenceEntryRepo _repo;

        public ReferenceEntryManager(IReferenceEntryRepo repo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
            : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public static string Sha256Hex(string input)
        {
            if (input == null) return null;

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        public async Task<InvokeResult> AddReferenceEntryAsync(ReferenceEntry referenceEntry, EntityHeader org, EntityHeader user)
        {
            if (referenceEntry == null) throw new ArgumentNullException(nameof(referenceEntry));

            if(String.IsNullOrEmpty(Sha256Hex(referenceEntry.NormalizedModelQuestion)))
            {
                var q = referenceEntry.ModelQuestion;
                q = q.Trim();
                q = q.Replace("`", "");
                q = Regex.Replace(q, @"\s+", " ");
                q = q.ToLowerInvariant();

                referenceEntry.NormalizedModelQuestion = q;
                referenceEntry.NormalizedModelQuestionHash = Sha256Hex(referenceEntry.NormalizedModelQuestion);
            }

            if(String.IsNullOrEmpty(referenceEntry.Name))
            {
                referenceEntry.Name = referenceEntry.NormalizedModelQuestion;
            }

            ValidationCheck(referenceEntry, Actions.Create);
            await AuthorizeAsync(referenceEntry, AuthorizeResult.AuthorizeActions.Create, user, org);

            Enrich(referenceEntry);

            await _repo.AddReferenceEntryAsync(referenceEntry);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> UpdateReferenceEntryAsync(ReferenceEntry referenceEntry, EntityHeader org, EntityHeader user)
        {
            if (referenceEntry == null) throw new ArgumentNullException(nameof(referenceEntry));

            ValidationCheck(referenceEntry, Actions.Update);
            await AuthorizeAsync(referenceEntry, AuthorizeResult.AuthorizeActions.Update, user, org);

            Enrich(referenceEntry);

            await _repo.UpdateReferenceEntryAsync(referenceEntry);
            return InvokeResult.Success;
        }

        public async Task<ReferenceEntry> GetReferenceEntryAsync(string id, EntityHeader org, EntityHeader user)
        {
            var entry = await _repo.GetReferenceEntryAsync(id);
            await AuthorizeAsync(entry, AuthorizeResult.AuthorizeActions.Read, user, org);
            return entry;
        }

        public async Task<InvokeResult> DeleteReferenceEntryAsync(string id, EntityHeader org, EntityHeader user)
        {
            var entry = await _repo.GetReferenceEntryAsync(id);
            await AuthorizeAsync(entry, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await _repo.DeleteReferenceEntryAsync(id);
            return InvokeResult.Success;
        }

        public async Task<ListResponse<ReferenceEntrySummary>> GetReferenceEntriesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(ReferenceEntry));
            return await _repo.GetReferenceEntrySummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<AuthoritativeAnswerLookupResult> LookupAsync(string orgId, string modelQuestion)
        {
            if (String.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
            if (String.IsNullOrWhiteSpace(modelQuestion)) throw new ArgumentNullException(nameof(modelQuestion));

            var normalized = Normalize(modelQuestion);
            var hash = ComputeSha256Hex(normalized);

            var matches = await _repo.FindByNormalizedModelQuestionHashAsync(orgId, hash);
            if (matches == null || matches.Count == 0)
            {
                return new AuthoritativeAnswerLookupResult { Status = AuthoritativeAnswerLookupStatus.NotFound };
            }

            // Only consider active entries.
            var active = matches.Where(m => m.IsActive).ToList();
            if (active.Count == 0)
            {
                return new AuthoritativeAnswerLookupResult { Status = AuthoritativeAnswerLookupStatus.NotFound };
            }

            // Conflict if multiple distinct model answers exist.
            var distinctAnswers = active
                .Select(m => (m.ModelAnswer ?? m.HumanAnswer ?? String.Empty).Trim())
                .Where(a => !String.IsNullOrEmpty(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctAnswers.Count > 1)
            {
                return new AuthoritativeAnswerLookupResult
                {
                    Status = AuthoritativeAnswerLookupStatus.Conflict,
                    Conflicts = active.Select(m => new AuthoritativeAnswerLookupMatch
                    {
                        AqId = m.Id,
                        Answer = (m.ModelAnswer ?? m.HumanAnswer),
                        SourceRef = m.SourceRef,
                        Confidence = m.AnswerConfidence?.Id.ToString().ToLowerInvariant() ?? "unknown"
                    }).ToList()
                };
            }

            var best = active.First();
            return new AuthoritativeAnswerLookupResult
            {
                Status = AuthoritativeAnswerLookupStatus.Answered,
                Answer = best.ModelAnswer ?? best.HumanAnswer,
                SourceRef = best.SourceRef ?? $"ref:{best.ReferenceIdentifier}",
                Confidence = best.AnswerConfidence?.Id.ToString().ToLowerInvariant() ?? "high"
            };
        }

        private void Enrich(ReferenceEntry entry)
        {
            // Normalize + hash
            if (!String.IsNullOrWhiteSpace(entry.ModelQuestion))
            {
                entry.NormalizedModelQuestion = Normalize(entry.ModelQuestion);
                entry.NormalizedModelQuestionHash = ComputeSha256Hex(entry.NormalizedModelQuestion);
            }

            // AppliesTo inference (only if empty)
            if (entry.AppliesTo == null || entry.AppliesTo.Count == 0)
            {
                entry.AppliesTo = ExtractAppliesToTokens(entry.ModelQuestion ?? entry.HumanQuestion, entry.ModelAnswer ?? entry.HumanAnswer);
            }

            // Defaults (low friction)
            if (entry.AnswerSource == null)
                entry.AnswerSource = EntityHeader<ReferenceEntrySource>.Create(ReferenceEntrySource.UserProvided);

            if (entry.AnswerConfidence == null)
                entry.AnswerConfidence = EntityHeader<ReferenceEntryConfidence>.Create(ReferenceEntryConfidence.High);

            if (entry.MetadataQuality == null)
                entry.MetadataQuality = EntityHeader<ReferenceEntryMetadataQuality>.Create(ReferenceEntryMetadataQuality.Medium);

            if (String.IsNullOrEmpty(entry.SourceRef))
                entry.SourceRef = "human";

            if (entry.Scope == null)
                entry.Scope = new List<string>();
        }

        private static string Normalize(string input)
        {
            if (input == null) return null;

            var q = input.Trim();
            q = q.Replace("`", String.Empty);
            q = Regex.Replace(q, @"\s+", " ");
            q = q.ToLowerInvariant();
            return q;
        }

        private static List<string> ExtractAppliesToTokens(string question, string answer)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var text = (question ?? String.Empty) + "\n" + (answer ?? String.Empty);

            foreach (Match match in Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+\b"))
                tokens.Add(match.Value);

            foreach (Match match in Regex.Matches(text, @"\b[A-Z][A-Za-z0-9_]{2,}\b"))
                tokens.Add(match.Value);

            return tokens.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        }

        private static string ComputeSha256Hex(string input)
        {
            if (input == null) return null;

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }


    }
}
