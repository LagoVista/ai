using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    /// <summary>
    /// Stores chapter archives (serialized turn payloads) as blobs.
    /// This keeps AgentSession small while making chapter reset deterministic.
    /// </summary>
    public class AgentSessionTurnArchiveStore : CloudFileStorage, IAgentSessionTurnArchiveStore
    {
        private readonly IAdminLogger _adminLogger;

        public AgentSessionTurnArchiveStore(IMLRepoSettings settings, IAdminLogger adminLogger)
            : base(settings.MLBlobStorage.AccountId, settings.MLBlobStorage.AccessKey, adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        private static string GetContainerName(string orgId) => $"agentsessions{orgId}".ToLowerInvariant();

        private static string BuildArchivePath(string orgId, string sessionId, int chapterIndex, string archiveId)
            => $"{orgId}/sessions/{sessionId}/archives/chapter-{chapterIndex:0000}/{archiveId}.json".ToLowerInvariant();

        private sealed class ArchivePayload
        {
            public string SessionId { get; set; }
            public int ChapterIndex { get; set; }
            public string Title { get; set; }
            public string CreationDate { get; set; }
            public int TurnCount { get; set; }
            public List<AgentSessionTurn> Turns { get; set; }
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public async Task<AgentSessionArchive> SaveAsync(AgentSession session, IReadOnlyList<AgentSessionTurn> turns, string title, EntityHeader user, CancellationToken ct = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turns == null) throw new ArgumentNullException(nameof(turns));

            var archive = new AgentSessionArchive
            {
                Id = Guid.NewGuid().ToId(),
                ChapterIndex = session.CurrentChapterIndex,
                Title = title,
                CreationDate = DateTime.UtcNow.ToString("o"),
                CreatedBy = user,
                TurnCount = turns.Count,
            };

            if (turns.Count > 0)
            {
                archive.FirstTurnId = turns[0]?.Id;
                archive.LastTurnId = turns[turns.Count - 1]?.Id;

                archive.FirstOpenAIResponseId = turns[0]?.OpenAIResponseId;
                archive.LastOpenAIResponseId = turns[turns.Count - 1]?.OpenAIResponseId;
            }

            var payload = new ArchivePayload
            {
                SessionId = session.Id,
                ChapterIndex = session.CurrentChapterIndex,
                Title = title,
                CreationDate = archive.CreationDate,
                TurnCount = turns.Count,
                Turns = new List<AgentSessionTurn>(turns)
            };

            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            archive.ContentSha256 = ComputeSha256Hex(json);

            var container = GetContainerName(session.OwnerOrganization?.Id ?? session.OwnerOrganization.Id);
            var blobKey = BuildArchivePath(session.OwnerOrganization?.Id ?? session.OwnerOrganization.Id, session.Id, session.CurrentChapterIndex, archive.Id);

            var uriResult = await AddFileAsync(container, blobKey, json);
            if (!uriResult.Successful)
            {
                throw new Exception($"Failed to save archive blob: {uriResult.Errors?[0]?.Message}");
            }

            archive.BlobKey = blobKey;
            archive.BlobUrl = uriResult.Result?.ToString();

            _adminLogger.Trace($"[AgentSessionTurnArchiveStore] Saved archive {archive.Id} for session {session.Id} chapter {session.CurrentChapterIndex} turns={turns.Count} sha={archive.ContentSha256}");

            return archive;
        }

        public async Task<IReadOnlyList<AgentSessionTurn>> LoadAsync(AgentSessionArchive archive, CancellationToken ct = default)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            if (string.IsNullOrWhiteSpace(archive.BlobKey)) throw new ArgumentNullException(nameof(archive.BlobKey));

            // NOTE: We do not have orgId/sessionId here; BlobKey includes orgId in the path.
            // Container name is not derivable from BlobKey alone, so v1 requires BlobUrl OR caller-provided orgId.
            // For now, prefer BlobUrl usage in UI; server-side load can be added when needed.
            throw new NotImplementedException("LoadAsync requires org/container context; implement when needed.");
        }

        private static string ComputeSha256Hex(string text)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
