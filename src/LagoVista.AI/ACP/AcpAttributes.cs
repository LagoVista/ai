using System;

namespace LagoVista.AI.ACP
{
    public enum AcpCommandPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Declares a class as an ACP Command.
    /// NOTE: Per v1 requirements, CommandId, DisplayName, and Description are required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AcpCommandAttribute : Attribute
    {
        public AcpCommandAttribute(string commandId, string displayName, string description)
        {
            if (String.IsNullOrWhiteSpace(commandId)) throw new ArgumentNullException(nameof(commandId));
            if (String.IsNullOrWhiteSpace(displayName)) throw new ArgumentNullException(nameof(displayName));
            if (String.IsNullOrWhiteSpace(description)) throw new ArgumentNullException(nameof(description));

            CommandId = commandId;
            DisplayName = displayName;
            Description = description;
        }

        public string CommandId { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Declares one or more trigger phrases for a command.
    /// Router recommendation: match is case-insensitive and must occur at the start of the input
    /// on a token boundary (e.g., "mode ddr" matches "mode", but "model" does not).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AcpTriggersAttribute : Attribute
    {
        public AcpTriggersAttribute(params string[] triggers)
        {
            if (triggers == null) throw new ArgumentNullException(nameof(triggers));
            if (triggers.Length == 0) throw new ArgumentException("At least one trigger is required.", nameof(triggers));

            for (var i = 0; i < triggers.Length; i++)
            {
                if (String.IsNullOrWhiteSpace(triggers[i]))
                    throw new ArgumentException("Triggers cannot contain null/empty values.", nameof(triggers));
            }

            Triggers = triggers;
        }

        public string[] Triggers { get; }

        /// <summary>
        /// Defaults to true; router should treat triggers as case-insensitive.
        /// </summary>
        public bool CaseInsensitive { get; set; } = true;
    }

    /// <summary>
    /// Declares positional argument count constraints for a command.
    /// Router should use this as a gating signal: if args are out of bounds, treat as no-match.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AcpArgsAttribute : Attribute
    {
        public AcpArgsAttribute(int min, int max)
        {
            if (min < 0) throw new ArgumentOutOfRangeException(nameof(min));
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max < min) throw new ArgumentException("max must be >= min.");

            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }
    }

    /// <summary>
    /// Optional safety metadata. If RequiresConfirmation is true, the router/pipeline can enforce
    /// an explicit confirmation step before executing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AcpSafetyAttribute : Attribute
    {
        public AcpSafetyAttribute(bool requiresConfirmation, bool producesSideEffects)
        {
            RequiresConfirmation = requiresConfirmation;
            ProducesSideEffects = producesSideEffects;
        }

        public bool RequiresConfirmation { get; }
        public bool ProducesSideEffects { get; }
    }

    /// <summary>
    /// Optional: regex validation for a specific positional argument.
    /// Intended as a gating signal to reduce false positives.
    /// Router should apply after parsing args; if it fails, treat as no-match.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AcpArgRegexAttribute : Attribute
    {
        public AcpArgRegexAttribute(int index, string pattern)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (String.IsNullOrWhiteSpace(pattern)) throw new ArgumentNullException(nameof(pattern));

            Index = index;
            Pattern = pattern;
        }

        public int Index { get; }
        public string Pattern { get; }

        /// <summary>
        /// Defaults to true for convenience; set false if you need case-sensitive validation.
        /// </summary>
        public bool IgnoreCase { get; set; } = true;
    }
}