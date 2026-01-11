using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Root immutable snapshot of the domain catalog.
    /// </summary>
    public sealed class DomainCatalog
    {
        [JsonConstructor]
        public DomainCatalog(
            IReadOnlyList<DomainEntry> domains,
            IReadOnlyList<ModelClassEntry> classes)
        {
            Domains = new ReadOnlyCollection<DomainEntry>(
                domains != null ? new List<DomainEntry>(domains) : new List<DomainEntry>());

            Classes = new ReadOnlyCollection<ModelClassEntry>(
                classes != null ? new List<ModelClassEntry>(classes) : new List<ModelClassEntry>());
        }

        [JsonProperty]
        public IReadOnlyList<DomainEntry> Domains { get; }

        [JsonProperty]
        public IReadOnlyList<ModelClassEntry> Classes { get; }
    }

    /// <summary>
    /// Immutable representation of a domain and its model classes.
    /// </summary>
    public sealed class DomainEntry
    {
        [JsonConstructor]
        public DomainEntry(
            string domainKey,
            string title,
            string description,
            IReadOnlyList<ModelClassEntry> classes)
        {
            if (string.IsNullOrWhiteSpace(domainKey))
                throw new ArgumentException("DomainKey is required.", nameof(domainKey));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description is required.", nameof(description));

            DomainKey = domainKey;
            Title = title;
            Description = description;
            Classes = new ReadOnlyCollection<ModelClassEntry>(
                classes != null ? new List<ModelClassEntry>(classes) : new List<ModelClassEntry>());
        }

        [JsonProperty]
        public string DomainKey { get; }

        [JsonProperty]
        public string Title { get; }

        [JsonProperty]
        public string Description { get; }

        [JsonProperty]
        public IReadOnlyList<ModelClassEntry> Classes { get; }
    }

    /// <summary>
    /// Immutable representation of an "interesting" model class
    /// (decorated with [EntityDescription]).
    /// </summary>
    public sealed class ModelClassEntry
    {
        [JsonConstructor]
        public ModelClassEntry(
            string domainKey,
            string className,
            string qualifiedClassName,
            string title,
            string description,
            string helpText,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(domainKey))
                throw new ArgumentException("DomainKey is required.", nameof(domainKey));

            if (string.IsNullOrWhiteSpace(className))
                throw new ArgumentException("ClassName is required.", nameof(className));

            if (string.IsNullOrWhiteSpace(qualifiedClassName))
                throw new ArgumentException("QualifiedClassName is required.", nameof(qualifiedClassName));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description is required.", nameof(description));

            if (string.IsNullOrWhiteSpace(helpText))
                throw new ArgumentException("HelpText is required.", nameof(helpText));

            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("RelativePath is required.", nameof(relativePath));

            DomainKey = domainKey;
            ClassName = className;
            QualifiedClassName = qualifiedClassName;
            Title = title;
            Description = description;
            HelpText = helpText;
            RelativePath = relativePath;
        }

        [JsonProperty]
        public string DomainKey { get; }

        [JsonProperty]
        public string ClassName { get; }

        [JsonProperty]
        public string QualifiedClassName { get; }

        [JsonProperty]
        public string Title { get; }

        [JsonProperty]
        public string Description { get; }

        [JsonProperty]
        public string HelpText { get; }

        [JsonProperty]
        public string RelativePath { get; }
    }
}
