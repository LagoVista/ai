using ICSharpCode.SharpZipLib.Core;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using RingCentral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class AgentSessionManager : ManagerBase, IAgentSessionManager
    {
        IAgentSessionRepo _repo;

        public AgentSessionManager(IAgentSessionRepo repo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) :
            base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public async Task AddAgentSessionAsync(AgentSession session, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(session, Actions.Create);

            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddSessionAsync(session);
        }

        public async Task AddAgentSessionTurnAsync(string agentSessionId, AgentSessionTurn turn, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            var now = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedDate = now;
            session.LastUpdatedBy = user;

            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Pending);
            turn.StatusTimeStamp = now;
            turn.CreationDate = now;
            turn.CreatedByUser = user;
            turn.SequenceNumber = session.Turns.Count + 1;
            session.Turns.Add(turn);

            ValidationCheck(session, Actions.Create);

            await _repo.UpdateSessionAsyunc(session);
        }

        public async Task CompleteAgentSessionTurnAsync(string agentSessionId, string turnId, string answerSummary, string answerBlobUrl, string openAiResponseId,
            int promptTokens, int completionTokens, int totalTokens,
            double executionMs, List<string> warnings, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if (turn == null)
            {
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);
            }

            if (turn.Status.Value == AgentSessionTurnStatuses.Completed)
            {
                throw new Exception("Cannot complete a turn that has already been completed.");
            }

            turn.AgentAnswerSummary = answerSummary;
            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create( AgentSessionTurnStatuses.Completed);
            turn.StatusTimeStamp = DateTime.UtcNow.ToJSONString();
            turn.OpenAIResponseReceivedDate = turn.StatusTimeStamp;
            turn.OpenAIResponseId = openAiResponseId;
            turn.TotalTokens = totalTokens;
            turn.PromptTokens = promptTokens;
            turn.CompletionTokens = completionTokens;
            turn.OpenAIChainExpiresDate = DateTime.UtcNow.AddDays(30).ToJSONString();
            turn.OpenAIResponseBlobUrl = answerBlobUrl;
            turn.Warnings.AddRange(warnings);
            turn.ExecutionMs = executionMs;

            ValidationCheck(session, Actions.Update);

            await _repo.UpdateSessionAsyunc(session);
        }

        public async Task FailAgentSessionTurnAsync(string agentSessionId, string turnId, string openAiResponseId, double executionMs, List<string> errors, List<string> warnings, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Update, user, org);

            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if(turn == null)
            {
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);
            }

            if (turn.Status.Value == AgentSessionTurnStatuses.Completed)
            {
                throw new Exception("Cannot fail a turn that has already been completed.");
            }

            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Failed);
            turn.StatusTimeStamp = DateTime.UtcNow.ToJSONString();
            turn.OpenAIResponseReceivedDate = turn.StatusTimeStamp;
            turn.Warnings.AddRange(warnings);
            turn.Errors.AddRange(errors);
            turn.ExecutionMs = executionMs;
            ValidationCheck(session, Actions.Update);

            await _repo.UpdateSessionAsyunc(session);
        }

        public async Task<InvokeResult> AbortTurnAsync(string sessionId, string turnId, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(sessionId);
            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Update, user, org);
            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if (turn == null)
            {
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);
            }

            if (turn.Status.Value == AgentSessionTurnStatuses.Completed)
            {
                throw new Exception("Cannot abort a turn that has already been completed.");
            }

            if (turn.Status.Value == AgentSessionTurnStatuses.Failed)
            {
                throw new Exception("Cannot abort a turn that has already failed.");
            }

            // If we didn't even create the turn yet, we were very early on in the request, it's OK to just let it fall out
            if (turn == null)
                return InvokeResult.Success;

            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Aborted);
            turn.StatusTimeStamp = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedDate = turn.StatusTimeStamp;

            ValidationCheck(session, Actions.Update);
            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult.Success;
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

        public async Task<AgentSessionTurn> GetAgentSessionTurnAsync(string agentSessionId, string turnId, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Update, user, org);

            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if (turn == null)
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);

            return turn;
        }

        public async Task<AgentSessionTurn> GetLastAgentSessionTurnAsync(string agentSessionId, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionId);
            await AuthorizeAsync(session, AuthorizeResult.AuthorizeActions.Update, user, org);

            return session.Turns.LastOrDefault();
        }

        public async Task SetRequestBlobUriAsync(string agentSessionid, string turnId, string requestBlobUri, EntityHeader org, EntityHeader user)
        {
            var session = await _repo.GetAgentSessionAsync(agentSessionid);
            var turn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if (turn == null)
            {
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);
            }

            turn.OpenAIRequestBlobUrl = requestBlobUri;
            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Pending);
            turn.StatusTimeStamp = DateTime.UtcNow.ToJSONString();
            session.LastUpdatedDate = turn.StatusTimeStamp;
            await _repo.UpdateSessionAsyunc(session);
        }

        public async Task<InvokeResult> SetSessionModeAsync(string sessionId, string mode, string reason, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);

            var now = DateTime.UtcNow.ToJSONString();
            session.ModeHistory.Add(new ModeHistory()
            {
                PreviousMode = session.Mode,
                NewMode = mode,
                Reason = reason,
                TimeStamp = now,
            });

            session.Mode = mode;
            session.ModeReason = reason;
            session.ModeSetTimestamp = now;
            session.LastUpdatedDate = now;

            ValidationCheck(session, Actions.Update);

            await _repo.UpdateSessionAsyunc(session);
            return InvokeResult.Success;
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



        public async Task<InvokeResult<AgentSessionMemoryNote>> AddSessionMemoryNoteAsync(string sessionId, AgentSessionMemoryNote note, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            session.LastUpdatedBy = user;
            session.LastUpdatedDate = note.CreationDate;
            session.MemoryNotes.Add(note);

            await _repo.UpdateSessionAsyunc(session); 

            return InvokeResult<AgentSessionMemoryNote>.Create(note);
        }

 

        public async Task<ListResponse<AgentSessionMemoryNote>> ListSessionMemoryNotesAsync(string sessionId, string tag, string kind, string importanceMin, int limit, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            if (session == null) return ListResponse<AgentSessionMemoryNote>.Create(new List<AgentSessionMemoryNote>());

            var notes = session.MemoryNotes ?? new List<AgentSessionMemoryNote>();

            IEnumerable<AgentSessionMemoryNote> query = notes;

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var t = tag.Trim();
                query = query.Where(n => n.Tags != null && n.Tags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)));
            }

            var kindFilter = ParseMemoryKind(kind);
            if (kindFilter.HasValue) query = query.Where(n => n.Kind != null && n.Kind.Value == kindFilter.Value);

            var minImportance = ParseMemoryImportance(importanceMin);
            if (minImportance.HasValue) query = query.Where(n => n.Importance != null && n.Importance.Value >= minImportance.Value);

            query = query.OrderByDescending(n => SafeIsoToDateTime(n.CreationDate)).ThenByDescending(n => n.MemoryId);

            if (limit <= 0) limit = 50;

            return ListResponse<AgentSessionMemoryNote>.Create(query.Take(limit).ToList());
        }

        public async Task<InvokeResult<List<AgentSessionMemoryNote>>> RecallSessionMemoryNotesAsync(string sessionId, List<string> memoryIds, string tag, string kind, bool includeDetails, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            if (session == null) return InvokeResult<List<AgentSessionMemoryNote>>.Create(new List<AgentSessionMemoryNote>());

            var notes = session.MemoryNotes ?? new List<AgentSessionMemoryNote>();

            IEnumerable<AgentSessionMemoryNote> query = notes;

            var ids = (memoryIds ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count > 0) query = query.Where(n => !string.IsNullOrWhiteSpace(n.MemoryId) && ids.Contains(n.MemoryId, StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var t = tag.Trim();
                query = query.Where(n => n.Tags != null && n.Tags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)));
            }

            var kindFilter = ParseMemoryKind(kind);
            if (kindFilter.HasValue) query = query.Where(n => n.Kind != null && n.Kind.Value == kindFilter.Value);

            var results = query.OrderByDescending(n => SafeIsoToDateTime(n.CreationDate)).ThenByDescending(n => n.MemoryId).ToList();

            if (!includeDetails)
            {
                results = results.Select(n => new AgentSessionMemoryNote
                {
                    Id = n.Id,
                    MemoryId = n.MemoryId,
                    Title = n.Title,
                    Summary = n.Summary,
                    Details = null,
                    Importance = n.Importance,
                    Kind = n.Kind,
                    Tags = n.Tags == null ? new List<string>() : new List<string>(n.Tags),
                    CreationDate = n.CreationDate,
                    CreatedByUser = n.CreatedByUser,
                    TurnSourceId = n.TurnSourceId,
                    ConversationId = n.ConversationId
                }).ToList();
            }

            return InvokeResult<List<AgentSessionMemoryNote>>.Create(results);
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
            checkpoint.ConversationId ??= session.Turns?.LastOrDefault()?.ConversationId;

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

        public async Task<InvokeResult<AgentSession>> RestoreSessionCheckpointAsync(string sessionId, string checkpointId, EntityHeader org, EntityHeader user)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return InvokeResult<AgentSession>.FromError("RestoreSessionCheckpointAsync requires sessionId.");
            if (string.IsNullOrWhiteSpace(checkpointId)) return InvokeResult<AgentSession>.FromError("RestoreSessionCheckpointAsync requires checkpointId.");

            var sourceSession = await GetAgentSessionAsync(sessionId, org, user);
            if (sourceSession == null) return InvokeResult<AgentSession>.FromError("Session not found.");

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

            var branch = await BranchSessionAsync(sessionId, anchorTurnId, org, user);
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
                MemoryNotesCopiedCount = branchedSession.MemoryNotes?.Count ?? 0,
                CheckpointsCopiedCount = branchedSession.Checkpoints?.Count ?? 0,
                ActiveFileRefsCopiedCount = (branchedSession.Turns ?? new List<AgentSessionTurn>()).Sum(t => t.ActiveFileRefs?.Count ?? 0),
                ChunkRefsCopiedCount = (branchedSession.Turns ?? new List<AgentSessionTurn>()).Sum(t => t.ChunkRefs?.Count ?? 0),
                CreatedByUser = user,
                ConversationId = branchedSession.Turns?.LastOrDefault()?.ConversationId,
                Summary = $"Restored {cp.CheckpointId} from session {sourceSession.Id} to new session {branchedSession.Id}.",
                Details = $"Restore checkpoint '{cp.CheckpointId}' (turn '{anchorTurnId}'). Created branched session '{branchedSession.Id}' with {branchedSession.Turns?.Count ?? 0} turns and {branchedSession.MemoryNotes?.Count ?? 0} memory notes."
            };

            branchedSession.RestoreReports.Add(report);

            await _repo.UpdateSessionAsyunc(branchedSession);

            return InvokeResult<AgentSession>.Create(branchedSession);
        }


        private static AgentSessionMemoryNoteKinds? ParseMemoryKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return null;

            switch (kind.Trim().ToLowerInvariant())
            {
                case "invariant":
                    return AgentSessionMemoryNoteKinds.Invariant;
                case "decision":
                    return AgentSessionMemoryNoteKinds.Decision;
                case "constraint":
                    return AgentSessionMemoryNoteKinds.Constraint;
                case "fact":
                    return AgentSessionMemoryNoteKinds.Fact;
                case "todo":
                    return AgentSessionMemoryNoteKinds.Todo;
                case "gotcha":
                    return AgentSessionMemoryNoteKinds.Gotcha;
                default:
                    return null;
            }
        }

        private static AgentSessionMemoryNoteImportance? ParseMemoryImportance(string importanceMin)
        {
            if (string.IsNullOrWhiteSpace(importanceMin)) return null;

            switch (importanceMin.Trim().ToLowerInvariant())
            {
                case "low":
                    return AgentSessionMemoryNoteImportance.Low;
                case "normal":
                    return AgentSessionMemoryNoteImportance.Normal;
                case "high":
                    return AgentSessionMemoryNoteImportance.High;
                case "critical":
                    return AgentSessionMemoryNoteImportance.Critical;
                default:
                    return null;
            }
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

        public async Task<InvokeResult> UpdateKFRsAsync(string sessionId, string mode, List<AgentSessionKfrEntry> entries, EntityHeader org, EntityHeader user)
        {
            var session = await GetAgentSessionAsync(sessionId, org, user);
            if(session.Kfrs.ContainsKey(mode))
                session.Kfrs[mode] = entries;
            else
                session.Kfrs.Add(mode, entries);

            await _repo.UpdateSessionAsyunc(session);

            return InvokeResult.Success;
        }
    }
}
