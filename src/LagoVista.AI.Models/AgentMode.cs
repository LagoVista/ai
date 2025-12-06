using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Full design-time definition of a single Aptix Agent Mode.
    /// Backed by AGN-013. Instances are immutable at runtime and
    /// loaded into the Agent Mode Catalog at startup.
    /// </summary>
    public sealed class AgentMode
    {
        // 3.1 Identity & UI Metadata

        /// <summary>
        /// Canonical immutable key. GUID with hyphens removed.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Human-readable key, e.g. "General", "DDR Authoring".
        /// Used in prompts and mode switching.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Display name for UI surfaces. May match Key.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Short description of what this mode is and covers.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// One-line "when to use this mode" summary used in the
        /// Mode Catalog System Prompt Block.
        /// </summary>
        public string WhenToUse { get; set; }

        // 3.2 User Interaction Metadata

        /// <summary>
        /// Optional welcome message shown when entering this mode.
        /// </summary>
        public string WelcomeMessage { get; set; }

        /// <summary>
        /// Mode-specific behavior instructions for the LLM when this
        /// mode is active (go into the Active Mode Behavior Block).
        /// </summary>
        public string[] ModeInstructions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional structured hints like "preferStructuredOutput",
        /// "avoidDestructiveTools", etc.
        /// </summary>
        public string[] BehaviorHints { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Hints about the human's role in this mode, e.g.
        /// "The human is authoring DDRs", "The human is designing workflows".
        /// </summary>
        public string[] HumanRoleHints { get; set; } = Array.Empty<string>();

        // 3.3 Tools

        /// <summary>
        /// Tool IDs that are enabled when this mode is active.
        /// </summary>
        public string[] AssociatedToolIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional grouping hints for UI or LLM reasoning, e.g. "authoring",
        /// "read-only", "diagnostics".
        /// </summary>
        public string[] ToolGroupHints { get; set; } = Array.Empty<string>();

        // 3.4 RAG Scoping Metadata

        /// <summary>
        /// Simple hints for RAG collection and tag preferences, e.g.
        /// "boost:DDR_DDRs", "exclude:telemetry".
        /// </summary>
        public string[] RagScopeHints { get; set; } = Array.Empty<string>();

        // 3.5 Recognition Metadata

        /// <summary>
        /// Phrases strongly associated with this mode.
        /// </summary>
        public string[] StrongSignals { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Weaker hints that might lean toward this mode.
        /// </summary>
        public string[] WeakSignals { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Representative user utterances that clearly belong to this mode.
        /// </summary>
        public string[] ExampleUtterances { get; set; } = Array.Empty<string>();

        // 3.6 Lifecycle Metadata

        /// <summary>
        /// "active", "experimental", or "deprecated".
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Simple version string, e.g. "v1", "v1.1".
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// True if this is the default mode when no explicit mode is set.
        /// </summary>
        public bool IsDefault { get; set; }

        public AgentModeSummary CreateSummary()
        {
            return new AgentModeSummary
            {
                Id = this.Id,
                Key = this.Key,
                DisplayName = this.DisplayName,
                Description = this.Description ?? this.WhenToUse,
                SystemPromptSummary = this.WhenToUse,
                IsDefault = this.IsDefault,
                HumanRoleHints = this.HumanRoleHints ?? Array.Empty<string>(),
                ExampleUtterances = this.ExampleUtterances ?? Array.Empty<string>()
            };
        }
    }


    /// <summary>
    /// Minimal mode summary DTO exposed by IAgentModeCatalogService.
    /// </summary>
    public sealed class AgentModeSummary
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string SystemPromptSummary { get; set; }
        public bool IsDefault { get; set; }
        public string[] HumanRoleHints { get; set; }
        public string[] ExampleUtterances { get; set; }
    }
}
