﻿using LagoVista.AI.Models;
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
