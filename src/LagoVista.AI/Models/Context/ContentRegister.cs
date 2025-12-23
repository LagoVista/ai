using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace LagoVista.AI.Models.Context
{
    /// <summary>
    /// AGN-030: A named container within the ContentProvider that holds an ordered set of line items.
    ///
    /// Notes:
    /// - A register declares exactly one ContextClassification (Session or Consumable).
    /// - The register is primarily a state container; business logic should live elsewhere.
    /// </summary>
    public sealed class ContentRegister
    {
        public ContentRegister(string name, ContextClassification classification)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Register name is required.", nameof(name));
            Name = name;
            Classification = classification;
        }

        /// <summary>
        /// Stable register name/key.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; }

        /// <summary>
        /// Session or Consumable (AGN-030).
        /// </summary>
        [JsonProperty("classification")]
        public ContextClassification Classification { get; }

        /// <summary>
        /// Ordered line items stored in this register.
        /// </summary>
        [JsonProperty("items")]
        public List<object> Items { get; } = new List<object>();

        /// <summary>
        /// Read-only view of items.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<object> ReadOnlyItems => new ReadOnlyCollection<object>(Items);

        /// <summary>
        /// Adds a line item to the register.
        /// </summary>
        public void Add(object item)
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
