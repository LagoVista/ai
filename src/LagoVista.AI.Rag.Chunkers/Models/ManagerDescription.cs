using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0039: Semantic description of a Manager class.
    ///
    /// This is a pure description model – it captures the intent, shape,
    /// relationships, and behaviors of a Manager without encoding any
    /// chunking-specific concepts (no PartIndex, no ContentHash, etc.).
    ///
    /// The chunking pipeline is expected to consume ManagerDescription and
    /// then project it into NormalizedChunk instances as a separate step.
    /// </summary>
    public sealed class ManagerDescription
    {
        /// <summary>
        /// Logical document identifier (IDX-001) for the source file.
        /// </summary>
        public string DocId { get; set; }

        /// <summary>
        /// Source file path or name used to locate the Manager in the repo.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Namespace that contains the Manager class.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Simple class name of the Manager (e.g., DeviceManager).
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// High-level Manager type classification. Currently mirrors the
        /// IDX-0039 flavors and can be used to distinguish between
        /// different Manager shapes if we introduce more patterns later.
        /// </summary>
        public ManagerType ManagerType { get; set; } = ManagerType.ManagerOverview;

        /// <summary>
        /// XML-doc style summary for the Manager class (if available).
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Primary entity name that this Manager orchestrates, e.g. "Device".
        /// May be null if heuristics cannot determine a clear entity.
        /// </summary>
        public string PrimaryEntity { get; set; }

        /// <summary>
        /// Simple names of all interfaces implemented by the Manager class,
        /// e.g. ["IDeviceManager", "IDisposable"].
        /// </summary>
        public IReadOnlyList<string> ImplementedInterfaces { get; set; }

        /// <summary>
        /// Primary DI/contract interface for this Manager, as determined by
        /// IDX-0039 heuristics (IFooManager, IDeviceManager, single *Manager
        /// interface, etc.).
        /// </summary>
        public string PrimaryInterface { get; set; }

        /// <summary>
        /// Interfaces that the Manager depends on via constructor injection.
        /// Typically these come from constructor parameters whose types are
        /// interfaces (e.g., IDeviceRepository, IAuditLogService).
        ///
        /// This is a flattened view across all constructors; use
        /// <see cref="Constructors"/> for per-ctor detail.
        /// </summary>
        public IReadOnlyList<string> DependencyInterfaces { get; set; }

        /// <summary>
        /// Constructors declared on the Manager class, including injected
        /// dependencies and source locations.
        /// </summary>
        public IReadOnlyList<ManagerConstructorDescription> Constructors { get; set; }

        /// <summary>
        /// Logical methods exposed by the Manager, including their
        /// signatures, summaries, and body text.
        /// </summary>
        public IReadOnlyList<ManagerMethodDescription> Methods { get; set; }
    }

    /// <summary>
    /// Describes a single constructor on a Manager class, including the
    /// injected dependencies and its location within the source file.
    /// </summary>
    public sealed class ManagerConstructorDescription
    {
        /// <summary>
        /// The textual signature of the constructor, including modifiers and
        /// parameter list (but not the body).
        /// </summary>
        public string SignatureText { get; set; }

        /// <summary>
        /// The full body text of the constructor (inside the braces).
        /// </summary>
        public string BodyText { get; set; }

        /// <summary>
        /// Inclusive line where the constructor starts in the source file.
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// Inclusive line where the constructor ends in the source file.
        /// </summary>
        public int? LineEnd { get; set; }

        /// <summary>
        /// Interfaces that this constructor depends on via parameters, e.g.
        /// IDeviceRepository, IAuditLogService. This is a per-constructor
        /// view; ManagerDescription.DependencyInterfaces is the flattened
        /// union across all constructors.
        /// </summary>
        public IReadOnlyList<string> DependencyInterfaces { get; set; }
    }

    /// <summary>
    /// Describes a single logical method on a Manager class.
    ///
    /// This is a semantic view: it captures the method's intent, signature,
    /// visibility, summary, and full body text, plus basic classification
    /// such as MethodKind and significance.
    /// </summary>
    public sealed class ManagerMethodDescription
    {
        /// <summary>
        /// Simple method name (e.g., CreateDeviceAsync).
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// XML-doc style summary for the method (if available).
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Simple return type name (e.g., Task, Task&lt;Device&gt;, Device).
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// Parameters for this method (name + type name).
        /// </summary>
        public IReadOnlyList<ManagerMethodParameterDescription> Parameters { get; set; }

        /// <summary>
        /// Lightweight classification of the method's role (Create, Update,
        /// Delete, Query, Validation, Other).
        /// </summary>
        public ManagerMethodKind MethodKind { get; set; } = ManagerMethodKind.Unknown;

        /// <summary>
        /// True when this method is treated as "significant" for Manager
        /// semantics – typically public methods and important workflow
        /// helpers.
        /// </summary>
        public bool IsSignificant { get; set; }

        /// <summary>
        /// True when the method is public.
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// True when the method is protected or internal.
        /// </summary>
        public bool IsProtectedOrInternal { get; set; }

        /// <summary>
        /// True when the method is private.
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        /// Inclusive line where the method starts in the source file.
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// Inclusive line where the method ends in the source file.
        /// </summary>
        public int? LineEnd { get; set; }

        /// <summary>
        /// Full body text of the method (inside the braces). This is kept
        /// here so that downstream components can decide how to chunk the
        /// body without having to re-resolve the syntax tree.
        /// </summary>
        public string BodyText { get; set; }
    }

    /// <summary>
    /// Describes a single parameter on a Manager method.
    /// </summary>
    public sealed class ManagerMethodParameterDescription
    {
        /// <summary>
        /// Parameter name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Simple type name for the parameter (e.g., Device, string,
        /// CancellationToken, IEnumerable&lt;Device&gt;).
        /// </summary>
        public string TypeName { get; set; }
    }

    /// <summary>
    /// High-level Manager type classification. Currently mirrors the
    /// IDX-0039 chunk flavors but is defined here as a semantic concept
    /// so that the description layer does not depend on chunking details.
    /// </summary>
    public enum ManagerType
    {
        Unknown = 0,

        /// <summary>
        /// Standard Manager with a class-level overview and methods.
        /// </summary>
        ManagerOverview = 1,

        /// <summary>
        /// Manager primarily characterized by its methods (typical case).
        /// </summary>
        ManagerMethod = 2,

        /// <summary>
        /// Manager with very large methods that may require special
        /// handling downstream. Included for future extensibility.
        /// </summary>
        ManagerMethodOverflow = 3
    }

    /// <summary>
    /// Lightweight method categories for Manager methods, used as optional
    /// metadata in IDX-0039.
    /// </summary>
    public enum ManagerMethodKind
    {
        Unknown = 0,
        Create = 1,
        Update = 2,
        Delete = 3,
        Query = 4,
        Validation = 5,
        Other = 6
    }
}
