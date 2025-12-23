// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 80100646c220b238ae0c48cb01159c27cb2380944e7b11d0bec23d4f789084cf
// IndexVersion: 2
// --- END CODE INDEX META ---
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
        public string SessionId { get; set; }
    }

    public class TextQueryResponse
    {
        public string Response { get; set; }
        public string SessionId { get; set; }
    }
    
}
