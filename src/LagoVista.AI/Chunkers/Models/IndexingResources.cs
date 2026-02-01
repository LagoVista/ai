using LagoVista.AI.Rag.Chunkers.Models;
using System;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Read-only lookup/reference data for indexing. No services should be placed here.
    /// </summary>
    public class IndexingResources
    {
        public IndexingResources(IndexFileContext fileCtx)
        {
            FileContext = fileCtx ?? throw new ArgumentNullException(nameof(fileCtx));
        }


        public IndexFileContext FileContext { get; }

    }
}
