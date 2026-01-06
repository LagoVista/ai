using LagoVista.AI.Models.Context;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IToolCallManifestRepo
    {
        Task SetCallToolManifestAsync(string orgId, string toolManifestId, ToolCallManifest toolManifest);
        Task<ToolCallManifest> GetToolCallManifestAsync(string toolManifestId, string orgId);
    
        Task RemoveToolCallManifestAsync(string toolManifestId, string orgId);  
    }
}
