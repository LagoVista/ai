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

        public async Task CompleteAgentSessionTurnAsync(string agentSessionId, string turnId, string answerSummary, string answerBlobUrl, string openAiResponseId, double executionMs, List<string> warnings, EntityHeader org, EntityHeader user)
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
    }
}
