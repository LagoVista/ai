using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Models
{
    /// <summary>
    /// Simple descriptor for a discovered file.
    /// </summary>
    public class DiscoveredFile
    {
        public string RepoId { get; set; }
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }
        public bool IsBinary { get; set; }
    }
}
