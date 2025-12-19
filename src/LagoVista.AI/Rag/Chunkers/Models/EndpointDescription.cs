using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0041: Semantic description of a single HTTP controller endpoint.
    ///
    /// This is a pure description model – it captures identity, handler
    /// linkage, request/response shape, authorization, and primary entity
    /// semantics, without encoding chunking/indexing concerns
    /// (no PartIndex, no ContentHash, etc.).
    /// </summary>
    public sealed partial class EndpointDescription : SummaryFacts
    {

        /// <summary>
        /// Source file path or name used to locate the controller in the repo.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Controller class name, e.g. DeviceController.
        /// </summary>
        public string ControllerName { get; set; }

        /// <summary>
        /// Action method name, e.g. GetDeviceAsync.
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Stable identifier for this endpoint, typically
        /// "<ControllerName>.<ActionNameWithoutAsyncSuffix>".
        /// Example: DeviceController.GetDevice.
        /// </summary>
        public string EndpointKey { get; set; }

        /// <summary>
        /// Effective route template, combining controller- and method-level
        /// routes, e.g. "api/devices/{id}".
        /// </summary>
        public string RouteTemplate { get; set; }

        /// <summary>
        /// HTTP methods for this endpoint, e.g. ["GET"], ["POST"].
        /// </summary>
        public IReadOnlyList<string> HttpMethods { get; set; }

        /// <summary>
        /// API version when available, e.g. "1.0", "v2".
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Logical area for this endpoint, e.g. Admin, DeviceManagement.
        /// </summary>
        public string Area { get; set; }

        public override string Subtype { get => "Endpoint"; }

        /// <summary>
        /// Handler linkage information (e.g., Manager interface and method)
        /// for this endpoint.
        /// </summary>
        public EndpointHandlerDescription Handler { get; set; }

        /// <summary>
        /// One-line summary of what the endpoint does.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Longer description of the endpoint behavior.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Free-form tags for classification, e.g. ["Device", "Read", "OrgScoped"].
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; }

        /// <summary>
        /// Additional notes or hints about behavior, usage, or implementation.
        /// </summary>
        public IReadOnlyList<string> Notes { get; set; }

        /// <summary>
        /// Non-body parameters (route/query/header/unknown) exposed by this
        /// endpoint.
        /// </summary>
        public IReadOnlyList<EndpointParameterDescription> Parameters { get; set; }

        /// <summary>
        /// Logical request body (if any). Omitted when the endpoint has no
        /// body according to IDX-0041 heuristics.
        /// </summary>
        public EndpointRequestBodyDescription RequestBody { get; set; }

        /// <summary>
        /// Response shapes for this endpoint, one entry per status code.
        /// </summary>
        public IReadOnlyList<EndpointResponseDescription> Responses { get; set; }

        /// <summary>
        /// Authorization and tenancy metadata for this endpoint.
        /// </summary>
        public EndpointAuthorizationDescription Authorization { get; set; }

        /// <summary>
        /// High-level UI/interaction pattern inferred from response & route,
        /// e.g. "FormLoad", "FormFactory", "List", "Command", "Detail".
        /// </summary>
        public string UiPattern { get; set; }

        /// <summary>
        /// True if this endpoint returns a blank, initialized model intended
        /// to hydrate a client-side create form (typically DetailResponse<T>
        /// with a /factory route).
        /// </summary>
        public bool IsFormFactory { get; set; }

        /// <summary>
        /// True if this endpoint loads an existing model instance for editing,
        /// typically DetailResponse<T> plus an {id} route parameter.
        /// </summary>
        public bool IsFormLoad { get; set; }

        /// <summary>
        /// True if this endpoint returns a list of models suitable for list
        /// or grid rendering, typically ListResponse<T>.
        /// </summary>
        public bool IsListEndpoint { get; set; }

        /// <summary>
        /// True if this endpoint is a command-style operation that returns an
        /// InvokeResult indicating success or failure without a model payload.
        /// </summary>
        public bool IsCommandEndpoint { get; set; }

        /// <summary>
        /// Inclusive 1-based line where the action method starts in the
        /// source file (IDX-020).
        /// </summary>
        public int? LineStart { get; set; }

        /// <summary>
        /// Inclusive 1-based line where the action method ends in the
        /// source file (IDX-020).
        /// </summary>
        public int? LineEnd { get; set; }

        /// <summary>
        /// Optional 0-based character offset for the first character of the
        /// action method (IDX-021).
        /// </summary>
        public int? CharStart { get; set; }

        /// <summary>
        /// Optional 0-based character offset for the last character of the
        /// action method (IDX-021).
        /// </summary>
        public int? CharEnd { get; set; }
    }

    /// <summary>
    /// Describes the primary handler (usually a Manager) invoked by a
    /// controller endpoint.
    /// </summary>
    public sealed class EndpointHandlerDescription
    {
        /// <summary>
        /// Name of the DI-injected interface used as the handler, e.g.
        /// IDeviceManager.
        /// </summary>
        public string Interface { get; set; }

        /// <summary>
        /// Name of the handler method invoked from the controller action,
        /// e.g. CreateDeviceAsync.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Logical kind of handler. Initially "Manager" for Manager-based
        /// handlers but extensible to other kinds later.
        /// </summary>
        public string Kind { get; set; }
    }

    /// <summary>
    /// Describes a non-body parameter (route, query, header, or unknown)
    /// for an endpoint.
    /// </summary>
    public sealed class EndpointParameterDescription
    {
        /// <summary>
        /// Parameter name as declared in the action method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Logical source of the parameter: Route, Query, Header, Unknown.
        /// </summary>
        public EndpointParameterSource Source { get; set; } = EndpointParameterSource.Unknown;

        /// <summary>
        /// Simple type name for the parameter, e.g. string, Guid,
        /// DeviceQueryRequest.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// True when the parameter is required according to IDX-0041
        /// heuristics (route parameter, non-nullable scalar without default,
        /// or explicitly marked required).
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// True when the parameter is a collection (List<T>, T[], etc.).
        /// </summary>
        public bool IsCollection { get; set; }

        /// <summary>
        /// Serialized default value when present, e.g. "false" or "10".
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Human-readable description for the parameter, typically sourced
        /// from XML documentation (<param name="...">).
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Describes the logical request body for an endpoint.
    /// At most one request body is modeled per endpoint.
    /// </summary>
    public sealed class EndpointRequestBodyDescription
    {
        /// <summary>
        /// Underlying payload model type, e.g. Device,
        /// CreateDeviceRequest.
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// True if the body is a collection (List<T>, T[], etc.).
        /// </summary>
        public bool IsCollection { get; set; }

        /// <summary>
        /// True if the body is a primitive/scalar type (string, Guid,
        /// int, etc.).
        /// </summary>
        public bool IsPrimitive { get; set; }

        /// <summary>
        /// Effective content types for the body. Defaults to
        /// ["application/json"] when not explicitly constrained.
        /// </summary>
        public IReadOnlyList<string> ContentTypes { get; set; }

        /// <summary>
        /// Human-readable description of the body payload.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Describes a single response shape for an endpoint (per status code).
    /// </summary>
    public sealed class EndpointResponseDescription
    {
        /// <summary>
        /// HTTP status code, e.g. 200, 201, 400, 404.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Human-readable explanation of the response.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Logical payload model type inside any framework wrapper, e.g.
        /// Device for Task<InvokeResult<Device>>.
        /// May be null for responses that have no payload model.
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// True if the payload is a collection (List<T>, T[], etc.).
        /// </summary>
        public bool? IsCollection { get; set; }

        /// <summary>
        /// True when the payload is wrapped in a standard wrapper type,
        /// e.g. InvokeResult or InvokeResult<T>.
        /// </summary>
        public bool? IsWrapped { get; set; }

        /// <summary>
        /// Wrapper type name, e.g. InvokeResult<Device>.
        /// </summary>
        public string WrapperType { get; set; }

        /// <summary>
        /// Effective content types for the response, e.g. ["application/json"].
        /// </summary>
        public IReadOnlyList<string> ContentTypes { get; set; }

        /// <summary>
        /// True for typical error responses (4xx, 5xx).
        /// </summary>
        public bool? IsError { get; set; }

        /// <summary>
        /// Describes the error envelope, e.g. "InvokeResult" when errors
        /// are represented via InvokeResult with Successful = false and
        /// Errors populated.
        /// </summary>
        public string ErrorShape { get; set; }
    }

    /// <summary>
    /// Authorization and tenancy metadata for an endpoint.
    /// </summary>
    public sealed class EndpointAuthorizationDescription
    {
        /// <summary>
        /// True when the endpoint requires authentication, typically when
        /// [Authorize] is present at the class or method level.
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// True when [AllowAnonymous] is present on the method.
        /// If true, RequiresAuthentication should be false.
        /// </summary>
        public bool AllowAnonymous { get; set; }

        /// <summary>
        /// Roles required for access, derived from attributes such as
        /// [Authorize(Roles = "OrgAdmin,Support")].
        /// </summary>
        public IReadOnlyList<string> Roles { get; set; }

        /// <summary>
        /// Policy names associated with this endpoint, derived from
        /// [Authorize(Policy = "...")] or custom authorization attributes.
        /// </summary>
        public IReadOnlyList<string> Policies { get; set; }

        /// <summary>
        /// OAuth-style scopes required for this endpoint, e.g.
        /// ["devices.read", "devices.write"].
        /// </summary>
        public IReadOnlyList<string> Scopes { get; set; }

        /// <summary>
        /// Logical tenancy classification, e.g. OrgScoped, UserScoped,
        /// System, Public.
        /// </summary>
        public string Tenancy { get; set; }
    }

    /// <summary>
    /// Logical source classification for endpoint parameters.
    /// </summary>
    public enum EndpointParameterSource
    {
        Unknown = 0,
        Route = 1,
        Query = 2,
        Header = 3
    }
}
