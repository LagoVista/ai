using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ITextQueryManager
    {
        Task<InvokeResult<TextQueuryResponse>> HandlePromptAsync(TextQuery query);
    }
}
