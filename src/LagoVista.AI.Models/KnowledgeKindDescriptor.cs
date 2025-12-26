using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Rendering descriptor for a KnowledgeKind.
    /// PKP can render blocks generically using these descriptors.
    /// </summary>
    public sealed class KnowledgeKindDescriptor
    {
        public KnowledgeKind Kind { get; set; }

        /// <summary>
        /// Human-friendly title for the block (optional).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Marker indicating the start of the rendered block (optional).
        /// </summary>
        public string BeginMarker { get; set; }

        /// <summary>
        /// Marker indicating the end of the rendered block (optional).
        /// </summary>
        public string EndMarker { get; set; }
        // Lanes (always present)
        public KnowledgeLane SessionKnowledge { get; set; } = new KnowledgeLane();
        public KnowledgeLane ConsumableKnowledge { get; set; } = new KnowledgeLane();

        /// <summary>
        /// One-line instruction explaining what this block is and how to use it (optional).
        /// </summary>
        public string InstructionLine { get; set; }
    }
}
