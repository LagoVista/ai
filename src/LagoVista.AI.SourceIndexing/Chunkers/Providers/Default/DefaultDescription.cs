using LagoVista.AI.Chunkers.Providers.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Chunkers.Providers.Default
{
    public partial class DefaultDescription : IDescriptionProvider
    {
        public string Namespace { get; set; }
        public string FullName { get; set; }
        public string SymbolName { get; set; }
        public string SourceName { get; set; }
        public string SymbolText { get; set; }
        public string SourcePath { get; set; }
        public string PrimaryEntity { get; set; }

        public string BaseClass { get; set; }


        /// <summary>
        /// Full names of base interfaces this interface extends.
        /// </summary>
        public IReadOnlyList<string> BaseInterfaces { get; set; }


        /// <summary>
        /// Methods declared on the interface.
        /// </summary>
        public IReadOnlyList<MethodDescription> Methods { get; set; }

        public IReadOnlyList<PropertyDescription> Properties { get; set; }

    }

    /// <summary>
    /// Reason of a single interface method.
    /// </summary>
    public class MethodDescription
    {
        /// <summary>
        /// Method name, e.g. CreateDeviceAsync.
        /// </summary>
        public string Name { get; set; }

        public string SemanticSummary { get; set; }

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
        public IReadOnlyList<MethodParameterDescription> Parameters { get; set; }

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
    /// Reason of a method parameter on an interface.
    /// </summary>
    public class MethodParameterDescription
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

    public class PropertyDescription
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }

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
}
