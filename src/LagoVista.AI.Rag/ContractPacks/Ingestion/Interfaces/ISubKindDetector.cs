using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces
{
    /// <summary>
    /// Abstraction over SubKind detection so we can unit test the catalog builder
    /// without binding directly to a static implementation.
    /// </summary>
    public interface ISubKindDetector
    {
        IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath);
    }
}
