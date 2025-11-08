// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 1400889e38a081f8568ad1a48240ff21397cf3e467bf81cc33da1a2b888c46d4
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ITextQueryManager
    {
        Task<InvokeResult<TextQueryResponse>> HandlePromptAsync(TextQuery query);
    }
}
