using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// In-memory catalog of all Aptix Agent Modes.
    /// Backed by AGN-013. Modes are immutable at runtime.
    /// </summary>
    public sealed class AgentModeCatalogService : IAgentModeCatalogService
    {

        public AgentModeCatalogService()
        {
            ValidateModes(GetModes());
        }

        private static void ValidateModes(IReadOnlyList<AgentMode> modes)
        {
            if (modes == null || modes.Count == 0)
                throw new InvalidOperationException("AgentModeCatalogService requires at least one mode.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var defaultCount = 0;

            foreach (var mode in modes)
            {
                if (string.IsNullOrWhiteSpace(mode.Id))
                    throw new InvalidOperationException("Agent mode Id is required.");

                if (!IsValidGuidNoHyphens(mode.Id))
                    throw new InvalidOperationException($"Agent mode Id '{mode.Id}' must be a 32-character uppercase hex GUID with no hyphens.");

                if (!ids.Add(mode.Id))
                    throw new InvalidOperationException($"Duplicate agent mode Id detected: '{mode.Id}'.");

                if (string.IsNullOrWhiteSpace(mode.Key))
                    throw new InvalidOperationException("Agent mode Key is required.");

                if (!IsValidSnakeCase(mode.Key))
                    throw new InvalidOperationException($"Agent mode Key '{mode.Key}' must be snake_case (lowercase letters, digits, underscores).");

                if (!keys.Add(mode.Key))
                    throw new InvalidOperationException($"Duplicate agent mode Key detected: '{mode.Key}'.");

                if (string.IsNullOrWhiteSpace(mode.WhenToUse))
                    throw new InvalidOperationException($"Agent mode '{mode.Key}' must define a non-empty WhenToUse line.");

                if (mode.IsDefault)
                    defaultCount++;

                if (!string.IsNullOrWhiteSpace(mode.Status) &&
                    mode.Status != "active" &&
                    mode.Status != "experimental" &&
                    mode.Status != "deprecated")
                {
                    throw new InvalidOperationException($"Agent mode '{mode.Key}' has invalid Status '{mode.Status}'.");
                }
            }

            if (defaultCount == 0)
                throw new InvalidOperationException("Exactly one agent mode must be marked IsDefault=true, but none were found.");

            if (defaultCount > 1)
                throw new InvalidOperationException("Exactly one agent mode must be marked IsDefault=true, but multiple were found.");
        }

        private static bool IsValidGuidNoHyphens(string id)
        {
            if (id.Length != 32) return false;
            for (var i = 0; i < id.Length; i++)
            {
                var c = id[i];
                var isDigit = c >= '0' && c <= '9';
                var isHexUpper = c >= 'A' && c <= 'F';
                if (!isDigit && !isHexUpper) return false;
            }
            return true;
        }

        private static bool IsValidSnakeCase(string key)
        {
            foreach (var c in key)
            {
                var isLower = c >= 'a' && c <= 'z';
                var isDigit = c >= '0' && c <= '9';
                var isUnderscore = c == '_';
                if (!isLower && !isDigit && !isUnderscore) return false;
            }
            return true;
        }

        private static string[] AppendCommonTools(string[] tools)
        {
            tools = tools.Append(ModeChangeTool.ToolName).ToArray();
            tools = tools.Append(AgentListModesTool.ToolName).ToArray();
            tools = tools.Append(ListWorkflowsTool.ToolName).ToArray();

            return tools;
        }

        public AgentMode GetMode(string modeKey)
        {
            if (string.IsNullOrWhiteSpace(modeKey))
                throw new InvalidOperationException(
                    "AgentModeCatalogService.GetMode was called with an empty or null mode key.");

            var mode = GetModes().FirstOrDefault(m =>
                string.Equals(m.Key, modeKey, StringComparison.OrdinalIgnoreCase));

            if (mode == null)
            {
                var valid = string.Join(", ", GetModes().Select(m => m.Key));
                throw new InvalidOperationException(
                    $"AgentModeCatalogService.GetMode was called with unknown mode key '{modeKey}'. " +
                    $"Valid mode keys are: {valid}.");
            }

            return mode;
        }

        public List<string> GetToolsForMode(string modeKey)
        {
            var mode = GetMode(modeKey); // already does strict validation
            // Defensive copy
            return new List<string>(mode.AssociatedToolIds ?? Array.Empty<string>());
        }

        private IReadOnlyList<AgentMode> GetModes()
        {
            var modes = new List<AgentMode>
                {
                new AgentMode
                {
                    // ORIGINAL: 3F8E4F37-7F7A-4C18-9C7F-6A8B9F945C11
                    Id = "3F8E4F377F7A4C189C7F6A8B9F945C11",
                    Key = "general",
                    DisplayName = "General",
                    Description = "General-purpose assistance for everyday Q&A, explanation, and lightweight help.",
                    WhenToUse = "Use this mode for everyday Q&A, explanation, and lightweight assistance.",
                    IsDefault = true,
                    Status = "active",
                    Version = "v1",

                    WelcomeMessage = "You are now in General mode. Use this mode for broad questions and lightweight assistance.",
                    ModeInstructions = new[] { "Provide clear, concise answers.", "Do not assume the user is in a structured Aptix workflow unless they say so.", "If the user appears to be asking for DDR or workflow work, consider recommending a mode switch." },
                    BehaviorHints = new[] { "preferConciseResponses" },
                    HumanRoleHints = new[] { "The human is asking general questions or exploring ideas." },
                    ExampleUtterances = new[] { "Can you explain how this works?", "Help me reason through this problem.", "What are the pros and cons of this approach?" },

                    AssociatedToolIds = new[] {HelloWorldTool.ToolName, HelloWorldClientTool.ToolName, AddAgentModeTool.ToolName, UpdateAgentModeTool.ToolName},
                    ToolGroupHints = new[] { "general" },
                    RagScopeHints = new[] { "boost:docs_general" },

                    StrongSignals = new[] { "general question", "explain this", "help me understand" },
                    WeakSignals = new string[] {}
                },

                new AgentMode
                {
                    // ORIGINAL: A9E1F9C1-5A0C-4F8D-9AF5-1F3E8B2A6D22
                    Id = "A9E1F9C15A0C4F8D9AF51F3E8B2A6D22",
                    Key = "ddr_authoring",
                    DisplayName = "DDR Authoring",
                    Description = "Structured creation, refinement, and validation of Aptix DDR specifications following SYS-001.",
                    WhenToUse = "Use this mode when the user wants to create, refine, or validate Aptix DDR specifications following SYS-001.",
                    IsDefault = false,
                    Status = "active",
                    Version = "v1",

                    WelcomeMessage = "You are now in DDR Authoring mode. We will work with SYS-001 to create or refine DDRs.",
                    ModeInstructions = new[] { "Follow SYS-001 when creating or updating DDRs.", "Drive the user through the DDR workflow step-by-step instead of dumping a full document at once.", "Ask for explicit confirmation before changing DDR status or marking a DDR as approved." },
                    BehaviorHints = new[] { "preferStructuredOutput", "avoidDestructiveTools" },
                    HumanRoleHints = new[] { "The human is authoring or editing a DDR.", "The human may paste existing DDR text for refinement." },
                    ExampleUtterances = new[] { "Help me draft a new DDR for this tool.", "Refine this DDR section to be more concise.", "Update this DDR to reflect the new workflow rules." },

                    AssociatedToolIds = new[] {  GetTlaCatalogAgentTool.ToolName, AddTlaAgentTool.ToolName, ListDdrsAgentTool.ToolName, GetDdrAgentTool.ToolName, CreateDdrAgentTool.ToolName, SetGoalAgentTool.ToolName,
                        AddChaptersAgentTool.ToolName, AddChapterAgentTool.ToolName,
                        SetDdrStatusAgentTool.ToolName, ListChaptersAgentTool.ToolName, ApproveChapterAgentTool.ToolName, ApproveDdrAgentTool.ToolName, ApproveGoalAgentTool.ToolName,  MoveDdrTlaAgentTool.ToolName, UpdateDdrMetadataAgentTool.ToolName,
                        ReorderChaptersAgentTool.ToolName,UpdateChapterSummaryAgentTool.ToolName, UpdateChapterDetailsAgentTool.ToolName  },
                    ToolGroupHints = new[] { "ddr" },
                    RagScopeHints = new[] { "boost:ddr_specs" },

                    StrongSignals = new[] { "create a ddr", "refine this ddr", "work on a spec" },
                    WeakSignals = new[] { "improve this document", "rewrite this section" }
                },

                        new AgentMode
                {
                    // ORIGINAL: A9E1F9C1-5A0C-4F8D-9AF5-1F3E8B2A6D22
                    Id = "D11C9951BA6E4C679DD722996784884C",
                    Key = "ddr_import",
                    DisplayName = "DDR Importing",
                    Description = "A process that let's the user upload DDR's to be imported into more formal storage.",
                    WhenToUse = "Use this mode when the user wants to import a DDR.",
                    IsDefault = false,
                    Status = "active",
                    Version = "v1",

                    WelcomeMessage = "You are now in DDR Import mode. Please paste your DDR into the chat window and press send.",
                    ModeInstructions = new[] { "Follow the prompt as supplied." },
                    BehaviorHints = new[] { "preferStructuredOutput", "avoidDestructiveTools" },
                    HumanRoleHints = new[] { "The human is importing a DDR.", "The human may paste existing DDR text for refinement." },
                    ExampleUtterances = new[] { "I need to import a DDR.", "Please import a DDr." },

                    AssociatedToolIds = new[] {  ModeChangeTool.ToolName, AgentListModesTool.ToolName, RequestUserApprovalAgentTool.ToolName, GetTlaCatalogAgentTool.ToolName, AddTlaAgentTool.ToolName,
                      AddChaptersAgentTool.ToolName, AddChapterAgentTool.ToolName,
                        ListDdrsAgentTool.ToolName, GetDdrAgentTool.ToolName, CreateDdrAgentTool.ToolName, SetGoalAgentTool.ToolName, SetDdrStatusAgentTool.ToolName, ListChaptersAgentTool.ToolName, ApproveChapterAgentTool.ToolName, 
                        ApproveDdrAgentTool.ToolName, ApproveGoalAgentTool.ToolName,  MoveDdrTlaAgentTool.ToolName, UpdateDdrMetadataAgentTool.ToolName, ReorderChaptersAgentTool.ToolName,UpdateChapterSummaryAgentTool.ToolName, UpdateChapterDetailsAgentTool.ToolName  },
                    ToolGroupHints = new[] { "ddr" },
                    RagScopeHints = new[] { "boost:ddr_specs" },

                    StrongSignals = new[] { "import a ddr" },
                    WeakSignals = new[] { "import a document", "create a document" }
                },

                new AgentMode
                {
                    // ORIGINAL: 0FB81E6A-8337-444B-A00A-0CE28E3A1F78
                    Id = "0FB81E6A8337444BA00A0CE28E3A1F78",
                    Key = "workflow_authoring",
                    DisplayName = "Workflow Authoring",
                    Description = "Creation, refinement, and validation of Aptix agent workflows using the Workflow Registry Tool (TUL-006).",
                    WhenToUse = "Use this mode when defining, editing, or validating Aptix workflows using TUL-006.",
                    IsDefault = false,
                    Status = "active",
                    Version = "v1",

                    WelcomeMessage = "You are now in Workflow Authoring mode. We will work with TUL-006 to define or refine workflows.",
                    ModeInstructions = new[] { "Follow TUL-006 when creating or updating workflows.", "Use structured JSON for workflow fields.", "Ask for user confirmation before modifying or publishing workflows." },
                    BehaviorHints = new[] { "preferStructuredOutput", "avoidDestructiveTools" },
                    HumanRoleHints = new[] { "The human is defining or refining an Aptix workflow." },
                    ExampleUtterances = new[] { "Create a new workflow.", "Update this workflow's steps.", "Show me the manifest for workflow X." },

                    AssociatedToolIds = new[] { ModeChangeTool.ToolName, AgentListModesTool.ToolName, RequestUserApprovalAgentTool.ToolName, CreateWorkflowTool.ToolName, GetWorkflowManifestTool.ToolName, UpdateWorkflowTool.ToolName, ListWorkflowsTool.ToolName, DeleteWorkflowTool.ToolName },
                    ToolGroupHints = new[] { "workflow" },
                    RagScopeHints = new[] { "boost:workflow_specs" },

                    StrongSignals = new[] { "create a workflow", "edit workflow", "workflow instructions" },
                    WeakSignals = new[] { "improve this process", "show workflow details" }
                }
            };

            foreach(var mode in modes)
            {
                mode.AssociatedToolIds = AppendCommonTools(mode.AssociatedToolIds);
            }

            return modes;
        }

        public string GetWelcomeMessage(string modeKey)
        {
            var mode = GetModes().SingleOrDefault(m =>
                string.Equals(m.Key, modeKey, StringComparison.OrdinalIgnoreCase));

            if(mode == null)
                throw new RecordNotFoundException(typeof(AgentMode).Name, modeKey)
                ;
            return mode.WelcomeMessage;
        }

        public Task<IReadOnlyList<AgentModeSummary>> GetAllModesAsync(CancellationToken cancellationToken)
            => Task.FromResult(GetModes().Select(m => m.CreateSummary()).ToList().AsReadOnly() as IReadOnlyList<AgentModeSummary>);

        public string BuildSystemPrompt(string currentModeKey)
        {
            // Resolve current mode; fall back to default if unknown or empty.
            var current = GetModes().FirstOrDefault(m =>
                !string.IsNullOrWhiteSpace(currentModeKey) &&
                string.Equals(m.Key, currentModeKey, StringComparison.OrdinalIgnoreCase))
                ?? GetModes().First(m => m.IsDefault);

            var sb = new StringBuilder();

            sb.AppendLine($"Current Mode: {current.Key}");
            sb.AppendLine();
            sb.AppendLine("Available Modes:");

            foreach (var mode in GetModes())
            {
                sb.Append("- ")
                  .Append(mode.Key)
                  .Append(": ")
                  .AppendLine(mode.WhenToUse ?? mode.Description ?? string.Empty);
            }

            sb.AppendLine();
            sb.AppendLine("Mode Switching:");
            sb.AppendLine("- If the user’s request clearly matches another mode’s \"when to use\" description, you may recommend switching.");
            sb.AppendLine("- If the user expresses interest in switching, follow the instructions in the agent_change_mode tool.");
            sb.AppendLine("- If you need more detail about modes, call the agent_list_modes tool.");

            return sb.ToString();
        }

    }
}
