using LagoVista.Core.AI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Models
{
    public class ProcessedFileResults
    {
        public List<IRagPoint> RagPoints { get; set; } = new List<IRagPoint>();
        public string OriginalFileBlobUri { get; set; }
        public byte[] OriginalFileContents { get; set; }
    }
}
