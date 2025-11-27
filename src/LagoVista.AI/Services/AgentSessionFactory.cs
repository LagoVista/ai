using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Services
{
    public class AgentSessionFactory : IAgentSessionFactory
    {
        private const int InstructionSummaryMaxLength = 256;

        private readonly IAgentSessionNamingService _namingService;

        public AgentSessionFactory(IAgentSessionNamingService namingService)
        {
            _namingService = namingService ?? throw new ArgumentNullException(nameof(namingService));
        }

        public async Task<AgentSession> CreateSession(AgentExecuteRequest request, AgentContext agentContext, OperationKinds kind, EntityHeader org, EntityHeader user)
        {
            var now = DateTime.UtcNow.ToJSONString();

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }


            var session = new AgentSession
            {
                OwnerOrganization = org,
                CreatedBy = user,
                CreationDate = now,
                LastUpdatedBy = user,
                LastUpdatedDate = now,
                AgentContext = request.AgentContext,
                ConversationContext = request.ConversationContext,
                OperationKind = EntityHeader<OperationKinds>.Create(kind),
                WorkspaceId = request.WorkspaceId,
                Repo = request.Repo,
                DefaultLanguage = request.Language
            };

            session.Key = session.Id.ToLower();
            session.Name =await  _namingService.GenerateNameAsync(agentContext, request.Instruction, default);

            return session;
        }

        public AgentSessionTurn CreateTurnForNewSession(AgentSession session, AgentExecuteRequest request, EntityHeader org, EntityHeader user)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var now = DateTime.UtcNow.ToJSONString();

            var turn = new AgentSessionTurn
            {
                SequenceNumber = 1,
                CreatedByUser = user,
                CreationDate = now,
                StatusTimeStamp = now,
                Mode = request.Mode,
                InstructionSummary = BuildInstructionSummary(request.Instruction),
                ConversationId = Guid.NewGuid().ToId()
            };

            return turn;
        }

        public AgentSessionTurn CreateTurnForExistingSession(AgentSession session, AgentExecuteRequest request, EntityHeader org, EntityHeader user)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var now = DateTime.UtcNow.ToJSONString();

            var turn = new AgentSessionTurn
            {
                CreatedByUser = user,
                CreationDate = now,
                StatusTimeStamp = now,
                Mode = "ask",
                InstructionSummary = BuildInstructionSummary(request.Instruction)
            };

            return turn;
        }

        private static string BuildInstructionSummary(string instruction)
        {
            if (string.IsNullOrWhiteSpace(instruction))
            {
                return string.Empty;
            }

            if (instruction.Length <= InstructionSummaryMaxLength)
            {
                return instruction;
            }

            return instruction.Substring(0, InstructionSummaryMaxLength);
        }
    }
}
