using LagoVista.AI.Models.AuthoritativeAnswers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IAuthoritativeAnswerRepository
    {
        /// <summary>
        /// Search for AQ entries by normalized question. Implementations may use exact match,
        /// prefix match, and/or similarity match.
        /// </summary>
        Task<List<AuthoritativeAnswerEntry>> SearchAsync(string orgId, string normalizedQuestion, IEnumerable<string> tags = null);

        /// <summary>
        /// Insert or update an AQ entry.
        /// </summary>
        Task UpsertAsync(AuthoritativeAnswerEntry entry);
    }
}
