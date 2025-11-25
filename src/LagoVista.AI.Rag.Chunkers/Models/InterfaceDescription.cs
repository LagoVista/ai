using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Pure semantic description of a C# interface (IDX-0042 InterfaceOverview).
    ///
    /// This is contract-focused metadata only; no chunking/indexing concerns.
    /// </summary>
    public partial class InterfaceDescription : SummaryFacts
    {
        /// <summary>
        /// Simple interface name, e.g. IDeviceManager.
        /// </summary>
        public string InterfaceName { get; set; }


        /// <summary>
        /// Fully qualified name, e.g. LagoVista.AI.Managers.IDeviceManager.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// True if the interface is generic (IRepository&lt;T&gt; etc.).
        /// </summary>
        public bool IsGeneric { get; set; }

        /// <summary>
        /// Number of generic type parameters.
        /// </summary>
        public int GenericArity { get; set; }

        /// <summary>
        /// Full names of base interfaces this interface extends.
        /// </summary>
        public IReadOnlyList<string> BaseInterfaces { get; set; }

        /// <summary>
        /// Coarse role classification for the contract: ManagerContract, RepositoryContract, ServiceContract, OtherContract.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Methods declared on the interface.
        /// </summary>
        public IReadOnlyList<InterfaceMethodDescription> Methods { get; set; }

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

        public override string Subtype => "Interface";
    }

    /// <summary>
    /// Description of a single interface method.
    /// </summary>
    public class InterfaceMethodDescription
    {
        /// <summary>
        /// Method name, e.g. CreateDeviceAsync.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Raw C# return type string, e.g. Task&lt;InvokeResult&lt;Device&gt;&gt;.
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// True if return type is Task or Task&lt;T&gt;.
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// Method parameters.
        /// </summary>
        public IReadOnlyList<InterfaceMethodParameterDescription> Parameters { get; set; }

        /// <summary>
        /// XML summary text for the method, when present.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// 1-based line where the method starts (inclusive).
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// 1-based line where the method ends (inclusive).
        /// </summary>
        public int? LineEnd { get; set; }
    }

    /// <summary>
    /// Description of a method parameter on an interface.
    /// </summary>
    public class InterfaceMethodParameterDescription
    {
        /// <summary>
        /// Parameter name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Parameter type name, e.g. Device, string, EntityHeader.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// True if the parameter has a default value and is therefore optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// String representation of the default value when present.
        /// </summary>
        public string DefaultValue { get; set; }
    }
}
