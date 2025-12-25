namespace LagoVista.AI.Models
{
    /// <summary>
    /// Atomic knowledge entry inside an AKP lane.
    /// - Instruction/Reference: Id is DDR (TLA-XXX), Content is resolved consumption field.
    /// - Tool: Id is tool_name; Content is optional tool usage guidance.
    /// </summary>
    public sealed class KnowledgeItem
    {
        public KnowledgeKind Kind { get; set; }

        /// <summary>
        /// DDR ID (TLA-XXX) or tool_name.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Resolved consumption content for DDR-based items, optional for tools.
        /// </summary>
        public string Content { get; set; }
    }
}
