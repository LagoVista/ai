using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Lightweight catalog row used in list_workflows responses.
    /// This is a transient DTO used for LLM/tool communication, not the persisted definition.
    /// </summary>
    public class WorkflowCatalogItem
    {
        public string WorkflowId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public WorkflowStatus Status { get; set; }

        public WorkflowVisibility Visibility { get; set; }

        public string Version { get; set; }
    }

    /// <summary>
    /// Match result used when the registry tool returns candidate workflows for a user message.
    /// This is a transient DTO.
    /// </summary>
    public class WorkflowMatch
    {
        public string WorkflowId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Optional score in the range [0,1] indicating how well the workflow matches the user message.
        /// Interpretation is implementation-specific.
        /// </summary>
        public double? MatchScore { get; set; }

        public WorkflowStatus Status { get; set; }

        public WorkflowVisibility Visibility { get; set; }
    }

    /// <summary>
    /// Arguments payload passed from the LLM into the Workflow Registry Tool (TUL-006).
    /// This is transient and reflects the JSON arguments schema.
    /// </summary>
    public class WorkflowRegistryRequest
    {
        /// <summary>
        /// Operation to perform: "list_workflows", "get_workflow_manifest", or "match_workflow".
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Workflow identifier, required for "get_workflow_manifest".
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// User message text to match against known workflows, required for "match_workflow".
        /// </summary>
        public string UserMessage { get; set; }
    }

    /// <summary>
    /// Response shape for list_workflows.
    /// </summary>
    public class WorkflowRegistryCatalogResponse
    {
        public List<WorkflowCatalogItem> Workflows { get; set; } = new List<WorkflowCatalogItem>();
    }

    /// <summary>
    /// Response shape for get_workflow_manifest.
    /// </summary>
    public class WorkflowRegistryManifestResponse
    {
        public WorkflowDefinition Workflow { get; set; }
    }

    /// <summary>
    /// Response shape for match_workflow.
    /// </summary>
    public class WorkflowRegistryMatchResponse
    {
        public List<WorkflowMatch> Matches { get; set; } = new List<WorkflowMatch>();
    }

    /// <summary>
    /// Single validation error used by authoring tools. This is transient and not persisted.
    /// </summary>
    public class WorkflowAuthoringError
    {
        public string Field { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// Generic response envelope for workflow authoring tools.
    /// Mirrors the JSON structure described in the TUL-006 DDR.
    /// This is used for LLM/tool communication.
    /// </summary>
    public class WorkflowAuthoringResponse
    {
        public bool Ok { get; set; }

        public WorkflowDefinition Workflow { get; set; }

        public List<WorkflowDefinition> Workflows { get; set; } = new List<WorkflowDefinition>();

        public List<string> Messages { get; set; } = new List<string>();

        public List<WorkflowAuthoringError> Errors { get; set; } = new List<WorkflowAuthoringError>();
    }
}
