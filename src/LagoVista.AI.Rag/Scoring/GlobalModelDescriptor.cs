using System;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Descriptor for a first-class domain model used as a semantic anchor
    /// during scoring. Typically sourced from EntityDescription-attributed types.
    /// </summary>
    public sealed class GlobalModelDescriptor
    {
        /// <summary>
        /// Canonical model name, e.g. "Device", "Alert", "CustomerAccount".
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional business domain or bounded context, e.g. "IoT" or "Billing".
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Optional human-readable description for potential future rewrite
        /// or guidance scenarios.
        /// </summary>
        public string Description { get; set; }
    }
}
