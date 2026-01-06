using LagoVista.AI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LagoVista.AI.ACP
{
    public sealed class AcpCommandRouter : IAcpCommandRouter
    {
        private readonly IAcpCommandRegistry _registry;

        public AcpCommandRouter(IAcpCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public AcpExecutionRoute Route(string inputText)
        {
            inputText = (inputText ?? String.Empty).Trim();
            if (inputText.Length == 0)
                return new AcpExecutionRoute { Outcome = AcpRouteOutcome.NoMatch };

            // Pull descriptors from registry (catalog)
            var descriptors = _registry.GetAllCommands()
                                      .Select(s => _registry.GetDescriptor(s.Id))
                                      .Where(d => d != null)
                                      .ToList();

            var matches = new List<(AcpCommandDescriptor desc, string trigger, string[] args)>();

            foreach (var cmd in descriptors)
            {
                if (cmd.Triggers == null || cmd.Triggers.Length == 0)
                    continue;

                foreach (var trigger in cmd.Triggers)
                {
                    if (String.IsNullOrWhiteSpace(trigger))
                        continue;

                    if (!StartsWithTrigger(inputText, trigger, cmd.TriggerCaseInsensitive, out var remainder))
                        continue;

                    var parsed = TryParseArgs(remainder);
                    if (!parsed.Success)
                    {
                        // Unbalanced quotes => treat as no-match and fall through to LLM
                        continue;
                    }

                    var args = parsed.Args;

                    // Arg count gating
                    if (args.Length < cmd.MinArgs || args.Length > cmd.MaxArgs)
                        continue;

                    // Regex gating
                    if (cmd.ArgRegexRules != null && cmd.ArgRegexRules.Count > 0)
                    {
                        if (!ArgsPassRegexRules(args, cmd.ArgRegexRules))
                            continue;
                    }

                    matches.Add((cmd, trigger, args));
                    break; // prevent duplicate matches for same command
                }
            }

            if (matches.Count == 0)
                return new AcpExecutionRoute { Outcome = AcpRouteOutcome.NoMatch };

            // Sort deterministically: priority desc, commandId asc, trigger length desc
            matches = matches
                .OrderByDescending(m => m.desc.Priority)
                .ThenBy(m => m.desc.CommandId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(m => (m.trigger ?? String.Empty).Length)
                .ThenBy(m => m.trigger ?? String.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 1)
            {
                return new AcpExecutionRoute
                {
                    Outcome = AcpRouteOutcome.SingleMatch,
                    CommandId = matches[0].desc.CommandId,
                    Args = matches[0].args
                };
            }

            return new AcpExecutionRoute
            {
                Outcome = AcpRouteOutcome.MultipleMatch,
                CandidateCommandIds = matches.Select(m => m.desc.CommandId).ToArray()
            };
        }

        private static bool StartsWithTrigger(string input, string trigger, bool caseInsensitive, out string remainder)
        {
            remainder = null;
            trigger = trigger.Trim();

            var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            if (!input.StartsWith(trigger, comparison))
                return false;

            if (input.Length > trigger.Length)
            {
                var next = input[trigger.Length];
                if (!(Char.IsWhiteSpace(next) || Char.IsPunctuation(next)))
                    return false;
            }

            remainder = input.Length == trigger.Length
                ? String.Empty
                : input.Substring(trigger.Length).Trim();

            return true;
        }

        private sealed class ArgParseResult
        {
            public bool Success { get; set; }
            public string[] Args { get; set; } = Array.Empty<string>();
        }

        private static ArgParseResult TryParseArgs(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return new ArgParseResult { Success = true, Args = Array.Empty<string>() };

            var args = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == '\\' && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    if (next == '"' || next == '\\')
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && Char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        args.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                args.Add(sb.ToString());

            // Unbalanced quotes => parsing failed => no ACP match
            if (inQuotes)
                return new ArgParseResult { Success = false, Args = Array.Empty<string>() };

            return new ArgParseResult { Success = true, Args = args.ToArray() };
        }

        private static bool ArgsPassRegexRules(string[] args, List<AcpArgRegexRule> rules)
        {
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                if (rule.Index < 0) continue;

                if (rule.Index >= args.Length)
                    return false;

                var options = rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

                // Consider adding a timeout in production.
                var rx = new Regex(rule.Pattern, options);

                if (!rx.IsMatch(args[rule.Index] ?? String.Empty))
                    return false;
            }

            return true;
        }
    }
}