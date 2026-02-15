using LagoVista.AI.Chunkers.Providers;
using LagoVista.AI.Chunkers.Providers.Default;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Chunkers.Providers.Interfaces
{
    /// <summary>
    /// Pure semantic description of a C# interface (IDX-0042 InterfaceOverview).
    ///
    /// This is contract-focused metadata only; no chunking/indexing concerns.
    /// </summary>
    public partial class InterfaceDescription : DefaultDescription, IDescriptionProvider
    {

        /// <summary>
        /// Simple interface name, e.g. IDeviceManager.
        /// </summary>
        public string InterfaceName { get; set; }


        public string OverviewSummary { get; set; }

        public IReadOnlyList<string> Responsibilities { get; set; }

        public IReadOnlyList<string> UsageNotes { get; set; }

        public string SemanticSummary { get; set; }
        public string LinkageSummary { get; set; }


        /// <summary>
        /// True if the interface is generic (IRepository&lt;T&gt; etc.).
        /// </summary>
        public bool IsGeneric { get; set; }

        /// <summary>
        /// Number of generic type parameters.
        /// </summary>
        public int GenericArity { get; set; }


        /// <summary>
        /// Coarse role classification for the contract: ManagerContract, RepositoryContract, ServiceContract, OtherContract.
        /// </summary>
        public string Role { get; set; }


        public List<string> OperationKinds { get; set; } = new List<string>(); // "query" | "command"
        public List<string> CrudVerbs { get; set; } = new List<string>();      // "create"|"read"|"update"|"delete"|"list"|...
        public List<string> Flags { get; set; } = new List<string>();          // "with_secrets","for_org","by_id","count",...
        public string FinderSnippet { get; set; }                 // embed text


        public List<string> ReturnTypes { get; set; } = new List<string>();

        public List<string> OperatesOnTypes { get; set; } = new List<string>();

        /// <summary>
        /// Full type names of implementing classes, when known.
        /// (May be populated by a later pass; builder leaves empty by default.)
        /// </summary>
        public IReadOnlyList<string> ImplementedBy { get; set; }

        /// <summary>
        /// Endpoint keys of controller actions that depend on this interface via DI,
        /// e.g. DeviceController.GetDevice. Typically populated by a later pass.
        /// </summary>
        public IReadOnlyList<string> UsedByControllers { get; set; }

        /// <summary>
        /// 1-based line where the interface starts (inclusive).
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// 1-based line where the interface ends (inclusive).
        /// </summary>
        public int? LineEnd { get; set; }
    }



}
