using Azure.Data.Tables;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class MemoryNoteRepo : TableStorageBase<MemoryNoteDTO>, IMemoryNoteRepo
    {
        public MemoryNoteRepo(IMLRepoSettings settings, IAdminLogger logger) :
               base(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey, logger)
        {
        }

        public Task AddMemoryNoteAsync(AgentSessionMemoryNote note)
        {
            return InsertAsync(MemoryNoteDTO.FromNote(note));
        }

        public async Task<ListResponse<AgentSessionMemoryNote>> GetMemoryNotesForSessionAsync(string orgId, string sessionId)
        {
            var nodes = await GetByParitionIdAsync(sessionId);

            if (nodes.Select(nd => nd.OrgId != orgId).Any())
                throw new NotAuthorizedException("unauthorized org access");

            return ListResponse<AgentSessionMemoryNote>.Create(nodes.Select(nd => nd.ToNotes()));
        }

        public Task UpdateMemoryNoteAsync(AgentSessionMemoryNote note)
        {
            return UpdateAsync(MemoryNoteDTO.FromNote(note));
        }
    }

    public class MemoryNoteDTO : TableStorageEntity
    {
        internal static MemoryNoteDTO FromNote(AgentSessionMemoryNote agentSessionMemoryNote)
        {
            return new MemoryNoteDTO()
            {
                RowKey = agentSessionMemoryNote.Id,
                PartitionKey = agentSessionMemoryNote.SessionId,
                CreatedById = agentSessionMemoryNote.CreatedByUser.Id,
                CrewatedBy = agentSessionMemoryNote.CreatedByUser.Text,
                CreationDate = agentSessionMemoryNote.CreationDate,
                Details = agentSessionMemoryNote.Details,
                Id = agentSessionMemoryNote.Id,
                Importance = agentSessionMemoryNote.Importance.Id.ToString(),
                Kind = agentSessionMemoryNote.Kind.Id.ToString(),
                MemoryId = agentSessionMemoryNote.MemoryId,
                SessionId = agentSessionMemoryNote.SessionId,
                TurnSourceId = agentSessionMemoryNote.TurnSourceId,
                Summary = agentSessionMemoryNote.Summary,
                Tags = string.Join(",", agentSessionMemoryNote.Tags ?? new List<string>()),
                Title = agentSessionMemoryNote.Title,
                OrgId = agentSessionMemoryNote.OrgId,
            };
        }

        public AgentSessionMemoryNote ToNotes()
        {
            return new AgentSessionMemoryNote()
            {
                OrgId = this.OrgId,
                Id = this.Id,
                Title = this.Title,
                Summary = this.Summary,
                CreationDate = this.CreationDate,
                Details = this.Details,
                CreatedByUser = EntityHeader.Create(CreatedById, CrewatedBy),
                Tags = this.Tags.Split(',').ToList(),
                Importance = EntityHeader<AgentSessionMemoryNoteImportance>.Create(Enum.Parse<AgentSessionMemoryNoteImportance>(this.Importance)),
                Kind = EntityHeader<AgentSessionMemoryNoteKinds>.Create(Enum.Parse<AgentSessionMemoryNoteKinds>(this.Kind)),
                MemoryId = this.MemoryId,
                SessionId = this.SessionId,
                TurnSourceId = this.TurnSourceId,
            };
        }

        public string Id { get; set; }

        public string OrgId { get; set; }

        /// <summary>
        /// Stable, short identifier displayed to the user and referenced later (e.g., MEM-0042).
        /// </summary>
        public string MemoryId { get; set; }

        public string Title { get; set; }

        /// <summary>
        /// 1-2 line summary that acts as the "marker" in the conversation.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Optional longer detail; can include snippets.
        /// </summary>
        public string Details { get; set; }

        public string Importance { get; set; }
        public string Kind { get; set; }

        public string Tags { get; set; }

        public string CreationDate { get; set; }

        public string CreatedById { get; set; }

        public string CrewatedBy { get; set; }

        public string TurnSourceId { get; set; }

        public string SessionId { get; set; }
    }
}
