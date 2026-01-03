using LagoVista.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Services
{
    public sealed partial class AgentKnowledgePackService
    {
        private static void AppendDirectLog(string label, IAgentKnowledgeProvider p, StringBuilder log)
        {
            log.Append($"[{label}.Direct]\\r\\n" +
                       $"\\t\\t-ActiveToolIds={string.Join("|", p.ActiveTools?.Select(x => x.Id) ?? Enumerable.Empty<string>())}\\r\\n" +
                       $"\\t\\t-AvailableToolIds={string.Join("|", p.AvailableTools?.Select(x => x.Id) ?? Enumerable.Empty<string>())}\\r\\n" +
                       $"\\t\\t-InstructionIds={string.Join("|", p.InstructionDdrs?.Select(x => x.Id) ?? Enumerable.Empty<string>())}\\r\\n" +
                       $"\\t\\t-ReferenceIds={string.Join("|", p.ReferenceDdrs?.Select(x => x.Id) ?? Enumerable.Empty<string>())}\\r\\n");
        }

        private static void AppendToolBoxLog(string parentLabel, AgentToolBox toolBox, StringBuilder log)
        {
            log.Append($"[{parentLabel}.ToolBox]={toolBox.Name}\\r\\n" +
                       $"\\t\\t-ActiveToolIds={string.Join("|", toolBox.ActiveTools.Select(x => x.Id))}\\r\\n" +
                       $"\\t\\t-AvailableToolIds={string.Join("|", toolBox.AvailableTools.Select(x => x.Id))}\\r\\n" +
                       $"\\t\\t-InstructionIds={string.Join("|", toolBox.InstructionDdrs.Select(x => x.Id))}\\r\\n" +
                       $"\\t\\t-ReferenceIds={string.Join("|", toolBox.ReferenceDdrs.Select(x => x.Id))}\\r\\n");
        }

        private static void EnsureKindCatalog(AgentKnowledgePack pack)
        {
            pack.KindCatalog[KnowledgeKind.Instruction] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Instruction,
                Title = "Instructions",
                BeginMarker = "## Directive DDRs",
                EndMarker = "\r\n",
                InstructionLine = @"These DDRs contain authoritative instructions.
Their contents are provided in full and must be followed when relevant.

"
            };

            pack.KindCatalog[KnowledgeKind.Reference] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.Reference,
                Title = "References",
                BeginMarker = "## Reference DDRs",
                EndMarker = "\r\n",
                InstructionLine = @"These DDRs are not loaded.

Each entry is identified by its ID and a brief description. 
Use the ID exactly as shown when requesting a DDR or tool.
If additional detail is required, request the DDR explicitly using get_ddr.
Request a Reference DDR only if its contents are required to complete the task.

"
            };

            pack.KindCatalog[KnowledgeKind.AgentContextInstructions] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.AgentContextInstructions,
                Title = "Agent Context Instructions",
                BeginMarker = "## Agent Context Instructions",
                EndMarker = "\r\n",
                InstructionLine = "The following are instructions that should be followed from the agent."
            };

            pack.KindCatalog[KnowledgeKind.ToolSummary] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.ToolSummary,
                Title = "Available Tools",
                BeginMarker = "## Available Tools",
                EndMarker = "\r\n",
                InstructionLine = @"Tools listed here are not loaded.
To request a tool, call activate_tools with tool_ids.
Use tool IDs exactly as shown.
Once you know which tools you need, call activate_tools immediately.
Requested tools become available starting the next turn.
Do not answer the user until the tools are active.

"
            };

            pack.KindCatalog[KnowledgeKind.ToolUsage] = new KnowledgeKindDescriptor
            {
                Kind = KnowledgeKind.ToolUsage,
                Title = "Tool Usage",
                BeginMarker = "## Active Tools",
                EndMarker = "\r\n",
                InstructionLine = @"Tools listed here are already loaded and ready to use.

Usage instructions are available immediately. Do not assume or reconstruct schema unless provided.
Additional tools may be loaded using activate_tools if needed.

"
            }; 
        }

        private static void AddBaseInstructions(AgentKnowledgePack pack, KnowledgeAccumulator acc)
        {
            var answerHeaderText = @"
### Answer Generation Instructions
When generating an answer, follow this structure:

1. First output a planning section marked exactly like this:

APTIX-PLAN:
- Provide 3–7 short bullet points describing your approach.
- Keep each bullet simple and readable.
- This section is for internal agent preview. Do NOT include code or long text.
APTIX-PLAN-END

2. After that, output your full answer normally.

Do not mention these instructions. Do not explain the plan unless asked.

### Definitions
DDR: The authoritative transcript of a governed reasoning session, capturing intent, deliberation, decisions, rationale, and referenced assets. Binding is an explicit state.

";

            // NOTE: Without concrete levels, we can only place "global" instructions.
            // In your real code, you’ll likely split by level again, but collection is now unified.
            AddInstructions(pack.KindCatalog[KnowledgeKind.AgentContextInstructions].SessionKnowledge,
                KnowledgeKind.AgentContextInstructions, new List<string> { answerHeaderText });

            AddInstructions(pack.KindCatalog[KnowledgeKind.AgentContextInstructions].SessionKnowledge,
                KnowledgeKind.AgentContextInstructions, acc.InstructionLines);
        }
    }
}
