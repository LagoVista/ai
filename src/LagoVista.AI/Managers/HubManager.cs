// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 1c7c70285208b79ccb64451b29867bcdec82a6308047ddd09a2c8b527a2de0c3
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class HubManager : IHubManager
    {
        public Task<InvokeResult<Hub>> GetHubForOrgAsync(EntityHeader org, EntityHeader user)
        {
            return Task.FromResult(new InvokeResult<Hub>()
            {

            });
        }
    }
}
