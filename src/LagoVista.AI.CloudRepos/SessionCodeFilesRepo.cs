using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.Storage;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class SessionCodeFilesRepo : TableStorageBase<SessionCodeFileActivityTSEntity>, ISessionCodeFilesRepo
    {
        public SessionCodeFilesRepo(IMLRepoSettings settings, IAdminLogger logger) : base(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey, logger)
        {
        }

        public Task AddSessionCodeFileActivityAsync(string sessionid, SessionCodeFileActivity activity)
        {
            var entity = SessionCodeFileActivityTSEntity.FromSessionCodeFileActivity(sessionid, activity);
            return InsertAsync(entity);
        }

        public async Task<List<SessionCodeFileActivity>> GetSessionCodeFileActivitiesAsync(string sessionid)
        {
            var entities = await GetByPartitionIdAsync(sessionid);
            return entities.Select(e => e.ToSessionCodeFileActivity()).ToList();
        }
    }

    public class SessionCodeFileActivityTSEntity : TableStorageEntity
    {
        public string SessionId { get; set; }
        public string Timestamp { get; set; }
        public string FilePath { get; set; }
        public string Reason { get; set; }
        public SessionCodeFileActivity ToSessionCodeFileActivity()
        {
            return new SessionCodeFileActivity
            {
                Timestamp = this.Timestamp,
                FilePath = this.FilePath,
                Reason = this.Reason
            };
        }
        public static SessionCodeFileActivityTSEntity FromSessionCodeFileActivity(string sessionId, SessionCodeFileActivity activity)
        {
            return new SessionCodeFileActivityTSEntity
            {
                PartitionKey = sessionId,
                RowKey = DateTime.UtcNow.ToInverseTicksRowKey(),
                SessionId = sessionId,
                Timestamp = activity.Timestamp,
                FilePath = activity.FilePath,
                Reason = activity.Reason
            };
        }
    }
}
