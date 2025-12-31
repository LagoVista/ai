using DnsClient.Protocol;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class AgentTurnChatHistoryRepo : TableStorageBase<AgentTurnChatHistoryDto>, IAgentTurnChatHistoryRepo
    {
        private const int MaxColumnSize = (64 - 1) * 1024; //Azure max column is 64K - just do 1K less for a little spare room.

        public AgentTurnChatHistoryRepo(IMLRepoSettings settings, IAdminLogger logger) : 
            base(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey, logger)
        {
        }

        public Task AddTurnAsync(string orgId, string sessionId, string turnId, string userInstructions, string modelResponseText)
        {
            var dto = new AgentTurnChatHistoryDto()
            {
                PartitionKey = sessionId,
                OrgId = orgId,
                RowKey = DateTime.UtcNow.ToInverseTicksRowKey(),
                TurnId = turnId,
                Sessionid = sessionId,
                UserInstructions = userInstructions.Length > MaxColumnSize ? userInstructions.Substring(MaxColumnSize) : userInstructions,
                ModelResponseText = modelResponseText.Length > MaxColumnSize ? modelResponseText.Substring(MaxColumnSize) : modelResponseText,
                ModelResponseTextTruncated = modelResponseText.Length > MaxColumnSize,
                UserInstructionsTruncated = userInstructions.Length > MaxColumnSize,
            };

            return InsertAsync(dto);
        }

        public async Task<AgentTurnChatHistory> GetTurnAsync(string orgId, string sessionId, string turnId)
        {
            var result = await GetAsync(sessionId, turnId);
            if (result.OrgId != orgId)
                throw new NotAuthorizedException("attempt to access record from wrong org.");

            return result.ToHistory();
        }

        public async Task<ListResponse<AgentTurnChatHistory>> GetTurnsAsync(string orgId, string sessionId)
        {
            var records = await GetByParitionIdAsync(orgId);
            return ListResponse<AgentTurnChatHistory>.Create(records.Select(rc => rc.ToHistory()));
        }
    }

    public class AgentTurnChatHistoryDto : TableStorageEntity
    {

        public AgentTurnChatHistory ToHistory()
        {
            return new AgentTurnChatHistory()
            {
                TimeStamp = this.TimeStamp,
                TurnId = this.TurnId,
                Sessionid = this.Sessionid,
                UserInstructions = this.UserInstructions,
                ModelResponseText = this.ModelResponseText,
                ModelResponseTextTruncated = this.ModelResponseTextTruncated,
                UserInstructionsTruncated  = this.UserInstructionsTruncated,
            };
        }

        public string OrgId { get; set; }

        public string TimeStamp { get; set; }
        public string TurnId { get; set; }
        public string Sessionid { get; set; }

        public string UserInstructions { get; set; }
        public string ModelResponseText { get; set; }

        public bool ModelResponseTextTruncated { get; set; }
        public bool UserInstructionsTruncated { get; set; }
    }

}
