TUL-012 Integration Notes

This bundle adds ModeEntryBootstrapService (src/lagoVista.AI/Services/ModeEntryBootstrapService.cs)

To wire into AgentReasoner:
- After mode change is detected and request.Mode / lastResponse.Mode are updated
- Resolve the AgentMode instance for the new mode
- Create a ModeEntryBootstrapRequest with:
  - Mode = resolved AgentMode
  - ModeKey = newModeFromTool
  - ToolContext = the existing AgentToolExecutionContext (toolContext)
- Call await _modeEntryBootstrapService.ExecuteAsync(...)
- If it fails:
  - surface error to user (per TUL-012) and stop processing

This repo snippet did not include the full AgentReasoner file path or DI registration.
Apply the integration in the existing AgentReasoner class and register ModeEntryBootstrapService in DI.
