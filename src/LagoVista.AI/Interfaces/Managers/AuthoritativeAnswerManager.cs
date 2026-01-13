using LagoVista.AI.Models.AuthoritativeAnswers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Managers
{
    public interface IAuthoritativeAnswerManager
    {
        Task<AuthoritativeAnswerLookupResult> LookupAsync(string orgId, string question, IEnumerable<string> tags = null);
        Task<AuthoritativeAnswerEntry> SaveAsync(string orgId, string question, string answer, IEnumerable<string> tags = null, string confidence = "high");
    }
}
