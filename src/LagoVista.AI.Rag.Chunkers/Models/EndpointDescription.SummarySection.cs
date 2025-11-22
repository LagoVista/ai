using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for EndpointDescription (IDX-0041).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class EndpointDescription
    /// </summary>
    public sealed partial class EndpointDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            var symbol = !string.IsNullOrWhiteSpace(EndpointKey)
                ? EndpointKey
                : $"{ControllerName}.{ActionName}";

            var sections = new List<SummarySection>();

            var overview = new StringBuilder();

            overview.AppendLine($"Endpoint: {symbol}");

            if (!string.IsNullOrWhiteSpace(ControllerName))
                overview.AppendLine($"Controller: {ControllerName}");

            if (!string.IsNullOrWhiteSpace(ActionName))
                overview.AppendLine($"Action: {ActionName}");

            if (!string.IsNullOrWhiteSpace(RouteTemplate))
                overview.AppendLine($"Route: {RouteTemplate}");

            if (HttpMethods != null && HttpMethods.Count > 0)
                overview.AppendLine($"Methods: {string.Join(", ", HttpMethods)}");

            if (!string.IsNullOrWhiteSpace(ApiVersion))
                overview.AppendLine($"API Version: {ApiVersion}");

            if (!string.IsNullOrWhiteSpace(Area))
                overview.AppendLine($"Area: {Area}");

            if (!string.IsNullOrWhiteSpace(PrimaryEntity))
                overview.AppendLine($"Primary Entity: {PrimaryEntity}");

            if (Handler != null)
            {
                overview.AppendLine();
                overview.AppendLine("Handler:");
                if (!string.IsNullOrWhiteSpace(Handler.Interface)) overview.AppendLine($" - Interface: {Handler.Interface}");
                if (!string.IsNullOrWhiteSpace(Handler.Method)) overview.AppendLine($" - Method: {Handler.Method}");
                if (!string.IsNullOrWhiteSpace(Handler.Kind)) overview.AppendLine($" - Kind: {Handler.Kind}");
            }

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                overview.AppendLine();
                overview.AppendLine("Summary:");
                overview.AppendLine(Summary.Trim());
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                overview.AppendLine();
                overview.AppendLine("Description:");
                overview.AppendLine(Description.Trim());
            }

            if (Authorization != null)
            {
                overview.AppendLine();
                overview.AppendLine("Authorization:");
                overview.AppendLine($" - Requires Auth: {Authorization.RequiresAuthentication}");
                if (Authorization.AllowAnonymous)
                    overview.AppendLine(" - Allow Anonymous: true");

                if (Authorization.Roles != null && Authorization.Roles.Count > 0)
                    overview.AppendLine($" - Roles: {string.Join(", ", Authorization.Roles)}");

                if (Authorization.Policies != null && Authorization.Policies.Count > 0)
                    overview.AppendLine($" - Policies: {string.Join(", ", Authorization.Policies)}");

                if (Authorization.Scopes != null && Authorization.Scopes.Count > 0)
                    overview.AppendLine($" - Scopes: {string.Join(", ", Authorization.Scopes)}");

                if (!string.IsNullOrWhiteSpace(Authorization.Tenancy))
                    overview.AppendLine($" - Tenancy: {Authorization.Tenancy}");
            }

            if (Tags != null && Tags.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("Tags: " + string.Join(", ", Tags));
            }

            if (Notes != null && Notes.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("Notes:");
                foreach (var note in Notes)
                    overview.AppendLine(" - " + note);
            }

            sections.Add(new SummarySection
            {
                SectionKey = "endpoint-overview",
                Symbol = symbol,
                SymbolType = "Controller",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var contracts = new StringBuilder();
            contracts.AppendLine($"Request / Response for {symbol}:");

            if (Parameters != null && Parameters.Count > 0)
            {
                contracts.AppendLine("Parameters:");
                foreach (var p in Parameters)
                {
                    var requiredFlag = p.IsRequired ? " [required]" : string.Empty;
                    contracts.AppendLine($"- {p.Name} ({p.Type}) via {p.Source}{requiredFlag}");
                    if (!string.IsNullOrWhiteSpace(p.Description))
                        contracts.AppendLine($"  {p.Description}");
                    if (!string.IsNullOrWhiteSpace(p.DefaultValue))
                        contracts.AppendLine($"  Default: {p.DefaultValue}");
                }
            }

            if (RequestBody != null)
            {
                contracts.AppendLine();
                contracts.AppendLine("Request Body:");
                contracts.AppendLine($"- Type: {RequestBody.ModelType}");
                contracts.AppendLine($"- IsCollection: {RequestBody.IsCollection}");
                contracts.AppendLine($"- IsPrimitive: {RequestBody.IsPrimitive}");

                if (RequestBody.ContentTypes != null && RequestBody.ContentTypes.Count > 0)
                    contracts.AppendLine($"- ContentTypes: {string.Join(", ", RequestBody.ContentTypes)}");

                if (!string.IsNullOrWhiteSpace(RequestBody.Description))
                    contracts.AppendLine($"- {RequestBody.Description}");
            }

            if (Responses != null && Responses.Count > 0)
            {
                contracts.AppendLine();
                contracts.AppendLine("Responses:");
                foreach (var r in Responses.OrderBy(r => r.StatusCode))
                {
                    contracts.AppendLine($"- {r.StatusCode}: {r.Description}");
                    if (!string.IsNullOrWhiteSpace(r.ModelType))
                        contracts.AppendLine($"  Model: {r.ModelType}");
                    if (!string.IsNullOrWhiteSpace(r.WrapperType))
                        contracts.AppendLine($"  Wrapper: {r.WrapperType}");
                    if (r.IsCollection.HasValue)
                        contracts.AppendLine($"  IsCollection: {r.IsCollection}");
                    if (r.IsWrapped.HasValue)
                        contracts.AppendLine($"  IsWrapped: {r.IsWrapped}");
                    if (r.ContentTypes != null && r.ContentTypes.Count > 0)
                        contracts.AppendLine($"  ContentTypes: {string.Join(", ", r.ContentTypes)}");
                    if (r.IsError.HasValue)
                        contracts.AppendLine($"  IsError: {r.IsError}");
                    if (!string.IsNullOrWhiteSpace(r.ErrorShape))
                        contracts.AppendLine($"  ErrorShape: {r.ErrorShape}");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "endpoint-request-response",
                Symbol = symbol,
                SymbolType = "Controller",
                SectionNormalizedText = contracts.ToString().Trim()
            });

            return sections;
        }
    }
}
