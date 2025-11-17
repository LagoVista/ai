using System.Collections.Generic;
using LagoVista.Core.Models;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI.Models
{
    public class AgentExecutionResponse 
    {
        public string SessionId { get; set; }
        public string TurnId { get; set; }

        public string OpenAIResponseId { get; set; }
        public string PreviousOpenAIResponseId { get; set; }

        public string AgentAnswer { get; set; }
        public string AgentAnswerFullText { get; set; }

        public string OpenAIRequestBlobUrl { get; set; }
        public string OpenAIResponseBlobUrl { get; set; }

        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        public List<AgentSessionChunkRef> ChunkRefs { get; set; } = new List<AgentSessionChunkRef>();
        public List<AgentSessionActiveFileRef> ActiveFileRefs { get; set; } = new List<AgentSessionActiveFileRef>();
    }
}
