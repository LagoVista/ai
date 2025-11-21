using LagoVista.AI.Rag.Chunkers.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    public sealed class SubKindDetectionResult
    {
        /// <summary>Relative path of the file this type was found in.</summary>
        public string Path { get; set; }

        public string Summary { get; set; }

        /// <summary>The inferred SubKind for this specific type.</summary>
        public CodeSubKind SubKind { get; set; }

        public string SubKindString => SubKind.ToString();

        /// <summary>The simple name of the type (identifier).</summary>
        public string PrimaryTypeName { get; set; }

        /// <summary>
        /// True if the file contains more than one distinct SubKind across all types.
        /// (Same value for all results from the same file.)
        /// </summary>
        public bool IsMixed { get; set; }

        /// <summary>Human-readable explanation of why this SubKind was chosen.</summary>
        public string Reason { get; set; }

        /// <summary>Evidence snippets / rules that fired for this type.</summary>
        public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Full C# text for this type declaration (including attributes, XML docs, etc.).
        /// This is the symbol-level text that can be fed into chunking / embedding.
        /// </summary>
        public string SymbolText { get; set; }
    }
}
