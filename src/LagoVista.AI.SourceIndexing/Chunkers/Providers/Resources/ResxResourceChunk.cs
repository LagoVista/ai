using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0051 – Normalized representation of a single RESX <data> entry.
    /// This is the object we will embed and later map into RagVectorPayload.
    /// </summary>
    public sealed class ResxResourceChunk
    {
        // Identity
        public string ResourceKey { get; set; }          // e.g. "Save", "Error_DeviceNotFound"
        public string ResourceValue { get; set; }        // Localized text
        public string Comment { get; set; }              // Optional developer comment

        // Location
        public string SourceFile { get; set; }           // File name, e.g. Resources.en-US.resx
        public string RelativePath { get; set; }         // Repo-relative path

        // Localization
        public string Culture { get; set; }              // "" | "en" | "en-US" etc.
        public ResourceSubKind SubKind { get; set; }     // UiString, ErrorMessage, etc.

        // Aggregated usage hints (populated by IDX-0052 – optional for V1)
        public int UsageCount { get; set; }
        public IReadOnlyList<string> PrimaryUsageKinds { get; set; }

        // Convenience flags
        public bool IsDuplicate { get; set; }
        public bool IsUiSharedCandidate { get; set; }
    }

    /// <summary>
    /// Fine-grained classification for resource entries.
    /// </summary>
    public enum ResourceSubKind
    {
        Unknown = 0,
        UiString = 1,
        ErrorMessage = 2,
        StatusLabel = 3,
        ValidationMessage = 4,
        CommandLabel = 5,
        HelpText = 6,
        Other = 99
    }
}
