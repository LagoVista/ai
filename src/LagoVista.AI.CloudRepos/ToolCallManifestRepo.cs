using LagoVista.AI.Interfaces;
using LagoVista.AI.Models.Context;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class ToolCallManifestRepo : IToolCallManifestRepo
    {
        public Task<ToolCallManifest> GetToolCallManifestAsync(string toolManifestId, string orgId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveToolCallManifestAsync(string toolManifestId, string orgId)
        {
            throw new NotImplementedException();
        }

        public Task SetCallToolManifestAsync(string orgId, ToolCallManifest toolManifest)
        {
            throw new NotImplementedException();
        }
    }
}
