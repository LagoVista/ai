using LagoVista.Core.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Visibility classification for a workflow. This is persisted as part of the workflow definition.
    /// </summary>
    public enum WorkflowVisibility
    {
        Public,
        Hidden,
        Experimental
    }

    /// <summary>
    /// Lifecycle status for a workflow. This is persisted as part of the workflow definition.
    /// </summary>
    public enum WorkflowStatus
    {
        Draft,
        Active,
        Deprecated,
        Disabled
    }

    /// <summary>
    /// Describes a single required input the LLM must collect from the user before or during workflow execution.
    /// This is part of the persisted workflow definition.
    /// </summary>
    public class WorkflowRequiredInput
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Optional hint about the expected input type (for example: "string", "int", "markdown", "file-path").
        /// </summary>
        public string InputType { get; set; }
    }

    /// <summary>
    /// Describes a follow-up option that can be offered to the user after the workflow completes.
    /// This is part of the persisted workflow definition.
    /// </summary>
    public class WorkflowFollowUpOption
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Optional target workflow that should be suggested if the user selects this follow-up.
        /// </summary>
        public string TargetWorkflowId { get; set; }
    }

    /// <summary>
    /// Full workflow definition / manifest as persisted by the agent and consumed by the Workflow Registry Tool (TUL-006).
    /// This object is the canonical shape that is stored and versioned.
    /// </summary>
    public class WorkflowDefinition : EntityBase
    {
        /// <summary>
        /// Stable identifier for the workflow (for example: "create_ddr").
        /// </summary>
        public string WorkflowId { get; set; }

        public string Title { get; set; }

        /// <summary>
        /// Example trigger phrases or patterns that indicate the user wants to start this workflow.
        /// </summary>
        public List<string> UserIntentPatterns { get; set; } = new List<string>();

        /// <summary>
        /// Inputs that the LLM must collect from the user before or during execution.
        /// </summary>
        public List<WorkflowRequiredInput> RequiredInputs { get; set; } = new List<WorkflowRequiredInput>();

        /// <summary>
        /// The instruction text the agent sends to the LLM describing how to execute the workflow step by step.
        /// </summary>
        public string InstructionText { get; set; }

        /// <summary>
        /// Logical tool names (IAgentTool.Name) that the LLM is allowed to call while executing this workflow.
        /// </summary>
        public List<string> PermittedTools { get; set; } = new List<string>();

        /// <summary>
        /// Natural-language description of how the LLM should determine that the workflow is complete.
        /// </summary>
        public string CompletionCriteria { get; set; }

        /// <summary>
        /// Optional follow-up options that may be suggested after the workflow completes.
        /// </summary>
        public List<WorkflowFollowUpOption> FollowUpOptions { get; set; } = new List<WorkflowFollowUpOption>();

        /// <summary>
        /// Optional natural-language preconditions that must hold before running this workflow.
        /// Evaluation and enforcement are handled outside of this model.
        /// </summary>
        public List<string> Preconditions { get; set; } = new List<string>();

        /// <summary>
        /// Free-form notes for the LLM or operator (warnings, caveats, or hints).
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Semantic version for this workflow definition (for example: "1.0.0").
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Lifecycle status for this workflow.
        /// </summary>
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Active;

        /// <summary>
        /// Visibility classification for this workflow.
        /// </summary>
        public WorkflowVisibility Visibility { get; set; } = WorkflowVisibility.Public;
    }
}
