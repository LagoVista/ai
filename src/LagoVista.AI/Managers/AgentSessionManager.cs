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

            turn.Status = EntityHeader<AgentSessionTurnStatuses>.Create(AgentSessionTurnStatuses.Pending);
            turn.StatusTimeStamp = session.LastUpdatedDate;
            turn.CreationDate = session.LastUpdatedDate;
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
                PreviousMode = mode,
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
            var session = await GetAgentSessionAsync(sessionId, org, user);

            var branchedTurn = session.Turns.SingleOrDefault(t => t.Id == turnId);
            if(branchedTurn == null)
            {
                throw new RecordNotFoundException(nameof(AgentSessionTurn), turnId);
            }

            var lastTurnIndex = session.Turns.IndexOf(branchedTurn);
            for(int idx = session.Turns.Count; idx > lastTurnIndex; idx--)
                session.Turns.RemoveAt(idx - 1);

            session.Id = Guid.NewGuid().ToId();
            session.Name = $"Branch - {session.Name}";
            session.LastUpdatedBy = user;
            session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            session.Mode = branchedTurn.Mode;

            await AddAgentSessionAsync(session, org, user);

            return InvokeResult<AgentSession>.Create(session);
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

            var session = await GetAgentSessionAsync(sessionId, org, user);
            if (session == null) return InvokeResult<AgentSession>.FromError("Session not found.");

            var cps = session.Checkpoints ?? new List<AgentSessionCheckpoint>();
            var cp = cps.FirstOrDefault(c => string.Equals(c.CheckpointId, checkpointId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cp == null) return InvokeResult<AgentSession>.FromError($"Checkpoint '{checkpointId}' not found.");

            var turnId = cp.TurnSourceId;

            if (string.IsNullOrWhiteSpace(turnId) && session.Turns != null && !String.IsNullOrEmpty(cp.TurnSourceId))
            {
                var turn = session.Turns.FirstOrDefault(t => t.Id == cp.TurnSourceId);
                turnId = turn?.Id;
            }

            if (string.IsNullOrWhiteSpace(turnId)) return InvokeResult<AgentSession>.FromError("Checkpoint could not be resolved to a turn.");

            return await BranchSessionAsync(sessionId, turnId, org, user);
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

    }
}
