using LagoVista.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public enum CompositionTraceOutcome
    {
        Ok,
        Failed,
        Aborted
    }

    public sealed class CompositionTrace
    {
        /// <summary>
        /// Stable identifier for this pipeline execution.
        /// Useful when persisted separately from the session.
        /// </summary>
        public string TraceId { get; set; } = Guid.NewGuid().ToId();

        /// <summary>
        /// UTC timestamp when the pipeline started.
        /// </summary>
        public string StartedUtc { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>
        /// UTC timestamp when the pipeline completed (success or failure).
        /// </summary>
        public string CompletedUtc { get; set; }

        /// <summary>
        /// Ordered list of step executions.
        /// </summary>
        public List<CompositionTraceStep> Steps { get; set; } = new List<CompositionTraceStep>();

        /// <summary>
        /// Optional summary written at the end of the pipeline.
        /// </summary>
        public string Summary { get; set; }
    }

    public sealed class CompositionTraceStep
    {
        /// <summary>
        /// Stable identifier for the pipeline step (e.g. "A3.ModeComposition").
        /// </summary>
        public string StepKey { get; set; }

        /// <summary>
        /// Human-readable name for diagnostics.
        /// </summary>
        public string StepName { get; set; }

        /// <summary>
        /// UTC timestamp when this step started.
        /// </summary>
        public string StartedUtc { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>
        /// UTC timestamp when this step completed.
        /// </summary>
        public string CompletedUtc { get; set; }

        /// <summary>
        /// Duration of the step in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Outcome of the step.
        /// </summary>
        public CompositionTraceOutcome Outcome { get; set; }

        /// <summary>
        /// Short free-form notes (keep small).
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Optional lightweight metrics (counts only).
        /// Never store large payloads here.
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
    }

    public static class CompositionTraceExtensions
    {
        public static CompositionTraceStep StartStep(
            this CompositionTrace trace,
            string stepKey,
            string stepName)
        {
            var step = new CompositionTraceStep
            {
                StepKey = stepKey,
                StepName = stepName
            };

            trace.Steps.Add(step);
            return step;
        }

        public static void Complete(
            this CompositionTraceStep step,
            CompositionTraceOutcome outcome,
            string notes = null,
            Dictionary<string, object> metrics = null)
        {
            step.CompletedUtc = DateTime.UtcNow.ToString("o");

            if (DateTime.TryParse(step.StartedUtc, out var started) &&
                DateTime.TryParse(step.CompletedUtc, out var completed))
            {
                step.ElapsedMs = (completed - started).TotalMilliseconds;
            }

            step.Outcome = outcome;
            step.Notes = notes;

            if (metrics != null)
            {
                foreach (var kvp in metrics)
                {
                    step.Metrics[kvp.Key] = kvp.Value;
                }
            }
        }
    }

}
