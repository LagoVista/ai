using LagoVista.Core;
using LagoVista.Core.Models;
using System;

namespace LagoVista.AI.Models
{
    public class AgentSessionChapter
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

     
        public int ChapterIndex { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string CreationDate { get; set; }
    
        public EntityHeader CreatedBy { get; set; }

        public string ContentSha256 { get; set; }
    
        public string BlobUrl { get; set; }
        public string BlobKey { get; set; }
    
        public long TotalTokenCount { get; set; }
    }
    
}