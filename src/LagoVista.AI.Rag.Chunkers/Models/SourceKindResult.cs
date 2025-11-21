using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Result of analyzing a C# source fragment for logical SubKind classification.
    /// </summary>
    public sealed class SourceKindResult
    {
        /// <summary>Relative path of the file this type was found in.</summary>
        public string Path { get; set; }

        /// <summary>The inferred SubKind for this specific type.</summary>
        public CodeSubKind SubKind { get; set; }

        public string SubKindString => SubKind.ToString();

        /// <summary>The simple name of the type (identifier).</summary>
        public string PrimaryTypeName { get; set; }

        public string Summary { get; set; }

        /// <summary>
        /// For the analyzer this is always false (we operate on a single symbol),
        /// but it is retained for compatibility and future-proofing.
        /// </summary>
        public bool IsMixed { get; set; }

        /// <summary>Human-readable explanation of why this SubKind was chosen.</summary>
        public string Reason { get; set; }

        /// <summary>Evidence snippets / rules that fired for this type.</summary>
        public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();

        /// <summary>
        /// C# text for this type declaration including using and namespace if available.
        /// </summary>
        public string SymbolText { get; set; }
    }
}
