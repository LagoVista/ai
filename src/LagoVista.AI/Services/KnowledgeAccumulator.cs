using LagoVista.AI.Models;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Services
{
    internal sealed class KnowledgeAccumulator
    {
        public readonly List<string> InstructionLines = new List<string>();
        public readonly List<EntityHeader> InstructionDdrs = new List<EntityHeader>();
        public readonly List<EntityHeader> ReferenceDdrs = new List<EntityHeader>();
        public readonly List<EntityHeader> ActiveTools = new List<EntityHeader> ();
        public readonly List<EntityHeader> AvailableTools = new List<EntityHeader>();

        private readonly HashSet<string> _instructionLines = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _instructionDdrIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _referenceDdrIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _availableToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void AddDirect(IAgentKnowledgeProvider p)
        {
            AddInstructions(p?.Instructions);
            AddEntityHeaders(p?.InstructionDdrs, InstructionDdrs, _instructionDdrIds);
            AddEntityHeaders(p?.ReferenceDdrs, ReferenceDdrs, _referenceDdrIds);
            AddEntityHeaders(p?.ActiveTools, ActiveTools, _activeToolIds);
            AddEntityHeaders(p?.AvailableTools, AvailableTools, _availableToolIds);
        }

        private void AddInstructions(IEnumerable<string> values)
        {
            if (values == null) return;
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (_instructionLines.Add(v)) InstructionLines.Add(v);
            }
        }

        private static void AddEntityHeaders(IEnumerable<EntityHeader> values, List<EntityHeader> target, HashSet<string> seen)
        {
            if (values == null) return;

            foreach (var eh in values)
            {
                if (eh == null) continue;
                if (string.IsNullOrWhiteSpace(eh.Id)) continue;

                if (seen.Add(eh.Id))
                {
                    target.Add(eh);
                }
            }
        }
    }

}
