namespace LagoVista.AI.Models
{
    /// <summary>
    /// Semantic kind for items inside an Agent Knowledge Pack (AKP).
    /// </summary>
    public enum KnowledgeKind
    {
        AgentModelContext,
        AgentContextInstructions,
        AgentPersona,
        Instruction,
        Reference,
        ToolUsage,
        ToolSummary,
        Kfr,
        Rag,
        NewChapterInitialPrompt
    }
}
