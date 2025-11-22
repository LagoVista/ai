using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Pure semantic description of a Repository class (IDX-0040).
    /// No chunking/indexing concerns here – this is the input to later
    /// chunk builders and RAG payload generators.
    /// </summary>
    public class RepositoryDescription
    {
        /// <summary>
        /// Simple name of the repository class, e.g. AgentContextRepo.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Namespace that contains the repository.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// XML summary (if present) on the repository class.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Name of the immediate base type, e.g. DocumentDBRepoBase.
        /// </summary>
        public string BaseTypeName { get; set; }

        /// <summary>
        /// Logical classification of the repository based on its base type.
        /// E.g. DocumentDb, TableStorage, Sql, InMemory, Other.
        /// </summary>
        public RepositoryKind RepositoryKind { get; set; }

        /// <summary>
        /// All interfaces implemented by the repository class (simple names only).
        /// </summary>
        public IReadOnlyList<string> ImplementedInterfaces { get; set; }

        /// <summary>
        /// Logical entity type this repository persists (IDX-0040 PrimaryEntity).
        /// </summary>
        public string PrimaryEntity { get; set; }

        /// <summary>
        /// Unique set of interface-typed dependencies pulled from ALL constructors.
        /// e.g. IAdminLogger, IMLRepoSettings, etc.
        /// </summary>
        public IReadOnlyList<string> DependencyInterfaces { get; set; }

        /// <summary>
        /// All constructors declared on the repository class.
        /// </summary>
        public IReadOnlyList<RepositoryConstructorDescription> Constructors { get; set; }

        /// <summary>
        /// All methods declared on the repository class.
        /// </summary>
        public IReadOnlyList<RepositoryMethodDescription> Methods { get; set; }

        /// <summary>
        /// Optional storage profile when it can be derived cheaply
        /// (e.g. from base class generics, constants, attributes).
        /// </summary>
        public RepositoryStorageProfileDescription StorageProfile { get; set; }
    }

    /// <summary>
    /// Description of a single constructor on a Repository.
    /// </summary>
    public class RepositoryConstructorDescription
    {
        /// <summary>
        /// Constructor parameters and their types.
        /// </summary>
        public IReadOnlyList<RepositoryMethodParameterDescription> Parameters { get; set; }

        /// <summary>
        /// 1-based line where the constructor starts (inclusive).
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// 1-based line where the constructor ends (inclusive).
        /// </summary>
        public int? LineEnd { get; set; }

        /// <summary>
        /// Raw constructor body text (for debugging / deeper analysis).
        /// </summary>
        public string BodyText { get; set; }
    }

    /// <summary>
    /// Description of a single repository method (IDX-0040 RepositoryMethod).
    /// </summary>
    public class RepositoryMethodDescription
    {
        /// <summary>
        /// Simple method name, e.g. GetAgentContextAsync.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// XML summary (if present) on the method.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Fully qualified return type name, e.g. System.Threading.Tasks.Task&lt;AgentContext&gt;.
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// Ordered list of parameters.
        /// </summary>
        public IReadOnlyList<RepositoryMethodParameterDescription> Parameters { get; set; }

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
        /// Heuristic flag for "interesting" methods (non-trivial persistence / query logic).
        /// </summary>
        public bool IsSignificant { get; set; }

        /// <summary>
        /// Logical classification of the repository method.
        /// </summary>
        public RepositoryMethodKind MethodKind { get; set; }

        /// <summary>
        /// 1-based line where the method starts (inclusive).
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// 1-based line where the method ends (inclusive).
        /// </summary>
        public int? LineEnd { get; set; }

        /// <summary>
        /// Raw method body text (for deeper analysis / summarization).
        /// </summary>
        public string BodyText { get; set; }
    }

    /// <summary>
    /// Description of a method or constructor parameter.
    /// </summary>
    public class RepositoryMethodParameterDescription
    {
        /// <summary>
        /// Parameter name as written in code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Simple type name, e.g. AgentContext, string, IAdminLogger.
        /// </summary>
        public string TypeName { get; set; }
    }

    /// <summary>
    /// Storage profile hints extracted from base types / attributes when possible.
    /// This mirrors the optional StorageProfile block in IDX-0040.
    /// </summary>
    public class RepositoryStorageProfileDescription
    {
        /// <summary>
        /// Storage technology kind, e.g. DocumentDb, TableStorage, Sql, InMemory.
        /// </summary>
        public string StorageKind { get; set; }

        /// <summary>
        /// Entity type stored in this collection/table, usually same as PrimaryEntity.
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Collection or table name, when discoverable.
        /// </summary>
        public string CollectionOrTable { get; set; }

        /// <summary>
        /// Partition key field name, when it can be clearly inferred.
        /// </summary>
        public string PartitionKeyField { get; set; }
    }

    /// <summary>
    /// Logical classification of repository methods (IDX-0040 MethodKind examples).
    /// </summary>
    public enum RepositoryMethodKind
    {
        Unknown = 0,
        GetById = 1,
        Query = 2,
        Insert = 3,
        Update = 4,
        Delete = 5,
        Other = 99
    }

    /// <summary>
    /// Logical classification of repositories based on their base type.
    /// This is distinct from StorageKind, which is a looser descriptor.
    /// </summary>
    public enum RepositoryKind
    {
        Unknown = 0,

        /// <summary>
        /// Repositories that inherit from a DocumentDB base, e.g. DocumentDBRepoBase&lt;T&gt;.
        /// </summary>
        DocumentDb = 1,

        /// <summary>
        /// Repositories backed by Azure Table Storage or similar table-style stores.
        /// </summary>
        TableStorage = 2,

        /// <summary>
        /// Repositories backed by relational databases (SQL Server, PostgreSQL, etc.).
        /// </summary>
        Sql = 3,

        /// <summary>
        /// In-memory / ephemeral repositories.
        /// </summary>
        InMemory = 4,

        /// <summary>
        /// Repositories backed by cloud file storage (e.g. Azure Blob Storage).
        /// </summary>
        CloudFileStorage = 5,

        /// <summary>
        /// Anything that does not clearly fall into another bucket.
        /// </summary>
        Other = 99
    }
}
