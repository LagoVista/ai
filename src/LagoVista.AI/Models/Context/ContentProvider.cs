using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace LagoVista.AI.Models.Context
{
    /// <summary>
    /// AGN-030: ContentProvider ("File Cabinet") for prompt-relevant context.
    ///
    /// - The ContentProvider contains multiple registers (drawers).
    /// - Each register declares exactly one classification: Session or Consumable.
    /// - Line items are stored in-register in insertion order.
    /// - Business logic for assembling prompts or orchestrating tools does not belong here.
    /// </summary>
    public sealed class ContentProvider
    {
        public const string ToolCallManifestRegisterName = "ToolCallManifest";

        [JsonProperty("registers")]
        private readonly Dictionary<string, ContentRegister> _registers = new Dictionary<string, ContentRegister>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns a snapshot of registers.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyCollection<ContentRegister> Registers => _registers.Values.ToList().AsReadOnly();

        /// <summary>
        /// Get an existing register by name, or create it with the provided classification.
        /// </summary>
        public ContentRegister GetOrCreateRegister(string name, ContextClassification classification)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Register name is required.", nameof(name));

            if (_registers.TryGetValue(name, out var existing))
            {
                if (existing.Classification != classification)
                {
                    throw new InvalidOperationException($"Register '{name}' already exists with classification '{existing.Classification}', requested '{classification}'.");
                }

                return existing;
            }

            var created = new ContentRegister(name, classification);
            _registers[name] = created;
            return created;
        }

        /// <summary>
        /// Try get register by name.
        /// </summary>
        public bool TryGetRegister(string name, out ContentRegister register)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                register = null!;
                return false;
            }

            return _registers.TryGetValue(name, out register!);
        }

        /// <summary>
        /// Convenience accessor for the ToolCallManifest register (Consumable).
        /// </summary>
        public ContentRegister ToolCallManifest => GetOrCreateRegister(ToolCallManifestRegisterName, ContextClassification.Consumable);

        /// <summary>
        /// Clears all items for all Consumable registers.
        /// </summary>
        public void ClearConsumables()
        {
            foreach (var reg in _registers.Values.Where(r => r.Classification == ContextClassification.Consumable))
            {
                reg.Clear();
            }
        }
    }
}
