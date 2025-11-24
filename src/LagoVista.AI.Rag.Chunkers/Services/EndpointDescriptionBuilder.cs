using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// IDX-0041: Builds one EndpointDescription per controller action
    /// detected as an HTTP endpoint.
    ///
    /// This is a semantic builder only – it does NOT create chunks or
    /// assign PartIndex/PartTotal/ContentHash. It simply returns a set
    /// of EndpointDescription objects in source order.
    /// </summary>
    public static class EndpointDescriptionBuilder
    {
        /// <summary>
        /// Creates EndpointDescriptions for each HTTP endpoint found in the
        /// given controller source.
        /// </summary>
        /// <param name="sourceText">Full C# source of the controller file.</param>
        /// <returns>Read-only list of EndpointDescription objects.</returns>
        public static IReadOnlyList<EndpointDescription> CreateEndpointDescriptions(IndexFileContext ctx, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ArgumentNullException(nameof(sourceText));

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create(
                "EndpointAnalysis",
                syntaxTrees: new[] { tree },
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                });

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

            var results = new List<EndpointDescription>();

            var controllerClasses = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(IsControllerClass)
                .ToList();

            foreach (var controller in controllerClasses)
            {
                var controllerName = controller.Identifier.Text;

                var classRoute = GetRouteTemplate(controller.AttributeLists);
                var apiVersion = GetApiVersion(controller.AttributeLists);
                var area = GetArea(controller.AttributeLists);

                foreach (var method in controller.Members.OfType<MethodDeclarationSyntax>())
                {
                    var httpMethods = GetHttpMethods(method.AttributeLists);
                    if (httpMethods.Count == 0)
                        continue; // Not an endpoint

                    var route = CombineRoutes(classRoute, GetRouteTemplate(method.AttributeLists));

                    var endpoint = new EndpointDescription
                    {
                        ControllerName = controllerName,
                        ActionName = method.Identifier.Text,
                        EndpointKey = BuildEndpointKey(controllerName, method.Identifier.Text),
                        RouteTemplate = route,
                        HttpMethods = httpMethods,
                        ApiVersion = apiVersion,
                        Area = area,
                        LineStart = GetLine(method.GetLocation()?.GetLineSpan().StartLinePosition.Line),
                        LineEnd = GetLine(method.GetLocation()?.GetLineSpan().EndLinePosition.Line)
                    };

                    // PrimaryEntity heuristics – prefer return type model over controller name
                    endpoint.PrimaryEntity = DetectPrimaryEntity(controllerName, route, method, semanticModel);

                    // Summary & Description (after PrimaryEntity is established)
                    endpoint.Summary = GetXmlSummary(method)
                                       ?? SynthesizeSummary(httpMethods, endpoint.PrimaryEntity, endpoint.ActionName);

                    endpoint.Description = GetXmlRemarks(method);

                    // Handler (Manager linkage)
                    endpoint.Handler = DetectHandler(controller, method, semanticModel);

                    // Parameters / Request Body
                    var parameters = new List<EndpointParameterDescription>();
                    EndpointRequestBodyDescription body = null;

                    foreach (var param in method.ParameterList.Parameters)
                    {
                        if (IsServiceParameter(param, semanticModel))
                            continue;

                        var source = DetectParameterSource(param, route, httpMethods);
                        var type = semanticModel.GetTypeInfo(param.Type).Type;

                        var isCollection = IsCollectionType(type);
                        var isPrimitive = IsPrimitiveType(type);

                        // Candidate for body
                        if (IsBodyCandidate(source, httpMethods, type) && body == null)
                        {
                            body = new EndpointRequestBodyDescription
                            {
                                ModelType = type?.Name ?? param.Type.ToString(),
                                IsCollection = isCollection,
                                IsPrimitive = isPrimitive,
                                ContentTypes = new[] { "application/json" },
                                Description = GetXmlParamDescription(method, param.Identifier.Text)
                            };

                            continue;
                        }

                        parameters.Add(new EndpointParameterDescription
                        {
                            Name = param.Identifier.Text,
                            Source = source,
                            Type = type?.Name ?? param.Type.ToString(),
                            IsCollection = isCollection,
                            IsRequired = IsRequired(param, type),
                            DefaultValue = param.Default?.Value.ToString(),
                            Description = GetXmlParamDescription(method, param.Identifier.Text)
                        });
                    }

                    endpoint.Parameters = parameters;
                    endpoint.RequestBody = body;

                    // Responses
                    endpoint.Responses = DetectResponses(method, semanticModel, httpMethods);

                    // UI pattern classification (FormLoad / FormFactory / List / Command / Detail)
                    ClassifyUiPattern(endpoint, method, semanticModel, httpMethods);

                    // Authorization
                    endpoint.Authorization = DetectAuthorization(method, controller);

                    results.Add(endpoint);
                }
            }

            return results;
        }

        #region Detection Helpers

        private static bool IsControllerClass(ClassDeclarationSyntax cls)
        {
            if (cls.BaseList?.Types.Any(t => t.ToString().Contains("Controller")) == true)
                return true;

            if (cls.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal))
                return true;

            return false;
        }

        private static List<string> GetHttpMethods(SyntaxList<AttributeListSyntax> attributes)
        {
            var methods = new List<string>();

            foreach (var attr in attributes.SelectMany(a => a.Attributes))
            {
                var name = attr.Name.ToString();

                if (name.Contains("HttpGet")) methods.Add("GET");
                else if (name.Contains("HttpPost")) methods.Add("POST");
                else if (name.Contains("HttpPut")) methods.Add("PUT");
                else if (name.Contains("HttpDelete")) methods.Add("DELETE");
                else if (name.Contains("HttpPatch")) methods.Add("PATCH");
            }

            return methods;
        }

        private static string GetRouteTemplate(SyntaxList<AttributeListSyntax> attributes)
        {
            foreach (var attr in attributes.SelectMany(a => a.Attributes))
            {
                var name = attr.Name.ToString();

                // Treat both [Route] and [Http*("<template>")] as route providers
                var isRouteAttr =
                    name.Contains("Route") ||
                    name.Contains("HttpGet") ||
                    name.Contains("HttpPost") ||
                    name.Contains("HttpPut") ||
                    name.Contains("HttpDelete") ||
                    name.Contains("HttpPatch");

                if (!isRouteAttr)
                    continue;

                var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                if (arg == null)
                    continue;

                // Best case: [HttpGet("/api/...")]
                if (arg.Expression is LiteralExpressionSyntax lit &&
                    lit.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    // ValueText gives the string *without* quotes
                    return lit.Token.ValueText;
                }

                // Fallback when Roslyn gives us something like: "/api/..." as text
                var text = arg.Expression.ToString().Trim();
                if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
                {
                    text = text.Substring(1, text.Length - 2);
                }

                return text;
            }

            return null;
        }

        private static string GetApiVersion(SyntaxList<AttributeListSyntax> attributes)
        {
            foreach (var attr in attributes.SelectMany(a => a.Attributes))
            {
                if (attr.Name.ToString().Contains("ApiVersion"))
                {
                    return attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"');
                }
            }

            return null;
        }

        private static string GetArea(SyntaxList<AttributeListSyntax> attributes)
        {
            foreach (var attr in attributes.SelectMany(a => a.Attributes))
            {
                if (attr.Name.ToString().Contains("Area"))
                {
                    return attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"');
                }
            }

            return null;
        }

        private static string CombineRoutes(string classRoute, string methodRoute)
        {
            if (string.IsNullOrEmpty(classRoute)) return methodRoute;
            if (string.IsNullOrEmpty(methodRoute)) return classRoute;

            return classRoute.TrimEnd('/') + "/" + methodRoute.TrimStart('/');
        }

        private static string BuildEndpointKey(string controller, string action)
        {
            if (action.EndsWith("Async"))
                action = action.Substring(0, action.Length - 5);

            return controller + "." + action;
        }

        private static string GetXmlSummary(MemberDeclarationSyntax member)
        {
            var trivia = member.GetLeadingTrivia()
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (trivia == null) return null;

            var summary = trivia.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

            return summary?.ToString()
                .Replace("<summary>", "")
                .Replace("</summary>", "")
                .Trim();
        }

        private static string GetXmlRemarks(MemberDeclarationSyntax member)
        {
            var trivia = member.GetLeadingTrivia()
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (trivia == null) return null;

            var remarks = trivia.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(e => e.StartTag.Name.ToString() == "remarks");

            return remarks?.ToString()
                .Replace("<remarks>", "")
                .Replace("</remarks>", "")
                .Trim();
        }

        private static string GetXmlParamDescription(MethodDeclarationSyntax method, string paramName)
        {
            var trivia = method.GetLeadingTrivia()
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (trivia == null) return null;

            var param = trivia.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(e => e.StartTag.Name.ToString() == "param" &&
                                     e.StartTag.Attributes.ToString().Contains(paramName));

            return param?.ToString()
                .Replace("<param name=\"" + paramName + "\">", "")
                .Replace("</param>", "")
                .Trim();
        }

        private static string SynthesizeSummary(List<string> httpMethods, string entity, string action)
        {
            var method = httpMethods.FirstOrDefault() ?? "UNKNOWN";
            var target = !string.IsNullOrWhiteSpace(entity) ? entity : action;

            return method + " " + target;
        }

        private static string DetectPrimaryEntity(
            string controllerName,
            string route,
            MethodDeclarationSyntax method,
            SemanticModel model)
        {
            // 1) Prefer logical model from return type (DetailResponse<T>, ListResponse<T>, InvokeResult<T>, etc.)
            var rawReturnType = model.GetTypeInfo(method.ReturnType).Type as ITypeSymbol;
            var analysis = AnalyzeReturnType(rawReturnType);
            if (!string.IsNullOrWhiteSpace(analysis.ModelType))
            {
                return analysis.ModelType;
            }

            // 2) Fall back to route last segment (ignoring {id})
            if (!string.IsNullOrWhiteSpace(route))
            {
                var parts = route
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    var last = parts[parts.Length - 1].Trim('{', '}');
                    if (!string.IsNullOrWhiteSpace(last) &&
                        !string.Equals(last, "id", StringComparison.OrdinalIgnoreCase))
                    {
                        return last;
                    }
                }
            }

            // 3) Finally, fall back to controller name (strip "Controller" suffix)
            if (controllerName.EndsWith("Controller", StringComparison.Ordinal))
            {
                return controllerName.Substring(0, controllerName.Length - "Controller".Length);
            }

            return null;
        }

        private static EndpointHandlerDescription DetectHandler(
            ClassDeclarationSyntax controller,
            MethodDeclarationSyntax method,
            SemanticModel model)
        {
            var fields = controller.Members
                .OfType<FieldDeclarationSyntax>();

            foreach (var field in fields)
            {
                var fieldType = model.GetTypeInfo(field.Declaration.Type).Type as INamedTypeSymbol;
                if (fieldType == null) continue;

                if (!fieldType.Name.EndsWith("Manager"))
                    continue;

                var fieldName = field.Declaration.Variables.First().Identifier.Text;

                var calls = method.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Where(m => m.Expression.ToString() == fieldName)
                    .ToList();

                if (calls.Count > 0)
                {
                    return new EndpointHandlerDescription
                    {
                        Interface = fieldType.Name,
                        Method = calls.First().Name.Identifier.Text,
                        Kind = "Manager"
                    };
                }
            }

            return null;
        }

        private static List<EndpointResponseDescription> DetectResponses(
            MethodDeclarationSyntax method,
            SemanticModel model,
            List<string> httpMethods)
        {
            var list = new List<EndpointResponseDescription>();

            var rawReturnType = model.GetTypeInfo(method.ReturnType).Type as ITypeSymbol;

            var status = httpMethods.Contains("POST")
                ? 201
                : 200;

            var analysis = AnalyzeReturnType(rawReturnType);

            list.Add(new EndpointResponseDescription
            {
                StatusCode = status,
                Description = null,
                ModelType = analysis.ModelType,
                IsCollection = analysis.IsCollection,
                IsWrapped = analysis.IsWrapped,
                WrapperType = analysis.WrapperType,
                IsError = false,
                ContentTypes = new[] { "application/json" }
            });

            // Generic 400 error envelope – V1 default
            list.Add(new EndpointResponseDescription
            {
                StatusCode = 400,
                Description = "Validation or input error.",
                ModelType = null,
                IsCollection = false,
                IsWrapped = true,
                WrapperType = "InvokeResult",
                ErrorShape = "InvokeResult",
                IsError = true,
                ContentTypes = new[] { "application/json" }
            });

            return list;
        }

        private sealed class ResponseTypeAnalysis
        {
            public string ModelType { get; set; }
            public bool? IsCollection { get; set; }
            public bool? IsWrapped { get; set; }
            public string WrapperType { get; set; }
        }

        private static ResponseTypeAnalysis AnalyzeReturnType(ITypeSymbol rawType)
        {
            // Nothing we can do
            if (rawType == null)
            {
                return new ResponseTypeAnalysis();
            }

            // Step 1: strip Task<T>, ActionResult<T>, etc.
            var current = rawType;

            if (current is INamedTypeSymbol named &&
                named.IsGenericType &&
                (named.Name == "Task" || named.Name == "ActionResult"))
            {
                current = named.TypeArguments.FirstOrDefault() ?? current;
            }

            var analysis = new ResponseTypeAnalysis();

            // Step 2: unwrap logical API wrappers we care about
            if (current is INamedTypeSymbol wrapper && wrapper.IsGenericType)
            {
                var wrapperName = wrapper.Name;

                if (wrapperName == "DetailResponse" ||
                    wrapperName == "ListResponse" ||
                    wrapperName == "InvokeResult")
                {
                    analysis.IsWrapped = true;
                    analysis.WrapperType = wrapper.ToDisplayString();

                    var inner = wrapper.TypeArguments.FirstOrDefault();

                    if (inner != null)
                    {
                        analysis.ModelType = inner.Name;

                        // ListResponse<T> means collection of T
                        if (wrapperName == "ListResponse")
                        {
                            analysis.IsCollection = true;
                        }
                        else
                        {
                            // Check if inner itself is a collection type
                            analysis.IsCollection = IsCollectionType(inner);
                        }

                        return analysis;
                    }

                    // InvokeResult with no generic type
                    if (wrapperName == "InvokeResult")
                    {
                        analysis.ModelType = null;
                        analysis.IsCollection = false;
                        return analysis;
                    }
                }
            }

            // Step 3: no known wrapper – treat as direct payload
            analysis.ModelType = current.Name;
            analysis.IsCollection = IsCollectionType(current);
            analysis.IsWrapped ??= false;

            return analysis;
        }

        private static void ClassifyUiPattern(
            EndpointDescription ep,
            MethodDeclarationSyntax method,
            SemanticModel semanticModel,
            List<string> httpMethods)
        {
            ep.UiPattern = null;
            ep.IsFormFactory = false;
            ep.IsFormLoad = false;
            ep.IsListEndpoint = false;
            ep.IsCommandEndpoint = false;

            var rawReturnType = semanticModel.GetTypeInfo(method.ReturnType).Type as ITypeSymbol;
            var analysis = AnalyzeReturnType(rawReturnType);

            var wrapper = analysis.WrapperType ?? string.Empty;
            var route = ep.RouteTemplate ?? string.Empty;
            var actionName = ep.ActionName ?? string.Empty;

            // DetailResponse<T> → form load/factory/detail
            if (!string.IsNullOrEmpty(wrapper) && wrapper.Contains("DetailResponse"))
            {
                // /factory route or *Factory action → blank form model
                if (route.EndsWith("/factory", StringComparison.OrdinalIgnoreCase) ||
                    actionName.EndsWith("Factory", StringComparison.OrdinalIgnoreCase))
                {
                    ep.UiPattern = "FormFactory";
                    ep.IsFormFactory = true;
                }
                // {id} route → load existing record
                else if (route.Contains("{id}", StringComparison.OrdinalIgnoreCase))
                {
                    ep.UiPattern = "FormLoad";
                    ep.IsFormLoad = true;
                }
                else
                {
                    ep.UiPattern = "Detail";
                }
            }
            // ListResponse<T> or explicit collection model
            else if ((!string.IsNullOrEmpty(wrapper) && wrapper.Contains("ListResponse")) ||
                     analysis.IsCollection == true)
            {
                ep.UiPattern = "List";
                ep.IsListEndpoint = true;
            }
            // Non-generic InvokeResult → command-style endpoint
            else if (!string.IsNullOrEmpty(wrapper) &&
                     wrapper.Contains("InvokeResult") &&
                     analysis.ModelType == null)
            {
                ep.UiPattern = "Command";
                ep.IsCommandEndpoint = true;
            }
        }

        private static EndpointAuthorizationDescription DetectAuthorization(
            MethodDeclarationSyntax method,
            ClassDeclarationSyntax controller)
        {
            var allAttributes = controller.AttributeLists.Concat(method.AttributeLists);

            // Treat LagoVista auth-style attributes as requiring authentication too.
            var hasAuthorizeLike = allAttributes
                .SelectMany(a => a.Attributes)
                .Any(a =>
                {
                    var name = a.Name.ToString();
                    return name.Contains("Authorize")
                           || name.Contains("ConfirmedUser")
                           || name.Contains("AppBuilder");
                });

            var hasAnonymous = method.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString().Contains("AllowAnonymous"));

            return new EndpointAuthorizationDescription
            {
                RequiresAuthentication = hasAuthorizeLike && !hasAnonymous,
                AllowAnonymous = hasAnonymous,
                Tenancy = hasAnonymous ? "Public" : "OrgScoped"
            };
        }

        private static EndpointParameterSource DetectParameterSource(
           ParameterSyntax param,
           string route,
           List<string> httpMethods)
        {
            foreach (var attr in param.AttributeLists.SelectMany(a => a.Attributes))
            {
                var name = attr.Name.ToString();

                if (name.Contains("FromRoute")) return EndpointParameterSource.Route;
                if (name.Contains("FromQuery")) return EndpointParameterSource.Query;
                if (name.Contains("FromHeader")) return EndpointParameterSource.Header;
            }

            if (!string.IsNullOrWhiteSpace(route) && route.Contains("{" + param.Identifier.Text + "}"))
                return EndpointParameterSource.Route;

            if (httpMethods.Contains("GET") || httpMethods.Contains("DELETE"))
                return EndpointParameterSource.Query;

            return EndpointParameterSource.Unknown;
        }

        private static bool IsBodyCandidate(
            EndpointParameterSource source,
            List<string> httpMethods,
            ITypeSymbol type)
        {
            if (source != EndpointParameterSource.Unknown)
                return false;

            if (!httpMethods.Any(m => m == "POST" || m == "PUT" || m == "PATCH"))
                return false;

            if (IsPrimitiveType(type))
                return false;

            return true;
        }

        private static bool IsPrimitiveType(ITypeSymbol type)
        {
            if (type == null) return false;

            if (type.SpecialType != SpecialType.None)
                return true;

            if (type.Name == "String" || type.Name == "Guid" || type.Name == "DateTime")
                return true;

            return false;
        }

        private static bool IsCollectionType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol) return true;

            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                if (named.Name == "List" || named.Name == "IEnumerable" || named.Name == "ICollection")
                    return true;
            }

            return false;
        }

        private static bool IsRequired(ParameterSyntax param, ITypeSymbol type)
        {
            if (param.AttributeLists.SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString().Contains("Required")))
                return true;

            if (type?.NullableAnnotation == NullableAnnotation.Annotated)
                return false;

            if (param.Default != null)
                return false;

            return true;
        }

        private static bool IsServiceParameter(ParameterSyntax param, SemanticModel model)
        {
            if (param.AttributeLists.SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString().Contains("FromServices")))
                return true;

            var type = model.GetTypeInfo(param.Type).Type;
            if (type == null) return false;

            if (type.Name.Contains("ILogger") || type.Name.Contains("HttpContext"))
                return true;

            return false;
        }

        private static int? GetLine(int? zeroBased) => zeroBased + 1;

        #endregion
    }
}
