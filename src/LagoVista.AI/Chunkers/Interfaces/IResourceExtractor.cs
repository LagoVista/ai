using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    public interface IResourceExtractor
    {
        IReadOnlyList<ResxResourceChunk> Extract(string xmlText, string relativePath);
    }
}
