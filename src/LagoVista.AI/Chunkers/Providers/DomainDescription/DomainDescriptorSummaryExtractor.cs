using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI;
using LagoVista.Core.Attributes; // DomainDescriptorAttribute, DomainDescriptionAttribute
using LagoVista.Core.Models.UIMetaData; // DomainDescription
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LagoVista.AI.Chunkers.Providers.DomainDescription
{
    /// <summary>
    /// Uses Roslyn to parse C# source files that declare domain descriptors and
    /// produces <see cref="DomainSummaryInfo"/> objects for indexing.
    /// </summary>
    public static class DomainDescriptorSummaryExtractor
    {
        public static InvokeResult<IReadOnlyList<DomainSummaryInfo>> Extract(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            var summaries = new List<DomainSummaryInfo>();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!HasAttribute(classDecl.AttributeLists, "DomainDescriptor"))
                    continue;

                ExtractFromDomainClass(classDecl, summaries);
            }

            return InvokeResult<IReadOnlyList<DomainSummaryInfo>>.Create(summaries);
        }

        private static void ExtractFromDomainClass(
            ClassDeclarationSyntax classDecl,
            List<DomainSummaryInfo> target)
        {
            var typeName = classDecl.Identifier.Text;
            var fullTypeName = typeName;

            var ns = classDecl.Parent as NamespaceDeclarationSyntax;
            if (ns != null)
            {
                fullTypeName = ns.Name + "." + typeName;
            }

            var constStringFields = BuildConstStringLookup(classDecl);

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (!IsStaticPublic(prop))
                    continue;

                if (!IsDomainDescriptionType(prop.Type))
                    continue;

                var domainAttr = GetAttribute(prop.AttributeLists, "DomainDescription");
                if (domainAttr == null)
                    continue;

                var domainKey = ResolveDomainKey(domainAttr, constStringFields) ?? "";

                var (title, description, domainTypeName) = ExtractDomainDescriptionInitializer(prop);

                var domainType = Core.Models.UIMetaData.DomainDescription.DomainTypes.BusinessObject;
                if (!string.IsNullOrWhiteSpace(domainTypeName))
                {
                    if (Enum.TryParse<Core.Models.UIMetaData.DomainDescription.DomainTypes>(domainTypeName, ignoreCase: true, out var parsed))
                    {
                        domainType = parsed;
                    }
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = !string.IsNullOrWhiteSpace(domainKey) ? domainKey : prop.Identifier.Text;
                }

                var info = new DomainSummaryInfo(
                    domainKey: !string.IsNullOrWhiteSpace(domainKey) ? domainKey : title,
                    domainKeyName: ResolveDomainKeyName(domainAttr, constStringFields),
                    title: title,
                    description: description ?? string.Empty,
                    domainType: domainType,
                    sourceTypeName: fullTypeName,
                    sourcePropertyName: prop.Identifier.Text);

                target.Add(info);
            }
        }

        private static (string title, string description, string domainTypeName) ExtractDomainDescriptionInitializer(PropertyDeclarationSyntax prop)
        {
            var getter = prop.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
            if (getter == null)
                return (null, null, null);

            var returnStmt = getter.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
            if (returnStmt == null)
                return (null, null, null);

            if (!(returnStmt.Expression is ObjectCreationExpressionSyntax creation))
                return (null, null, null);

            if (creation.Initializer == null)
                return (null, null, null);

            string title = null;
            string description = null;
            string domainTypeName = null;

            foreach (var expr in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var leftName = expr.Left switch
                {
                    IdentifierNameSyntax ident => ident.Identifier.Text,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(leftName))
                    continue;

                if (leftName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    title = TryGetStringLiteral(expr.Right) ?? title;
                }
                else if (leftName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                {
                    description = TryGetStringLiteral(expr.Right) ?? description;
                }
                else if (leftName.Equals("DomainType", StringComparison.OrdinalIgnoreCase))
                {
                    domainTypeName = TryGetEnumMemberName(expr.Right) ?? domainTypeName;
                }
            }

            return (title, description, domainTypeName);
        }

        private static Dictionary<string, string> BuildConstStringLookup(ClassDeclarationSyntax classDecl)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    continue;

                if (!(field.Declaration.Type is PredefinedTypeSyntax pts) || !pts.Keyword.IsKind(SyntaxKind.StringKeyword))
                    continue;

                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    var value = TryGetStringLiteral(variable.Initializer?.Value);
                    if (!string.IsNullOrWhiteSpace(name) && value != null)
                    {
                        dict[name] = value;
                    }
                }
            }

            return dict;
        }

        private static string ResolveDomainKey(AttributeSyntax attr, Dictionary<string, string> constStrings)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                return null;

            var argExpr = attr.ArgumentList.Arguments[0].Expression;

            var literal = TryGetStringLiteral(argExpr);
            if (literal != null)
                return literal;

            if (argExpr is IdentifierNameSyntax ident)
            {
                if (constStrings.TryGetValue(ident.Identifier.Text, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string ResolveDomainKeyName(AttributeSyntax attr, Dictionary<string, string> constStrings)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                return null;

            var argExpr = attr.ArgumentList.Arguments[0].Expression;


            if (argExpr is IdentifierNameSyntax ident)
            {
                return ident.Identifier.Text;
            }

            return null;
        }

        private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string attributeName)
        {
            return GetAttribute(lists, attributeName) != null;
        }

        private static AttributeSyntax GetAttribute(SyntaxList<AttributeListSyntax> lists, string attributeName)
        {
            foreach (var list in lists)
            {
                foreach (var attr in list.Attributes)
                {
                    var name = attr.Name.ToString();

                    if (name.Equals(attributeName, StringComparison.Ordinal) ||
                        name.Equals(attributeName + "Attribute", StringComparison.Ordinal) ||
                        name.EndsWith("." + attributeName, StringComparison.Ordinal) ||
                        name.EndsWith("." + attributeName + "Attribute", StringComparison.Ordinal))
                    {
                        return attr;
                    }
                }
            }

            return null;
        }

        private static bool IsStaticPublic(PropertyDeclarationSyntax prop)
        {
            var mods = prop.Modifiers;
            return mods.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                   mods.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        }

        private static bool IsDomainDescriptionType(TypeSyntax typeSyntax)
        {
            var typeName = typeSyntax.ToString();
            return typeName.EndsWith("DomainDescription", StringComparison.Ordinal);
        }

        private static string TryGetStringLiteral(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }

            return null;
        }

        private static string TryGetEnumMemberName(ExpressionSyntax expr)
        {
            if (expr is MemberAccessExpressionSyntax member)
            {
                return member.Name.Identifier.Text;
            }

            if (expr is IdentifierNameSyntax ident)
            {
                return ident.Identifier.Text;
            }

            return null;
        }
    }
}
