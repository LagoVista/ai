using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    public class TitleDescriptionCatalog
    {
        public int Version { get; set; } = 1;
        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;

        public List<TitleDescriptionCatalogEntry> Refined { get; } = new List<TitleDescriptionCatalogEntry>();
        public List<TitleDescriptionCatalogEntry> Warnings { get; } = new List<TitleDescriptionCatalogEntry>();
        public List<TitleDescriptionCatalogEntry> Failures { get; } = new List<TitleDescriptionCatalogEntry>();
        public List<TitleDescriptionCatalogEntry> Skipped { get; } = new List<TitleDescriptionCatalogEntry>();
        public List<DomainCatalogEntry> Domains { get; } = new List<DomainCatalogEntry>();
    }

    public enum CatalogEntryKind
    {
        Model,
        Domain
    }

    public class TitleDescriptionCatalogEntry
    {
        public CatalogEntryKind Kind { get; set; }
        public string RepoId { get; set; }
        public string File { get; set; }
        public string FileHash { get; set; }
        public string SymbolName { get; set; }
        public string IndexVersion { get; set; }
        public DateTime Timestamp { get; set; }

        public string OriginalTitle { get; set; }
        public string OriginalDescription { get; set; }
        public string OriginalHelp { get; set; }

        public string RefinedTitle { get; set; }
        public string RefinedDescription { get; set; }
        public string RefinedHelp { get; set; }

        public string TitleResourceKey { get; set; }
        public string DescriptionResourceKey { get; set; }
        public string HelpResourceKey { get; set; }

        public string ReasonOrNotes { get; set; }
    }

    public class DomainCatalogEntry
    {
        public string RepoId { get; set; }
        public string File { get; set; }
        public string FileHash { get; set; }
        public string SymbolName { get; set; }
        public string DomainKey { get; set; }
        public string IndexVersion { get; set; }
        public DateTime Timestamp { get; set; }

        public string OriginalTitle { get; set; }
        public string OriginalDescription { get; set; }
        public string RefinedTitle { get; set; }
        public string RefinedDescription { get; set; }

        public List<DomainEntitySummary> Entities { get; set; } = new List<DomainEntitySummary>();
    }

    public class DomainEntitySummary
    {
        public string SymbolName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
