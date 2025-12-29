using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public sealed class ClientCapabilities
    {
        public bool SupportsCommandPalette { get; set; }
        public bool SupportsPickers { get; set; }
        public bool SupportsForms { get; set; }
    }

    public abstract class RouterAction { }

    public sealed class ContinueWithLlm : RouterAction { }

    public sealed class OpenPickerAction : RouterAction
    {
        public string PickerType { get; }
        public string Prefill { get; }
        public string HighlightId { get; }

        public OpenPickerAction(string pickerType, string prefill, string highlightId = null)
        {
            PickerType = pickerType;
            Prefill = prefill;
            HighlightId = highlightId;
        }
    }

    public sealed class ExecuteAction : RouterAction
    {
        public string ActionId { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public ExecuteAction(string actionId, IReadOnlyDictionary<string, string> parameters)
        {
            ActionId = actionId;
            Parameters = parameters;
        }
    }

    public sealed class AskClarifyingQuestionAction : RouterAction
    {
        public string Question { get; }
        public IReadOnlyList<string> Choices { get; }

        public AskClarifyingQuestionAction(string question, IReadOnlyList<string> choices)
        {
            Question = question;
            Choices = choices;
        }
    }

    public sealed class ResolverItem
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();

        // Optional usage data
        public int UsageCount { get; set; }
        public DateTimeOffset? LastUsed { get; set; }
    }


    public sealed class MatchResult
    {
        public ResolverItem Item { get; }
        public double Confidence { get; }

        public MatchResult(ResolverItem item, double confidence)
        {
            Item = item;
            Confidence = confidence;
        }
    }

    public interface IResolverCatalog
    {
        IReadOnlyList<ResolverItem> Items { get; }
    }


}
