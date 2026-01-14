using LagoVista.Core.Models;

namespace LagoVista.AI.Models
{
    public class AgentSessionArchive
    {
        public string Id { get; set; }

     
        public int ChapterIndex { get; set; }
        public string Title { get; set; }

        public string Summary { get; set; }

        public string CreationDate { get; set; }
    
        public int TurnCount { get; set; }
        public EntityHeader CreatedBy { get; set; }
    
        public string FirstTurnId { get; set; }
        public string LastTurnId { get; set; }

        public string FirstOpenAIResponseId { get; set; }
        public string LastOpenAIResponseId { get; set; }
        public string ContentSha256 { get; set; }
    
        public string BlobUrl { get; set; }
        public string BlobKey { get; set; }
    }
    
}