using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace LagoVista.AI.Models.Context
{
    /// <summary>
    /// AGN-030: A named container within the PromptKnowledgeProvider that holds an ordered set of line items.
    ///
    /// AdditionalConfiguration:
    /// - A register declares exactly one ContextClassification (Session or Consumable).
    /// - The register is primarily a state container; business logic should live elsewhere.
    /// </summary>
    public sealed class ContentRegister
    {
        public ContentRegister(KnowledgeKind kind, ContextClassification classification)
        {
            Kind = kind;
            Classification = classification;
        }

        /// <summary>
        /// Stable register kind/key.
        /// </summary>
        [JsonProperty("kind")]
        public KnowledgeKind Kind { get; }

        /// <summary>
        /// Session or Consumable (AGN-030).
        /// </summary>
        [JsonProperty("classification")]
        public ContextClassification Classification { get; }

        /// <summary>
        /// Ordered line items stored in this register.
        /// </summary>
        [JsonProperty("items")]
        public List<string> Items { get; } = new List<string>();

        /// <summary>
        /// Read-only view of items.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<string> ReadOnlyItems => new ReadOnlyCollection<string>(Items);

        /// <summary>
        /// Adds a line item to the register.
        /// </summary>
        public void Add(string item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Items.Add(item);
        }

        /// <summary>
        /// Clears all items from the register.
        /// </summary>
        public void Clear() => Items.Clear();
    }
}
