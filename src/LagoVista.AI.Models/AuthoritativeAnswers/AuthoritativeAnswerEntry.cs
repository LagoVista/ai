using LagoVista.Core.Models;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models.AuthoritativeAnswers
{
    /// <summary>
    /// Authoritative Answer (AQ) entry. This is intended to capture settled clarifications
    /// that can be reused by both humans and the LLM.
    /// 
    /// NOTE: Architectural invariants and long-term commitments remain DDRs.
    /// </summary>
    public class AuthoritativeAnswerEntry : EntityBase
    {
        /// <summary>
        /// Globally unique identifier for this AQ entry.
        /// </summary>
        public string AqId { get; set; }

        /// <summary>
        /// Organization identifier. In v1 this is also used as the Table Storage PartitionKey.
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        /// Canonical, normalized question used for lookup.
        /// </summary>
        public string NormalizedQuestion { get; set; }

        /// <summary>
        /// Human-friendly question (optional). If not provided, NormalizedQuestion may be displayed.
        /// </summary>
        public string HumanQuestion { get; set; }

        /// <summary>
        /// LLM-optimized question (optional). If not provided, NormalizedQuestion may be used.
        /// </summary>
        public string LlmQuestion { get; set; }

        /// <summary>
        /// Human-friendly answer (optional).
        /// </summary>
        public string HumanAnswer { get; set; }

        /// <summary>
        /// LLM-optimized answer (optional). Keep this concise and directly actionable.
        /// </summary>
        public string LlmAnswer { get; set; }

        /// <summary>
        /// Optional tags for retrieval boosting (symbols, types, properties, etc.).
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Optional source reference (e.g., ddr:SYS-000123, faq:XYZ, human).
        /// </summary>
        public string SourceRef { get; set; }

        /// <summary>
        /// Optional scope hint (even though org is the primary partitioning mechanism).
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Confidence hint. V1 is string to avoid enum churn.
        /// Expected values: high | medium | low
        /// </summary>
        public string Confidence { get; set; }

        /// <summary>
        /// ISO-8601 timestamp (UTC) for creation.
        /// </summary>
        public string CreatedUtc { get; set; }

        /// <summary>
        /// ISO-8601 timestamp (UTC) for last update.
        /// </summary>
        public string UpdatedUtc { get; set; }
    }
}
