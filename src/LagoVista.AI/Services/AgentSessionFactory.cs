using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Resources;
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

            var potentialName = String.IsNullOrEmpty(ctx.Envelope.OriginalInstructions) ? "File Upload" : ctx.Envelope.OriginalInstructions;
            var generatedName = await _namingService.GenerateNameAsync(ctx.AgentContext, potentialName, ctx.CancellationToken);


            var chapter = new AgentSessionChapter()
            {
                Title = $"{AIResources.AgentChapter_ChaterLabel} 1",
                ChapterIndex = 1,
                CreatedBy = ctx.Envelope.User,
                CreationDate = now,
            };

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
                AgentPersona = ctx.AgentContext.DefaultAgentPersona,
                AgentMode = ctx. Mode.ToEntityHeader(),
                Role = ctx.Role.ToEntityHeader(),
                Name = generatedName,
                CurrentChapterIndex = chapter.ChapterIndex,
                ChapterSeed = AIResources.AgentSesssion_ChapterSeed_Initial
            };

            session.CurrentChapter = EntityHeader.Create(chapter.Id, chapter.Title);

            session.Chapters.Add(chapter);
            session.Key = session.Id.ToLower();
            return session;
        }


        public AgentSessionTurn CreateTurnForNewSession(IAgentPipelineContext ctx, AgentSession session)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var now = DateTime.UtcNow.ToJSONString();
            var turn = new AgentSessionTurn
            {
                SequenceNumber = 1,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = now,
                Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.Initial),
                StatusTimeStamp = now,
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.OriginalInstructions),
                OriginalInstructions = ctx.Envelope.OriginalInstructions,
                SessionId = session.Id,
                Mode = session.Mode
            };

            return turn;
        }

        public AgentSessionTurn CreateTurnForNewChapter(IAgentPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var session = ctx.Session ?? throw new ArgumentNullException(nameof(ctx.Session));

            var turn = new AgentSessionTurn
            {
                SequenceNumber = 1,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = ctx.TimeStamp,
                StatusTimeStamp = ctx.TimeStamp,
                Mode = session.Mode,
                Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.ChapterStart),
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.OriginalInstructions),
                OriginalInstructions = ctx.Envelope.OriginalInstructions,
                SessionId = session.Id,
                OpenAIResponseId = null
            };

            return turn;

        }

        public AgentSessionTurn CreateTurnForExistingSession(IAgentPipelineContext ctx, AgentSession session)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var now = DateTime.UtcNow.ToJSONString();

            var turn = new AgentSessionTurn
            {
                SequenceNumber = session.Turns.Count + 1,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = now,
                StatusTimeStamp = now,
                Mode = session.Mode,
                Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.Normal),
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.OriginalInstructions),
                OriginalInstructions = ctx.Envelope.OriginalInstructions,
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

        public AgentSessionChapter CreateBoundaryTurnForNewChapter(IAgentPipelineContext ctx)
        {
            var newChapter = new AgentSessionChapter()
            {
                Title = $"Chapter {ctx.Session.Chapters.Count + 1}",
                ChapterIndex = ctx.Session.Chapters.Count + 1,
                Summary = ctx.Session.CurrentCapsule.PreviousChapterSummary,
                CreatedBy = ctx.Envelope.User,
                CreationDate = ctx.TimeStamp
            };

            ctx.Session.LastUpdatedDate = DateTime.UtcNow.ToJSONString();
            ctx.Session.LastUpdatedBy = ctx.Envelope.User;
            ctx.Session.CurrentChapterIndex = ctx.Session.CurrentChapterIndex + 1;
            ctx.Session.CurrentChapter = EntityHeader.Create(newChapter.Id, newChapter.Title);
            ctx.Session.ChapterSeed = ctx.Session.CurrentCapsule.PreviousChapterSummary;

            return newChapter;
        }

        public AgentSessionTurn CreateFirstTurnForNewChapter(IAgentPipelineContext ctx, AgentSession session)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var now = DateTime.UtcNow.ToJSONString();
            var turn = new AgentSessionTurn
            {
                SequenceNumber = 2,
                CreatedByUser = ctx.Envelope.User,
                CreationDate = now,
                Type = EntityHeader<AgentSessionTurnType>.Create(AgentSessionTurnType.Initial),
                StatusTimeStamp = now,
                InstructionSummary = BuildInstructionSummary(ctx.Envelope.OriginalInstructions),
                SessionId = session.Id,
                Mode = session.Mode
            };

            return turn;
        }
    }
}
