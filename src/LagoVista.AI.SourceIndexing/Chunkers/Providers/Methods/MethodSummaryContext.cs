using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Lightweight context object for building human-readable summaries of methods
    /// (primarily Managers/Repositories/Services/Controllers) that operate on a
    /// particular model within a domain.
    ///
    /// This is intentionally simple and is meant as a placeholder. We can evolve
    /// the shape and the wording strategy as we learn more about real-world usage.
    /// </summary>
    public sealed class MethodSummaryContext
    {
        /// <summary>
        /// Logical domain name, e.g. "Customer Management", "Device Management".
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// Short human tagline for the domain, e.g. "manages customer accounts and billing".
        /// </summary>
        public string DomainTagline { get; set; }

        /// <summary>
        /// Model name the method primarily operates on, e.g. "Customer".
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Short human tagline for the model, e.g. "represents a customer in the system".
        /// </summary>
        public string ModelTagline { get; set; }

        /// <summary>
        /// SubKind / role of the containing type, e.g. "Manager", "Repository", "Service", "Controller".
        /// </summary>
        public string SubKind { get; set; }

        /// <summary>
        /// Method name, e.g. "AddCustomerAsync".
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Optional signature sketch, e.g.
        /// "AddCustomerAsync(Customer customer, EntityHeader org, EntityHeader user)".
        /// </summary>
        public string Signature { get; set; }
    }
}
