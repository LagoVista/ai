using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Services
{
    public interface IPromptKnowledgeProvider
    {
        Task<InvokeResult<IAgentPipelineContext>> PopulateAsync(IAgentPipelineContext ctx, bool changeMode);
    }
}
