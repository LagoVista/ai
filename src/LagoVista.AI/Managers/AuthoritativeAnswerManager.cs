using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.AuthoritativeAnswers;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    /// <summary>
    /// Business logic for Authoritative Q&A (AQ).
    /// 
    /// V1: contracts + basic normalization + persistence. RAG integration comes next.
    /// </summary>
    public class AuthoritativeAnswerManager : IAuthoritativeAnswerManager
    {
        private readonly IAuthoritativeAnswerRepository _repo;
        private readonly IAdminLogger _logger;

        public AuthoritativeAnswerManager(IAuthoritativeAnswerRepository repo, IAdminLogger logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AuthoritativeAnswerLookupResult> LookupAsync(string orgId, string question, IEnumerable<string> tags = null)
        {
            if (String.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
            if (String.IsNullOrWhiteSpace(question)) throw new ArgumentNullException(nameof(question));

            var normalizedQuestion = NormalizeQuestion(question);

            var matches = await _repo.SearchAsync(orgId, normalizedQuestion, tags);

            if (matches == null || matches.Count == 0)
            {
                return new AuthoritativeAnswerLookupResult
                {
                    Status = AuthoritativeAnswerLookupStatus.NotFound
                };
            }

            // V1 policy: if multiple distinct LLM answers exist, treat as conflict.
            // (We will tighten this with scoring/authority thresholds when RAG is wired in.)
            var distinctAnswers = matches
                .Select(m => (m.LlmAnswer ?? m.HumanAnswer ?? String.Empty).Trim())
                .Where(a => !String.IsNullOrEmpty(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctAnswers.Count > 1)
            {
                return new AuthoritativeAnswerLookupResult
                {
                    Status = AuthoritativeAnswerLookupStatus.Conflict,
                    Conflicts = matches.Select(m => new AuthoritativeAnswerLookupMatch
                    {
                        AqId = m.AqId,
                        Answer = (m.LlmAnswer ?? m.HumanAnswer),
                        SourceRef = m.SourceRef,
                        Confidence = m.Confidence
                    }).ToList()
                };
            }

            var best = matches.First();
            return new AuthoritativeAnswerLookupResult
            {
                Status = AuthoritativeAnswerLookupStatus.Answered,
                Answer = best.LlmAnswer ?? best.HumanAnswer,
                SourceRef = best.SourceRef ?? $"aq:{best.AqId}",
                Confidence = best.Confidence ?? "high"
            };
        }

        public async Task<AuthoritativeAnswerEntry> SaveAsync(string orgId, string question, string answer, IEnumerable<string> tags = null, string confidence = "high")
        {
            if (String.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
            if (String.IsNullOrWhiteSpace(question)) throw new ArgumentNullException(nameof(question));
            if (String.IsNullOrWhiteSpace(answer)) throw new ArgumentNullException(nameof(answer));

            // V1: keep under Table Storage per-property max (64KB). Caller can enforce stricter policy later.
            // If you want app-level hard minimums, we can add them here.
            if (question.Length >= 64000) throw new ArgumentOutOfRangeException(nameof(question), "Question too large for AQ entry.");
            if (answer.Length >= 64000) throw new ArgumentOutOfRangeException(nameof(answer), "Answer too large for AQ entry.");

            var now = DateTime.UtcNow.ToString("o");
            var entry = new AuthoritativeAnswerEntry
            {
                AqId = Guid.NewGuid().ToString("N"),
                OrgId = orgId,
                NormalizedQuestion = NormalizeQuestion(question),
                HumanQuestion = question,
                LlmQuestion = question,
                HumanAnswer = answer,
                LlmAnswer = answer,
                Tags = tags?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
                Confidence = confidence,
                SourceRef = "human",
                CreatedUtc = now,
                UpdatedUtc = now,
            };

            await _repo.UpsertAsync(entry);
            return entry;
        }

        private static string NormalizeQuestion(string question)
        {
            // Minimal deterministic normalization for V1.
            // We'll expand this once retrieval/scoring is wired in.
            return question.Trim();
        }
    }
}
