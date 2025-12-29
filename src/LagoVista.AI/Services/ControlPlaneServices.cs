using LagoVista.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Services
{
    public sealed class ResolverMatcher
    {
        public IReadOnlyList<MatchResult> Match(string input, IEnumerable<ResolverItem> items)
        {
            var normalized = Normalize(input);

            return items
                .Select(item => new MatchResult(item, Score(normalized, item)))
                .Where(r => r.Confidence > 0)
                .OrderByDescending(r => r.Confidence)
                .ToList();
        }

        private static double Score(string input, ResolverItem item)
        {
            if (string.Equals(input, Normalize(item.Id)))
                return 1.0;

            if (item.Aliases.Any(a => Normalize(a) == input))
                return 0.95;

            if (Normalize(item.DisplayName).Contains(input))
                return 0.8;

            if (item.Keywords.Any(k => Normalize(k).Contains(input)))
                return 0.6;

            return 0;
        }

        private static string Normalize(string value) =>
            value?.Trim().ToLowerInvariant();
    }

    public sealed class EnumSelectionRouter
    {
        private readonly ResolverMatcher _matcher;

        public EnumSelectionRouter(ResolverMatcher matcher)
        {
            _matcher = matcher;
        }

        public RouterAction Route(
            string rawInput,
            ClientCapabilities caps,
            IResolverCatalog catalog,
            string pickerType,
            string executeActionId)
        {
            var matches = _matcher.Match(rawInput, catalog.Items);

            var best = matches.FirstOrDefault();

            if (caps.SupportsPickers)
            {
                return new OpenPickerAction(
                    pickerType,
                    rawInput,
                    best?.Confidence >= 0.95 ? best.Item.Id : null
                );
            }

            if (best == null)
            {
                return new AskClarifyingQuestionAction(
                    "I couldn't find a matching option. Which one did you mean?",
                    catalog.Items.Select(i => i.DisplayName).Take(5).ToList()
                );
            }

            if (best.Confidence >= 0.95)
            {
                return new ExecuteAction(
                    executeActionId,
                    new Dictionary<string, string> { { "id", best.Item.Id } }
                );
            }

            return new AskClarifyingQuestionAction(
                "Which one did you want?",
                matches.Take(3).Select(m => m.Item.DisplayName).ToList()
            );
        }
    }

}
