using System;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;

namespace LagoVista.AI.Services
{
    public class AgentSessionFactory : IAgentSessionFactory
    {
        private const int InstructionSummaryMaxLength = 256;

        public AgentSession CreateSession(NewAgentExecutionSession request, EntityHeader org, EntityHeader user)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var session = new AgentSession
            {
                AgentContext = request.AgentContext,
                ConversationContext = request.ConversationContext,
                OperationKind = request.OperationKind,
                WorkspaceId = request.WorkspaceId,
                Repo = request.Repo,
                DefaultLanguage = request.Language
            };

            return session;
        }

        public AgentSessionTurn CreateTurnForNewSession(AgentSession session, NewAgentExecutionSession request, EntityHeader org, EntityHeader user)
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
                Mode = "ask",
                InstructionSummary = BuildInstructionSummary(request.Instruction),
                ConversationId = Guid.NewGuid().ToId()
            };

            return turn;
        }

        public AgentSessionTurn CreateTurnForExistingSession(AgentSession session, AgentExecutionRequest request, EntityHeader org, EntityHeader user)
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
