    using System;

    namespace LagoVista.AI.Rag.Chunkers.Models
    {
        /// <summary>
        /// Normalized chunk for a C# source component (file, type, member).
        /// Replaces RawChunk/RagChunk for C# assets.
        /// </summary>
        public class CSharpComponentChunk
        {
            /// <summary>
            /// Symbol name (method, type, property, etc.).
            /// </summary>
            public string SymbolName { get; set; }

            /// <summary>
            /// High-level kind: "file", "type", "method", "property", "field", "event", etc.
            /// </summary>
            public string SymbolKind { get; set; }

            /// <summary>
            /// Optional grouping key (often matches SymbolKind).
            /// </summary>
            public string SectionKey { get; set; }

            /// <summary>
            /// 1-based starting line number in the original file.
            /// </summary>
            public int LineStart { get; set; }

            /// <summary>
            /// 1-based ending line number in the original file (inclusive).
            /// </summary>
            public int LineEnd { get; set; }

            /// <summary>
            /// 0-based character index in the original file where this chunk begins.
            /// </summary>
            public int StartCharacter { get; set; }

            /// <summary>
            /// 0-based character index in the original file where this chunk ends (exclusive).
            /// </summary>
            public int EndCharacter { get; set; }

            /// <summary>
            /// Pre-computed token estimate for this chunk.
            /// </summary>
            public int EstimatedTokens { get; set; }

            /// <summary>
            /// Normalized text for this chunk, including any injected summary comment.
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// Sequential ordering number for this chunk within the file.
            /// </summary>
            public int PartIndex { get; set; }

            /// <summary>
            /// Total number of chunks produced for the file.
            /// </summary>
            public int PartTotal { get; set; }
        }
    }
