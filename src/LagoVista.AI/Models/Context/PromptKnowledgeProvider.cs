using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace LagoVista.AI.Models.Context
{

    public enum PromptKnowledgeProviderStates
    {
        NotSet,
        Ready,
        Error
    }

    /// <summary>
    /// AGN-030: PromptKnowledgeProvider ("File Cabinet") for prompt-relevant context.
    ///
    /// - The PromptKnowledgeProvider contains multiple registers (drawers).
    /// - Each register declares exactly one classification: Session or Consumable.
    /// - Line items are stored in-register in insertion order.
    /// - Business logic for assembling prompts or orchestrating tools does not belong here.
    /// </summary>
    public sealed class PromptKnowledgeProvider
    {

        public const string ToolCallManifestRegisterName = "ToolCallManifest";

        [JsonProperty("registers")]
        private readonly Dictionary<KnowledgeKind, ContentRegister> _registers = new Dictionary<KnowledgeKind, ContentRegister>();

        /// <summary>
        /// Returns a snapshot of registers.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyCollection<ContentRegister> Registers => _registers.Values.ToList().AsReadOnly();


        public void Reset()
        {         
            ClearConsumables();
            ClearSession();
            ToolCallManifest.ToolCallResults.Clear();
            ToolCallManifest.ToolCalls.Clear();
        }

        /// <summary>
        /// Get an existing register by kind, or create it with the provided classification.
        /// </summary>
        public ContentRegister GetOrCreateRegister(KnowledgeKind kind, ContextClassification classification)
        {
            
            if (_registers.TryGetValue(kind, out var existing))
            {
                if (existing.Classification != classification)
                {
                    throw new InvalidOperationException($"Register '{kind}' already exists with classification '{existing.Classification}', requested '{classification}'.");
                }

                return existing;
            }

            var created = new ContentRegister(kind, classification);
            _registers[kind] = created;
            return created;
        }

        public List<string> ActiveTools { get; } = new List<string>();
 
        public PromptKnowledgeProviderStates State { get; set; } = PromptKnowledgeProviderStates.NotSet;

        /// <summary>
        /// Try get register by kind.
        /// </summary>
        public bool TryGetRegister(KnowledgeKind name, out ContentRegister register)
        {
            return _registers.TryGetValue(name, out register!);
        }

        public ToolCallManifest ToolCallManifest { get; set; } = new ToolCallManifest();

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

        public void ClearSession()
        {
            foreach (var reg in _registers.Values.Where(r => r.Classification == ContextClassification.Session))
            {
                reg.Clear();
            }
        }
    }
}
