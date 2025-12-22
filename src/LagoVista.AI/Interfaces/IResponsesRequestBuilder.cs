using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IResponsesRequestBuilder
    {
        ResponsesApiRequest Build(
            ConversationContext conversationContext,
            AgentExecuteRequest request,
            string ragContextBlock,
            string toolUsageMetadataBlock);
    }
}
