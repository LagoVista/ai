using LagoVista.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ISessionCodeFilesRepo
    {
        Task AddSessionCodeFileActivityAsync(string sessionid, SessionCodeFileActivity activity);

        Task<List<SessionCodeFileActivity>> GetSessionCodeFileActivitiesAsync(string sessionid);
    }
}
