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

        public async Task<AgentSession> CreateSession(IAgentPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var now = DateTime.UtcNow.ToJSONString();

            var potentialName = String.IsNullOrEmpty(ctx.Envelope.Instructions) ? "File Upload" : ctx.Envelope.Instructions;
            var generatedName = await _namingService.GenerateNameAsync(ctx.AgentContext, potentialName, ctx.CancellationToken);

            var session = new AgentSession
            {
                OwnerOrganization = ctx.Envelope.Org,
                CreatedBy = ctx.Envelope.User,
                CreationDate = now,
                LastUpdatedBy = ctx.Envelope.User,
                LastUpdatedDate = now,
                OperationKind = EntityHeader<OperationKinds>.Create(OperationKinds.Code),
                ModeReason = "initial startup",
                ModeSetTimestamp = now,
                AgentContext = ctx.AgentContext.ToEntityHeader(),
                Role = ctx.Role.ToEntityHeader(),
                Name = generatedName,
            };

            session.Key = session.Id.ToLower();
            return session;
        }


        public AgentSessionTurn CreateTurnForNewSession(IAgentPipelineContext ctx, AgentSession session)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var now = DateTime.UtcNow.ToJSONString();
            var turn = new AgentSessionTurn
            {
                SequenceNumber = 1,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = now,
                StatusTimeStamp = now,
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.Instructions),
                SessionId = session.Id,
                Mode = session.Mode

            };

            return turn;
        }

        public AgentSessionTurn CreateTurnForExistingSession(IAgentPipelineContext ctx, AgentSession session)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var now = DateTime.UtcNow.ToJSONString();

            var turn = new AgentSessionTurn
            {
                SequenceNumber = session.Turns.Count + 1,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = now,
                StatusTimeStamp = now,
                Mode = session.Mode,
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.Instructions),
                SessionId = session.Id
            };

            return turn;
        }

        private static string BuildInstructionSummary(string instruction)
        {
            if (string.IsNullOrWhiteSpace(instruction))
            {
                return "File Uplaod";
            }

            if (instruction.Length <= InstructionSummaryMaxLength)
            {
                return instruction;
            }

            return instruction.Substring(0, InstructionSummaryMaxLength);
        }

     
    }
}
