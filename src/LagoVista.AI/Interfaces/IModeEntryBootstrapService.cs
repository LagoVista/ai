using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Interfaces
{
    public interface IModeEntryBootstrapService
    {
        Task<InvokeResult<ModeEntryBootstrapDetails>> ExecuteAsync(
            ModeEntryBootstrapRequest request,
            CancellationToken cancellationToken = default);
    }
}
