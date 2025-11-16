// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: d68b820ae954b7d9eb204d19d603b461d057fe4c34957f7004671d5fbf0e2a24
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.AI.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Services
{
    public sealed class AnswerResult
    {
        public string Text { get; set; } = "";
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }
}
