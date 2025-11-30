using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Roslyn-based implementation of IModelMetadataSource that scans the
    /// provided C# files for classes with [EntityDescription] and properties
    /// with [FormField], resolving resource-backed title/description/help and
    /// field context from the supplied RESX dictionaries.
    ///
    /// Structural issues (missing LabelResource, unresolved title resources,
    /// malformed attributes, etc.) are recorded in ModelMetadata.Errors for
    /// the orchestrator to treat as failures.
    /// </summary>
    public class RoslynModelMetadataSource : IModelMetadataSource
    {
        private readonly IAdminLogger _logger;

        public RoslynModelMetadataSource(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<ModelMetadata>> GetModelsAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
            CancellationToken cancellationToken)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var results = new List<ModelMetadata>();

            var total = files.Count;
            for (var index = 0; index < total; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = files[index];

                if (file.IsBinary || !file.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _logger.Trace($"[RoslynModelMetadataSource_GetModelsAsync] Processing file {index + 1}/{total}: {file.FullPath}");

                string code;
                try
                {
                    code = await File.ReadAllTextAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.AddException("RoslynModelMetadataSource_GetModelsAsync", ex);
                    continue;
                }

                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = tree.GetRoot(cancellationToken);

                var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDecls)
                {
                    var entityAttributes = GetAttributesByName(classDecl.AttributeLists, "EntityDescription");
                    if (entityAttributes.Count == 0)
                    {
                        // Not a first-class model.
                        continue;
                    }

                    var model = new ModelMetadata
                    {
                        RepoId = file.RepoId,
                        FullPath = file.FullPath,
                        ClassName = classDecl.Identifier.Text
                    };

                    if (entityAttributes.Count > 1)
                    {
                        model.Errors.Add("Class has multiple [EntityDescription] attributes.");
                    }

                    var entityAttr = entityAttributes[0];

                    // [EntityDescription(domainConst, titleRes, descRes, helpRes, ...)]
                    if (entityAttr.ArgumentList == null || entityAttr.ArgumentList.Arguments.Count < 4)
                    {
                        model.Errors.Add("[EntityDescription] must have at least 4 positional arguments (domain, title, description, help).");
                    }
                    else
                    {
                        var args = entityAttr.ArgumentList.Arguments;

                        // Domain key is the constant reference; we normalize to the right-most identifier
                        // so that AIDomain.AIAdmin and AIAdmin both normalize to "AIAdmin".
                        model.DomainKey = ExtractSymbolKey(args[0].Expression);

                        model.TitleResourceKey = ExtractResourceKey(args[1].Expression);
                        model.DescriptionResourceKey = ExtractResourceKey(args[2].Expression);
                        model.HelpResourceKey = ExtractResourceKey(args[3].Expression);

                        if (string.IsNullOrWhiteSpace(model.TitleResourceKey))
                        {
                            model.Errors.Add("Title resource key could not be extracted from [EntityDescription].");
                        }

                        if (string.IsNullOrWhiteSpace(model.DescriptionResourceKey))
                        {
                            model.Errors.Add("Description resource key could not be extracted from [EntityDescription].");
                        }
                    }

                    // Resolve RESX values (title/description/help).
                    if (!string.IsNullOrWhiteSpace(model.TitleResourceKey))
                    {
                        model.Title = ResolveResource(resources, model.TitleResourceKey);
                        if (string.IsNullOrWhiteSpace(model.Title))
                        {
                            model.Errors.Add($"Title resource '{model.TitleResourceKey}' could not be resolved.");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(model.DescriptionResourceKey))
                    {
                        model.Description = ResolveResource(resources, model.DescriptionResourceKey);
                        if (string.IsNullOrWhiteSpace(model.Description))
                        {
                            model.Errors.Add($"Description resource '{model.DescriptionResourceKey}' could not be resolved.");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(model.HelpResourceKey))
                    {
                        model.Help = ResolveResource(resources, model.HelpResourceKey);
                        // Help is optional; unresolved help is not an error.
                    }

                    // Now scan properties for [FormField] attributes to build field context.
                    var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
                    foreach (var prop in properties)
                    {
                        var formAttrs = GetAttributesByName(prop.AttributeLists, "FormField");
                        if (formAttrs.Count == 0)
                        {
                            continue;
                        }

                        var fieldSummary = new FieldSummary
                        {
                            PropertyName = prop.Identifier.Text,
                            Label = prop.Identifier.Text // default; overridden if LabelResource resolves
                        };

                        var form = formAttrs[0];
                        bool hasLabelResourceArg = false;
                        string labelKey = null;

                        if (form.ArgumentList != null)
                        {
                            foreach (var arg in form.ArgumentList.Arguments)
                            {
                                var name = arg.NameEquals?.Name?.Identifier.Text
                                           ?? arg.NameColon?.Name?.Identifier.Text;

                                if (string.Equals(name, "LabelResource", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasLabelResourceArg = true;
                                    labelKey = ExtractResourceKey(arg.Expression);
                                    if (!string.IsNullOrWhiteSpace(labelKey))
                                    {
                                        var labelValue = ResolveResource(resources, labelKey);
                                        if (!string.IsNullOrWhiteSpace(labelValue))
                                        {
                                            fieldSummary.Label = labelValue;
                                        }
                                        else
                                        {
                                            model.Errors.Add($"FormField on property '{fieldSummary.PropertyName}' has LabelResource '{labelKey}' that could not be resolved.");
                                        }
                                    }
                                    else
                                    {
                                        model.Errors.Add($"FormField on property '{fieldSummary.PropertyName}' has a LabelResource expression that could not be parsed.");
                                    }
                                }
                                else if (string.Equals(name, "HelpResource", StringComparison.OrdinalIgnoreCase))
                                {
                                    var helpKey = ExtractResourceKey(arg.Expression);
                                    if (!string.IsNullOrWhiteSpace(helpKey))
                                    {
                                        var helpValue = ResolveResource(resources, helpKey);
                                        if (!string.IsNullOrWhiteSpace(helpValue))
                                        {
                                            fieldSummary.Help = helpValue;
                                        }
                                        // Help is optional; unresolved help is not an error.
                                    }
                                }
                            }
                        }

                        if (!hasLabelResourceArg)
                        {
                            model.Errors.Add($"FormField on property '{fieldSummary.PropertyName}' is missing a LabelResource argument.");
                        }

                        model.Fields.Add(fieldSummary);
                    }

                    results.Add(model);
                }
            }

            return results;
        }

        private static List<AttributeSyntax> GetAttributesByName(SyntaxList<AttributeListSyntax> attributeLists, string simpleName)
        {
            var list = new List<AttributeSyntax>();
            foreach (var attrList in attributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name.Equals(simpleName, StringComparison.Ordinal) ||
                        name.Equals(simpleName + "Attribute", StringComparison.Ordinal) ||
                        name.EndsWith("." + simpleName, StringComparison.Ordinal) ||
                        name.EndsWith("." + simpleName + "Attribute", StringComparison.Ordinal))
                    {
                        list.Add(attr);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Extracts a resource key from an expression like
        /// AIResources.Names.AiAgentContext_Title or a simple string literal.
        /// We normalize to the right-most identifier so fully-qualified member
        /// access collapses to just the resource name.
        /// </summary>
        private static string ExtractResourceKey(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    return literal.Token.ValueText;

                case IdentifierNameSyntax id:
                    return id.Identifier.Text;

                case MemberAccessExpressionSyntax member:
                    return GetRightmostIdentifier(member);

                default:
                    return expr.ToString().Trim();
            }
        }

        /// <summary>
        /// Extracts a symbol key used for linking models to domains. For
        /// domain constants we only care about the right-most identifier,
        /// so AIDomain.AIAdmin and AIAdmin both normalize to "AIAdmin".
        /// </summary>
        private static string ExtractSymbolKey(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case IdentifierNameSyntax id:
                    return id.Identifier.Text;

                case MemberAccessExpressionSyntax member:
                    return GetRightmostIdentifier(member);

                default:
                    return expr.ToString().Trim();
            }
        }

        private static string GetRightmostIdentifier(MemberAccessExpressionSyntax member)
        {
            if (member.Name is IdentifierNameSyntax id)
            {
                return id.Identifier.Text;
            }

            if (member.Expression is MemberAccessExpressionSyntax inner)
            {
                return GetRightmostIdentifier(inner);
            }

            return member.Name.ToString();
        }

        private static string ResolveResource(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources,
            string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (var kvp in resources)
            {
                var inner = kvp.Value;
                if (inner != null && inner.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
