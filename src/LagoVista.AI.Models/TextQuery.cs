using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class TextQuery
    {
        public string Query { get; set; }
        public string ConversationId { get; set; }
    }

    public class TextQueuryResponse
    {
        public string Response { get; set; }
        public string ConversationId { get; set; }
    }
}
