using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public enum TextQueryType
    {
        Query,
        Reword,
        CreateContent
    }

    public class TextQuery
    {
        public TextQueryType QueryType { get; set; }

        public string Query { get; set; }
        public string Role { get; set; }
        public string ConversationId { get; set; }
    }

    public class TextQueryResponse
    {
        public string Response { get; set; }
        public string ConversationId { get; set; }
    }
    
}
