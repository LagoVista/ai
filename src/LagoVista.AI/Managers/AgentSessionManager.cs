using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class AgentSessionManager : ManagerBase, IAgentSessionManager
    {
        private readonly IAgentSessionRepo _repo;
        private readonly IAdminLogger _adminLogger;        
        private readonly IAgentSessionTurnChapterStore _chapterStore;

        public AgentSessionManager(
            IAgentSessionRepo repo,
            IAdminLogger logger,
            IAppConfig appConfig,
            IAgentSessionTurnChapterStore archiveStore,
            IDependencyManager dependencyManager,
            ISecurity security) : base(logger, appConfig, dependencyManager, security)
        {
            _adminLogger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _chapterStore = archiveStore ?? throw new ArgumentNullException(nameof(archiveStore));
        }

        public async Task AddAgentSessionAsync(AgentSession session, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(session, Actions.Create);

            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddSessionAsync(session);
        }

        public async Task<AgentSession> GetAgentSessionAsync(string agentSessionId, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Read, user, org);
            return session;
        }

        public async Task<ListResponse<AgentSessionSummary>> GetAgentSessionsAsync(ListRequest listRequest, EntityHeader org, EntityHeader user)
        {
            var sessions = await _repo.GetSessionSummariesForOrgAsync(org.Id, listRequest);
            await AuthorizeOrgAccessAsync(user.Id, org.Id, typeof(AgentSessionSummary));
            return sessions;
        }

        public async Task<ListResponse<AgentSessionSummary>> GetAgentSessionsForUserAsync(string userId, ListRequest listRequest, EntityHeader org, EntityHeader user)
        {
            var sessions = await _repo.GetSessionSummariesForUserAsync(org.Id, userId, listRequest);
            await AuthorizeOrgAccessAsync(user.Id, org.Id, typeof(AgentSessionSummary));
            return sessions;
        }

        public async Task<InvokeResult<AgentSessionSummary>> SetSessionNameAsync(string sessionid, string name, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionid, org, user);
            session.Name = name;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedBy = user;
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionSummary>.Create(session.CreateSummary());
        }

        public async Task<InvokeResult<AgentSessionSummary>> ArchiveSessionAsync(string sessionid, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionid, org, user);
            session.Archived = true;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedBy = user;
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionSummary>.Create(session.CreateSummary());
        }

        public async Task<InvokeResult<AgentSessionSummary>> ShareSessionAsync(string sessionid, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionid, org, user);
            session.Shared = true;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedBy = user;
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionSummary>.Create(session.CreateSummary());
        }

        public async Task<InvokeResult<AgentSessionSummary>> DeleteSessionAsync(string sessionid, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionid, org, user);
            session.IsDeleted = true;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.DeletionDate = session.LastUpdatedDate;
            session.LastUpdatedBy = user;
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionSummary>.Create(session.CreateSummary());
        }

        public async Task<InvokeResult<AgentSessionSummary>> CompleteSessionAsync(string sessionId, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            session.Completed = true;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedBy = user;
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionSummary>.Create(session.CreateSummary());
        }

        private static string ExtractKfrValue(AgentSession session, string mode, KfrKind kind)
        {
            if (session == null) return null;
            if (string.IsNullOrWhiteSpace(mode)) return null;
            if (session.Kfrs == null) return null;

            if (!session.Kfrs.ContainsKey(mode)) return null;

            var entries = session.Kfrs[mode] ?? new List<AgentSessionKfrEntry>();
            var entry = entries.FirstOrDefault(e => e != null && e.IsActive && e.Kind == kind);
            return entry?.Value;
        }

        private static List<string> ExtractKfrValues(AgentSession session, string mode, KfrKind kind)
        {
            if (session == null) return new List<string>();
            if (string.IsNullOrWhiteSpace(mode)) return new List<string>();
            if (session.Kfrs == null) return new List<string>();

            if (!session.Kfrs.ContainsKey(mode)) return new List<string>();

            var entries = session.Kfrs[mode] ?? new List<AgentSessionKfrEntry>();
            return entries
                .Where(e => e != null && e.IsActive && e.Kind == kind && !string.IsNullOrWhiteSpace(e.Value))
                .Select(e => e.Value)
                .ToList();
        }

        public async Task<InvokeResult<AgentSession>> RollbackAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if (turn == null)
                return InvokeResult<AgentSession>.FromError($"Turn '{turnId}' not found in session '{sessionId}'.");

            var cloned = JsonConvert.DeserializeObject<AgentSessionTurn>(
                JsonConvert.SerializeObject(turn)
            );

            cloned.Id = Guid.NewGuid().ToId();
            cloned.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.RolledBackTurn);
            cloned.StatusTimeStamp = DateTime.UtcNow.ToJSONString();
            cloned.InstructionSummary = $"Rolled Back to on {DateTime.UtcNow.ToJSONString()}\r\n{turn.InstructionSummary}";
            cloned.OriginalInstructions = turn.OriginalInstructions;

            if (session.Mode != cloned.Mode)
            {
                session.ModeHistory.Add(new ModeHistory()
                {
                    NewMode = cloned.Mode,
                    PreviousMode = session.Mode,
                    TimeStamp = cloned.StatusTimeStamp,
                    Reason = "session rolled back"
                });
            }

            session.Turns.Add(cloned);

            await UpdateSessionAsync(session, org, user);

            return InvokeResult<AgentSession>.Create(session);
        }

        public async Task<InvokeResult<AgentSession>> BranchSessionAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user)
        {
            var sourceSession = await GetAgentSessionAsync(sessionId, org, user);

            var clonedSession = JsonConvert.DeserializeObject<AgentSession>(JsonConvert.SerializeObject(sourceSession));
            if (clonedSession == null) throw new InvalidOperationException("Failed to clone session.");

            var branchedTurn = clonedSession.Turns.SingleOrDefault(t => t.Id == turnId);
            if (branchedTurn == null) throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);

            var lastTurnIndex = clonedSession.Turns.IndexOf(branchedTurn);

            // Remove turns AFTER the anchor turn, on the CLONE.
            for (var idx = clonedSession.Turns.Count - 1; idx > lastTurnIndex; idx--)
                clonedSession.Turns.RemoveAt(idx);

            clonedSession.Id = Guid.NewGuid().ToId();
            clonedSession.Name = $"Branch - {sourceSession.Name}";
            clonedSession.LastUpdatedBy = user;
            clonedSession.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            clonedSession.Mode = branchedTurn.Mode;

            clonedSession.SourceSessionId = null;
            clonedSession.SourceCheckpointId = null;
            clonedSession.SourceTurnSourceId = null;
            clonedSession.RestoreOperationId = null;
            clonedSession.RestoredOnUtc = null;

            await AddAgentSessionAsync(clonedSession, org, user);

            return InvokeResult<AgentSession>.Create(clonedSession);
        }

        public async Task<InvokeResult<AgentSessionCheckpoint>> AddSessionCheckpointAsync(string sessionId, AgentSessionCheckpoint checkpoint, EntityHeader org, EntityHeader user)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return InvokeResult<AgentSessionCheckpoint>.FromError("AddSessionCheckpointAsync requires sessionId.");
            if (checkpoint == null) return InvokeResult<AgentSessionCheckpoint>.FromError("AddSessionCheckpointAsync requires a checkpoint.");
            if (string.IsNullOrWhiteSpace(checkpoint.Name)) return InvokeResult<AgentSessionCheckpoint>.FromError("Checkpoint requires Name.");
            if (string.IsNullOrWhiteSpace(checkpoint.TurnSourceId)) return InvokeResult<AgentSessionCheckpoint>.FromError("Checkpoint requires a TurnSourceId.");

            var session = await GetAgentSessionAsync(sessionId, org, user);
            if (session == null) return InvokeResult<AgentSessionCheckpoint>.FromError("Session not found.");

            session.Checkpoints ??= new List<AgentSessionCheckpoint>();

            checkpoint.Name = checkpoint.Name.Trim();
            checkpoint.CheckpointId = string.IsNullOrWhiteSpace(checkpoint.CheckpointId) ? NextCheckpointId(session.Checkpoints) : checkpoint.CheckpointId.Trim();
            checkpoint.CreationDate = string.IsNullOrWhiteSpace(checkpoint.CreationDate) ? DateTime.UtcNow.ToString("o") : checkpoint.CreationDate;
            checkpoint.CreatedByUser ??= user;
            checkpoint.SessionId ??= session.Turns?.LastOrDefault()?.SessionId;

            session.Checkpoints.Add(checkpoint);

            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult<AgentSessionCheckpoint>.Create(checkpoint);
        }

        public async Task<ListResponse<AgentSessionCheckpoint>> ListSessionCheckpointsAsync(string sessionId, int limit, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            if (session == null) return ListResponse<AgentSessionCheckpoint>.Create(new List<AgentSessionCheckpoint>());

            var cps = session.Checkpoints ?? new List<AgentSessionCheckpoint>();

            if (limit <= 0) limit = 100;

            var ordered = cps.OrderByDescending(c => SafeIsoToDateTime(c.CreationDate)).ThenByDescending(c => c.CheckpointId).Take(limit).ToList();
            return ListResponse<AgentSessionCheckpoint>.Create(ordered);
        }

        public async Task<InvokeResult<AgentSession>> RestoreSessionCheckpointAsync(AgentSession sourceSession, string checkpointId, EntityHeader org, EntityHeader user)
        {
            if (sourceSession == null) return InvokeResult<AgentSession>.FromError("Session not found.");

            if (string.IsNullOrWhiteSpace(checkpointId)) return InvokeResult<AgentSession>.FromError("RestoreSessionCheckpointAsync requires checkpointId.");

            var cps = sourceSession.Checkpoints ?? new List<AgentSessionCheckpoint>();
            var cp = cps.FirstOrDefault(c => string.Equals(c.CheckpointId, checkpointId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cp == null) return InvokeResult<AgentSession>.FromError($"Checkpoint '{checkpointId}' not found.");

            var anchorTurnId = cp.TurnSourceId;

            if (string.IsNullOrWhiteSpace(anchorTurnId) && sourceSession.Turns != null && !string.IsNullOrEmpty(cp.TurnSourceId))
            {
                var turn = sourceSession.Turns.FirstOrDefault(t => t.Id == cp.TurnSourceId);
                anchorTurnId = turn?.Id;
            }

            if (string.IsNullOrWhiteSpace(anchorTurnId)) return InvokeResult<AgentSession>.FromError("Checkpoint could not be resolved to a turn.");

            var startedUtc = DateTime.UtcNow.ToJSONString();

            var branch = await BranchSessionAsync(sourceSession.Id, anchorTurnId, org, user);
            if (!branch.Successful) return InvokeResult<AgentSession>.FromInvokeResult(branch.ToInvokeResult());

            var branchedSession = branch.Result;
            if (branchedSession == null || string.IsNullOrWhiteSpace(branchedSession.Id)) return InvokeResult<AgentSession>.FromError("Restore produced an invalid branched session.");

            branchedSession.RestoreReports ??= new List<AgentSessionRestoreReport>();

            string NextRestoreOperationId(List<AgentSessionRestoreReport> existing)
            {
                var next = 1;

                foreach (var r in existing ?? new List<AgentSessionRestoreReport>())
                {
                    if (r == null || string.IsNullOrWhiteSpace(r.RestoreOperationId)) continue;

                    var s = r.RestoreOperationId.Trim();
                    if (!s.StartsWith("RST-", StringComparison.OrdinalIgnoreCase)) continue;

                    var numPart = s.Substring(4);
                    if (int.TryParse(numPart, out var n) && n >= next) next = n + 1;
                }

                return $"RST-{next:000000}";
            }

            var opId = NextRestoreOperationId(branchedSession.RestoreReports);
            var completedUtc = DateTime.UtcNow.ToString("o");

            branchedSession.SourceSessionId = sourceSession.Id;
            branchedSession.SourceCheckpointId = cp.CheckpointId;
            branchedSession.SourceTurnSourceId = anchorTurnId;
            branchedSession.RestoreOperationId = opId;
            branchedSession.RestoredOnUtc = completedUtc;
            branchedSession.LastUpdatedBy = user;
            branchedSession.LastUpdatedDate = completedUtc;

            var report = new AgentSessionRestoreReport
            {
                RestoreOperationId = opId,
                StartedUtc = startedUtc,
                CompletedUtc = completedUtc,
                SourceSessionId = sourceSession.Id,
                SourceCheckpointId = cp.CheckpointId,
                SourceTurnSourceId = anchorTurnId,
                BranchedSessionId = branchedSession.Id,
                TurnsCopiedCount = branchedSession.Turns?.Count ?? 0,
                CheckpointsCopiedCount = branchedSession.Checkpoints?.Count ?? 0,
                ActiveFileRefsCopiedCount = (branchedSession.Turns ?? new List<AgentSessionTurn>()).Sum(t => t.ActiveFileRefs?.Count ?? 0),
                ChunkRefsCopiedCount = (branchedSession.Turns ?? new List<AgentSessionTurn>()).Sum(t => t.ChunkRefs?.Count ?? 0),
                CreatedByUser = user,
                SessionId = branchedSession.Turns?.LastOrDefault()?.SessionId,
                Summary = $"Restored {cp.CheckpointId} from session {sourceSession.Id} to new session {branchedSession.Id}.",
                Details = $"Restore checkpoint '{cp.CheckpointId}' (turn '{anchorTurnId}'). Created branched session '{branchedSession.Id}' with {branchedSession.Turns?.Count ?? 0} turns."
            };

            branchedSession.RestoreReports.Add(report);
            return InvokeResult<AgentSession>.Create(branchedSession);
        }

        private static DateTime SafeIsoToDateTime(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return DateTime.MinValue;
            if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) return dt;
            return DateTime.MinValue;
        }

        private static string NextCheckpointId(List<AgentSessionCheckpoint> existing)
        {
            var next = 1;

            foreach (var cp in existing ?? new List<AgentSessionCheckpoint>())
            {
                if (cp == null || string.IsNullOrWhiteSpace(cp.CheckpointId)) continue;

                var s = cp.CheckpointId.Trim();
                if (!s.StartsWith("CP-", StringComparison.OrdinalIgnoreCase)) continue;

                var numPart = s.Substring(3);
                if (int.TryParse(numPart, out var n) && n >= next) next = n + 1;
            }

            return $"CP-{next:0000}";
        }

        public Task<InvokeResult> UpdateKFRsAsync(AgentSession session, string mode, List<AgentSessionKfrEntry> entries)
        {
            if (session.Kfrs.ContainsKey(mode))
                session.Kfrs[mode] = entries;
            else
                session.Kfrs.Add(mode, entries);

            return Task.FromResult(InvokeResult.Success);
        }

        public async Task<InvokeResult> UpdateSessionAsync(AgentSession session, EntityHeader org, EntityHeader user)
        {
            await _repo.UpdateSessionAsyunc(session);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult<AgentSession>> RestoreSessionChapterAsync(string sessionId, string chapterId, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            return await RestoreSessionChapterAsync(session, chapterId, org, user);
        }

        public async Task<InvokeResult<AgentSession>> RestoreSessionChapterAsync(AgentSession session, string chapterId, EntityHeader org, EntityHeader user)
        {
            if (session.Turns.Any())
            {
                session.Turns.Last().Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.ChapterEnd);
        
                var currentArchive = session.Chapters?.FirstOrDefault(a => a.ChapterIndex == session.CurrentChapterIndex);
                if (currentArchive != null)
                    await _chapterStore.UpdateAsync(currentArchive, session, session.Turns, user);
            }

            var chatper = session.Chapters?.FirstOrDefault(a => a.Id == chapterId); 
            if(chatper == null)
                throw new RecordNotFoundException(nameof(AgentSessionChapter), chapterId);

            var turns = await _chapterStore.LoadAsync(chatper);
            session.Turns = new List<AgentSessionTurn>(turns);
            session.ChapterSeed = chatper.Summary;
            session.CurrentChapterIndex = chatper.ChapterIndex;
            session.CurrentChapter = EntityHeader.Create(chatper.Id, chatper.Title);

            await UpdateSessionAsync(session, org, user);

            return InvokeResult<AgentSession>.Create(session);
        }
    }
}
