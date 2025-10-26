using System.Collections.Generic;

namespace LagoVista.AI.Services
{
    public sealed class AnswerResult
    {
        public string Text { get; set; } = "";
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }
}
