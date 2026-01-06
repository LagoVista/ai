using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System;
using System.Text;

namespace LagoVista.AI.Models
{

    public partial class AgentPersonaDefinition : EntityBase, IValidateable, IFormDescriptor, IFormDescriptorCol2, ISummaryFactory
    {
   

        /// <summary>
        /// Builds a small, model-friendly preference block that translates enum selections
        /// into short behavioral cues. This should be injected at session/chapter start (or on persona change).
        /// 
        /// Notes:
        /// - Non-normative on purpose: this describes preferences and interaction style.
        /// - Does not override or replace schemas, workflow rules, or tool contracts.
        /// - Uses EntityHeader.Text for the human-facing option text.
        /// - Optionally appends AdditionalConfiguration verbatim at the end.
        /// </summary>
        /// <param name="includeAdditionalConfiguration">If true, appends AdditionalConfiguration if present.</param>
        /// <param name="maxAdditionalConfigurationChars">If set, trims AdditionalConfiguration to this many characters.</param>
        public string BuildPersonaGuidance(bool includeAdditionalConfiguration = true, int? maxAdditionalConfigurationChars = 600)
        {
            var sb = new StringBuilder();

            AppendPreference(sb, "Name", DisplayName);
            AppendPreference(sb, "Tone", ToneStyle?.Text, ToneStyle?.Value.ToString());
            AppendPreference(sb, "Verbosity", VerbosityLevel?.Text, VerbosityLevel?.Value.ToString());
            AppendPreference(sb, "Detail focus", DetailFocus?.Text, DetailFocus?.Value.ToString());
            AppendPreference(sb, "Assumption tolerance", AssumptionTolerance?.Text, AssumptionTolerance?.Value.ToString());
            AppendPreference(sb, "Challenge level", ChallengeLevel?.Text, ChallengeLevel?.Value.ToString());
            AppendPreference(sb, "Risk sensitivity", RiskSensitivity?.Text, RiskSensitivity?.Value.ToString());
            AppendPreference(sb, "Creativity", CreativityLevel?.Text, CreativityLevel?.Value.ToString());
            AppendPreference(sb, "Reflection", ReflectionLevel?.Text, ReflectionLevel?.Value.ToString());
            AppendPreference(sb, "Suggestions", SuggestionStyle?.Text, SuggestionStyle?.Value.ToString());
            AppendPreference(sb, "Confirmations", ConfirmationStrictness?.Text, ConfirmationStrictness?.Value.ToString());
            AppendPreference(sb, "Humor", HumorLevel?.Text, HumorLevel?.Value.ToString());

            sb.AppendLine();
            sb.AppendLine("Interpretation hints (concise):");
            AppendInterpretationHints(sb);

            sb.AppendLine();
            sb.AppendLine("Guardrails:");
            sb.AppendLine("- Persona preferences are non-normative. These are style preferences only; correctness, schemas, workflow rules, and tool contracts remain unchanged.");

            if (includeAdditionalConfiguration && !String.IsNullOrWhiteSpace(AdditionalConfiguration))
            {
                var extra = AdditionalConfiguration.Trim();
                if (maxAdditionalConfigurationChars.HasValue && extra.Length > maxAdditionalConfigurationChars.Value)
                {
                    extra = extra.Substring(0, maxAdditionalConfigurationChars.Value).TrimEnd() + "…";
                }

                sb.AppendLine();
                sb.AppendLine("Additional configuration:");
                sb.AppendLine(extra);
            }

            return sb.ToString().Trim();
        }

        private static void AppendPreference(StringBuilder sb, string label, string valueText, string fallback = null)
        {
            var value = !String.IsNullOrWhiteSpace(valueText) ? valueText : fallback;
            if (String.IsNullOrWhiteSpace(value))
            {
                return;
            }

            sb.Append("- ");
            sb.Append(label);
            sb.Append(": ");
            sb.AppendLine(value);
        }

        private void AppendInterpretationHints(StringBuilder sb)
        {
            // Keep these short. The goal is to turn your "knobs" into consistent behavior.
            // These lines are intentionally descriptive (non-normative).

            switch (AssumptionTolerance?.Value)
            {
                case Models.AssumptionTolerance.Low:
                    sb.AppendLine("- Assumptions: prefer asking before filling missing details; list unknowns briefly.");
                    break;
                case Models.AssumptionTolerance.High:
                    sb.AppendLine("- Assumptions: proceed with reasonable defaults; call out assumptions explicitly.");
                    break;
                default:
                    sb.AppendLine("- Assumptions: use reasonable defaults; ask when blocked or ambiguous.");
                    break;
            }

            switch (DetailFocus?.Value)
            {
                case Models.DetailFocus.Outcome:
                    sb.AppendLine("- Detail: emphasize outcomes and decisions; minimal step-by-step.");
                    break;
                case Models.DetailFocus.Process:
                    sb.AppendLine("- Detail: include steps/rationale where helpful; explain tradeoffs.");
                    break;
                default:
                    sb.AppendLine("- Detail: balanced outcomes plus brief rationale.");
                    break;
            }

            switch (ChallengeLevel?.Value)
            {
                case Models.ChallengeLevel.None:
                    sb.AppendLine("- Pushback: accept intent; clarify only when necessary.");
                    break;
                case Models.ChallengeLevel.Light:
                    sb.AppendLine("- Pushback: surface obvious risks/ambiguities as questions.");
                    break;
                case Models.ChallengeLevel.High:
                    sb.AppendLine("- Pushback: actively stress-test ideas; highlight failure modes and alternatives.");
                    break;
                case Models.ChallengeLevel.Adversarial:
                    sb.AppendLine("- Pushback: treat proposals as hypotheses; attempt to falsify weak assumptions.");
                    break;
                default:
                    sb.AppendLine("- Pushback: challenge assumptions politely; offer counterpoints and tradeoffs.");
                    break;
            }

            switch (RiskSensitivity?.Value)
            {
                case Models.RiskSensitivity.Low:
                    sb.AppendLine("- Risk: favor speed and progress; fewer warnings.");
                    break;
                case Models.RiskSensitivity.High:
                    sb.AppendLine("- Risk: prefer safe and reversible choices; highlight risk hotspots.");
                    break;
                default:
                    sb.AppendLine("- Risk: balance speed with safety; flag material risks.");
                    break;
            }

            switch (CreativityLevel?.Value)
            {
                case Models.CreativityLevel.Minimal:
                    sb.AppendLine("- Creativity: stick closely to provided patterns; avoid novel approaches.");
                    break;
                case Models.CreativityLevel.Constrained:
                    sb.AppendLine("- Creativity: offer small variations within known patterns.");
                    break;
                case Models.CreativityLevel.Expansive:
                    sb.AppendLine("- Creativity: explore novel options and reframes; keep grounded in constraints.");
                    break;
                default:
                    sb.AppendLine("- Creativity: explore a few alternatives while staying grounded.");
                    break;
            }

            switch (VerbosityLevel?.Value)
            {
                case Models.VerbosityLevel.UltraConcise:
                    sb.AppendLine("- Length: very short responses; focus on essentials.");
                    break;
                case Models.VerbosityLevel.Thorough:
                    sb.AppendLine("- Length: more complete explanations; include relevant context.");
                    break;
                default:
                    sb.AppendLine("- Length: concise with enough context to avoid round-trips.");
                    break;
            }

            switch (ReflectionLevel?.Value)
            {
                case Models.ReflectionLevel.None:
                    sb.AppendLine("- Reflection: minimal restatement; move quickly to answers.");
                    break;
                case Models.ReflectionLevel.Normal:
                    sb.AppendLine("- Reflection: briefly restate intent before committing decisions.");
                    break;
                default:
                    sb.AppendLine("- Reflection: light confirmation of intent where it improves clarity.");
                    break;
            }

            switch (SuggestionStyle?.Value)
            {
                case Models.SuggestionStyle.ReactiveOnly:
                    sb.AppendLine("- Suggestions: provide options mainly when asked or when ambiguity is present.");
                    break;
                case Models.SuggestionStyle.Proactive:
                    sb.AppendLine("- Suggestions: proactively offer 1–2 options when helpful.");
                    break;
                default:
                    sb.AppendLine("- Suggestions: offer 1–2 plausible options when helpful.");
                    break;
            }

            switch (ConfirmationStrictness?.Value)
            {
                case Models.ConfirmationStrictness.Low:
                    sb.AppendLine("- Confirmations: avoid extra check-ins; assume forward motion is desired.");
                    break;
                case Models.ConfirmationStrictness.High:
                    sb.AppendLine("- Confirmations: pause on key decisions and confirm intent explicitly.");
                    break;
                default:
                    sb.AppendLine("- Confirmations: confirm only on key forks or irreversible steps.");
                    break;
            }

            switch (HumorLevel?.Value)
            {
                case Models.HumorLevel.Light:
                    sb.AppendLine("- Humor: light and occasional; avoid jokes during serious or high-stakes sections.");
                    break;
                default:
                    sb.AppendLine("- Humor: off; keep tone straightforward.");
                    break;
            }

            switch (ToneStyle?.Value)
            {
                case Models.ToneStyle.Warm:
                    sb.AppendLine("- Tone: friendly and encouraging; keep it professional.");
                    break;
                case Models.ToneStyle.Direct:
                    sb.AppendLine("- Tone: blunt and efficient; minimal social glue.");
                    break;
                case Models.ToneStyle.Conversational:
                    sb.AppendLine("- Tone: human and collaborative; short reflections and natural phrasing.");
                    break;
                default:
                    sb.AppendLine("- Tone: neutral and professional.");
                    break;
            }
        }

 
    } 
}
