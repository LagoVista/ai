// Path: LagoVista.AI.Rag.Chunkers/Models/EndpointDescription.SummarySection.cs

using LagoVista.AI.Rag.Chunkers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for EndpointDescription (IDX-0041).
    /// Converts structured endpoint metadata into human-readable sections
    /// suitable for embedding.
    /// </summary>
    public sealed partial class EndpointDescription : ISummarySectionBuilder
    {
        /// <summary>
        /// Builds human-readable summary sections for this HTTP endpoint,
        /// enriched with domain/model context.
        /// </summary>
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            var sections = new List<SummarySection>();

            // Prefer EndpointKey, then Controller.Action, then Controller, then fallback.
            string symbol;
            if (!string.IsNullOrWhiteSpace(EndpointKey))
            {
                symbol = EndpointKey;
            }
            else if (!string.IsNullOrWhiteSpace(ControllerName) && !string.IsNullOrWhiteSpace(ActionName))
            {
                symbol = $"{ControllerName}.{ActionName}";
            }
            else if (!string.IsNullOrWhiteSpace(ControllerName))
            {
                symbol = ControllerName;
            }
            else
            {
                symbol = "(unknown-endpoint)";
            }

            // -----------------------------------------------------------------
            // endpoint-overview
            // -----------------------------------------------------------------
            var overview = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                overview.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                overview.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
            {
                overview.AppendLine();
            }

            overview.AppendLine($"Endpoint: {symbol}");

            if (!string.IsNullOrWhiteSpace(ControllerName))
            {
                overview.AppendLine($"Controller: {ControllerName}");
            }

            if (!string.IsNullOrWhiteSpace(ActionName))
            {
                overview.AppendLine($"Action: {ActionName}");
            }

            if (!string.IsNullOrWhiteSpace(RouteTemplate))
            {
                overview.AppendLine($"Route: {RouteTemplate}");
            }

            if (HttpMethods != null && HttpMethods.Count > 0)
            {
                overview.AppendLine($"HTTP Methods: {string.Join(", ", HttpMethods)}");
            }

            if (!string.IsNullOrWhiteSpace(ApiVersion))
            {
                overview.AppendLine($"API Version: {ApiVersion}");
            }

            if (!string.IsNullOrWhiteSpace(Area))
            {
                overview.AppendLine($"Area: {Area}");
            }

            if (!string.IsNullOrWhiteSpace(PrimaryEntity))
            {
                overview.AppendLine($"Primary Entity: {PrimaryEntity}");
            }

            if (Handler != null)
            {
                overview.AppendLine();
                overview.AppendLine("Handler:");

                if (!string.IsNullOrWhiteSpace(Handler.Interface))
                {
                    overview.AppendLine($"  Interface: {Handler.Interface}");
                }

                if (!string.IsNullOrWhiteSpace(Handler.Method))
                {
                    overview.AppendLine($"  Method: {Handler.Method}");
                }

                if (!string.IsNullOrWhiteSpace(Handler.Kind))
                {
                    overview.AppendLine($"  Kind: {Handler.Kind}");
                }
            }

            if (Authorization != null)
            {
                overview.AppendLine();
                overview.AppendLine("Authorization:");
                overview.AppendLine($"  Requires Authentication: {Authorization.RequiresAuthentication}");
                overview.AppendLine($"  Allow Anonymous: {Authorization.AllowAnonymous}");

                if (Authorization.Roles != null && Authorization.Roles.Count > 0)
                {
                    overview.AppendLine($"  Roles: {string.Join(", ", Authorization.Roles)}");
                }

                if (Authorization.Policies != null && Authorization.Policies.Count > 0)
                {
                    overview.AppendLine($"  Policies: {string.Join(", ", Authorization.Policies)}");
                }

                if (Authorization.Scopes != null && Authorization.Scopes.Count > 0)
                {
                    overview.AppendLine($"  Scopes: {string.Join(", ", Authorization.Scopes)}");
                }

                if (!string.IsNullOrWhiteSpace(Authorization.Tenancy))
                {
                    overview.AppendLine($"  Tenancy: {Authorization.Tenancy}");
                }
            }

            // UI pattern – form/list/command semantics + UI component hints
            if (!string.IsNullOrWhiteSpace(UiPattern))
            {
                overview.AppendLine();
                overview.AppendLine("UI Pattern:");

                switch (UiPattern)
                {
                    case "FormFactory":
                        overview.AppendLine(
                            "  This endpoint returns a blank, initialized model instance " +
                            "for client-side form creation (form factory).");
                        overview.AppendLine(
                            "  In typical NuvOS UIs this is rendered using the " +
                            "<nuvos-formviewer> Angular component (FormResponseViewerComponent).");
                        break;

                    case "FormLoad":
                        overview.AppendLine(
                            "  This endpoint loads an existing model instance for editing " +
                            "using an id route parameter.");
                        overview.AppendLine(
                            "  In typical NuvOS UIs this is rendered using the " +
                            "<nuvos-formviewer> Angular component (FormResponseViewerComponent).");
                        break;

                    case "List":
                        overview.AppendLine(
                            "  This endpoint returns a list of models suitable for list or " +
                            "grid rendering, typically using paging.");
                        overview.AppendLine(
                            "  In typical NuvOS UIs this is rendered using the " +
                            "<nuvos-listviewer> Angular component (ListResponseViewViewComponent).");
                        break;

                    case "Command":
                        overview.AppendLine(
                            "  This endpoint executes a command and returns an InvokeResult " +
                            "indicating success or failure.");
                        break;

                    case "Detail":
                        overview.AppendLine(
                            "  This endpoint returns a detailed model instance suitable for " +
                            "display or editing.");
                        overview.AppendLine(
                            "  In typical NuvOS UIs this is rendered using the " +
                            "<nuvos-formviewer> Angular component (FormResponseViewerComponent).");
                        break;

                    default:
                        overview.AppendLine($"  {UiPattern}");
                        break;
                }
            }

            if (Tags != null && Tags.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine($"Tags: {string.Join(", ", Tags)}");
            }

            if (Notes != null && Notes.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("Notes:");
                foreach (var note in Notes)
                {
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        overview.AppendLine($"- {note}");
                    }
                }
            }

            if (LineStart.HasValue || LineEnd.HasValue)
            {
                overview.AppendLine();
                overview.AppendLine($"Lines: {LineStart ?? 0}-{LineEnd ?? 0}");
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

            sections.Add(new SummarySection
            {
                SectionKey = "endpoint-overview",
                SectionType = "Overview",
                Flavor = "EndpointDescription",
                Symbol = symbol,
                SymbolType = "Endpoint",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // -----------------------------------------------------------------
            // endpoint-request-response (contracts + parameters)
            // -----------------------------------------------------------------

            var contracts = new StringBuilder();

            domainLine = BuildDomainLine(headerInfo);
            modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
            {
                contracts.AppendLine(domainLine);
            }

            if (!string.IsNullOrWhiteSpace(modelLine))
            {
                contracts.AppendLine(modelLine);
            }

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
            {
                contracts.AppendLine();
            }

            contracts.AppendLine($"Request and response shape for endpoint {symbol}:");

            // Non-body parameters
            contracts.AppendLine();
            contracts.AppendLine("Non-body parameters:");

            if (Parameters == null || Parameters.Count == 0)
            {
                contracts.AppendLine("  None.");
            }
            else
            {
                foreach (var param in Parameters)
                {
                    if (param == null)
                    {
                        continue;
                    }

                    var defaultValue = string.IsNullOrWhiteSpace(param.DefaultValue)
                        ? "(none)"
                        : param.DefaultValue;

                    contracts.AppendLine(
                        $"- {param.Name}: Source={param.Source}, Type={param.Type}, " +
                        $"Required={param.IsRequired}, Collection={param.IsCollection}, Default={defaultValue}");

                    if (!string.IsNullOrWhiteSpace(param.Description))
                    {
                        contracts.AppendLine("  " + param.Description.Trim());
                    }
                }
            }

            // Request body
            contracts.AppendLine();
            contracts.AppendLine("Request body:");

            if (RequestBody == null)
            {
                contracts.AppendLine("  None.");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(RequestBody.ModelType))
                {
                    contracts.AppendLine($"  Model: {RequestBody.ModelType}");
                }

                contracts.AppendLine($"  IsCollection: {RequestBody.IsCollection}");
                contracts.AppendLine($"  IsPrimitive: {RequestBody.IsPrimitive}");

                if (RequestBody.ContentTypes != null && RequestBody.ContentTypes.Count > 0)
                {
                    contracts.AppendLine("  ContentTypes: " +
                        string.Join(", ", RequestBody.ContentTypes));
                }

                if (!string.IsNullOrWhiteSpace(RequestBody.Description))
                {
                    contracts.AppendLine("  " + RequestBody.Description.Trim());
                }
            }

            // Responses
            contracts.AppendLine();
            contracts.AppendLine("Responses:");

            if (Responses == null || Responses.Count == 0)
            {
                contracts.AppendLine("  None.");
            }
            else
            {
                foreach (var resp in Responses.OrderBy(r => r.StatusCode))
                {
                    if (resp == null)
                    {
                        continue;
                    }

                    contracts.AppendLine();
                    contracts.AppendLine($"- {resp.StatusCode}: {resp.Description}");

                    if (!string.IsNullOrWhiteSpace(resp.ModelType))
                    {
                        contracts.AppendLine($"  Model: {resp.ModelType}");
                    }

                    if (resp.IsCollection.HasValue)
                    {
                        contracts.AppendLine($"  IsCollection: {resp.IsCollection.Value}");
                    }

                    if (resp.IsWrapped.HasValue)
                    {
                        contracts.AppendLine($"  IsWrapped: {resp.IsWrapped.Value}");
                    }

                    if (!string.IsNullOrWhiteSpace(resp.WrapperType))
                    {
                        contracts.AppendLine($"  Wrapper: {resp.WrapperType}");
                    }

                    if (resp.ContentTypes != null && resp.ContentTypes.Count > 0)
                    {
                        contracts.AppendLine("  ContentTypes: " +
                            string.Join(", ", resp.ContentTypes));
                    }

                    if (resp.IsError.HasValue)
                    {
                        contracts.AppendLine($"  IsError: {resp.IsError.Value}");
                    }

                    if (!string.IsNullOrWhiteSpace(resp.ErrorShape))
                    {
                        contracts.AppendLine($"  ErrorShape: {resp.ErrorShape}");
                    }
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "endpoint-request-response",
                SectionType = "Contracts",
                Flavor = "EndpointDescription",
                Symbol = symbol,
                SymbolType = "Endpoint",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = contracts.ToString().Trim()
            });

            return sections;
        }

        private static string BuildDomainLine(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var hasName = !string.IsNullOrWhiteSpace(header.DomainName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.DomainTagLine);

            if (!hasName && !hasTagline)
            {
                return null;
            }

            if (hasName && hasTagline)
            {
                return $"Domain: {header.DomainName} — {header.DomainTagLine}";
            }

            if (hasName)
            {
                return $"Domain: {header.DomainName}";
            }

            return header.DomainTagLine;
        }

        private static string BuildModelLine(DomainModelHeaderInformation header)
        {
            if (header == null)
            {
                return null;
            }

            var modelName = !string.IsNullOrWhiteSpace(header.ModelName)
                ? header.ModelName
                : header.ModelClassName;

            var hasName = !string.IsNullOrWhiteSpace(modelName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.ModelTagLine);

            if (!hasName && !hasTagline)
            {
                return null;
            }

            if (hasName && hasTagline)
            {
                return $"Model: {modelName} — {header.ModelTagLine}";
            }

            if (hasName)
            {
                return $"Model: {modelName}";
            }

            return header.ModelTagLine;
        }
    }
}
