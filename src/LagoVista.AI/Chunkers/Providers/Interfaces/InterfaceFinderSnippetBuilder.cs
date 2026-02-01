using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class InterfaceFinderSnippetBuilder
{
    // Keep this list small and explicit; extend as you encounter patterns.
    private static readonly Dictionary<string, string> VerbMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // CRUD-ish
        ["Create"] = "create",
        ["Add"] = "create",
        ["Insert"] = "create",
        ["Register"] = "create",

        ["Get"] = "read",
        ["Find"] = "read",
        ["Fetch"] = "read",
        ["Load"] = "read",
        ["Read"] = "read",

        ["Update"] = "update",
        ["Set"] = "update",
        ["Save"] = "update",
        ["Rename"] = "update",
        ["Mark"] = "mark",
        ["MarkAs"] = "mark",
        ["Approve"] = "approve",
        ["Disable"] = "disable",
        ["Enable"] = "enable",
        ["Clear"] = "clear",
        ["Reset"] = "reset",

        ["Delete"] = "delete",
        ["Remove"] = "delete",
        ["Revoke"] = "delete",

        ["List"] = "list",
        ["Search"] = "search",
        ["Query"] = "query",

        ["Count"] = "count",

        // Workflow (keep as-is; don’t coerce into CRUD)
        ["Begin"] = "begin",
        ["Complete"] = "complete",
        ["Handle"] = "handle",
        ["Confirm"] = "confirm",
        ["Verify"] = "verify",
        ["Accept"] = "accept",
        ["Consume"] = "consume",
        ["Rotate"] = "rotate"
    };

    // Types that are usually “context”, not the subject.
    private static readonly HashSet<string> IgnoredParamTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EntityHeader",
        "ListRequest"
    };

    // Primitive-ish types where the parameter name is more informative than the type.
    private static readonly HashSet<string> PrimitiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "string","bool","byte","sbyte","short","ushort","int","uint","long","ulong","float","double","decimal",
        "DateTime","Guid"
    };

    public static string BuildMethodAtom(InterfaceMethodDescription method)
    {
        if (method == null || string.IsNullOrWhiteSpace(method.Name))
            return null;

        return BuildMethodLine(method);
    }

    public static string BuildFinderSnippet(InterfaceDescription iface)
    {
        if (iface == null) throw new ArgumentNullException(nameof(iface));

        var lines = new List<string>();

        // Optional: tiny interface-level anchor to avoid Manager/Repo clustering.
        var role = NormalizeRole(iface.Role);
        if (!string.IsNullOrWhiteSpace(role)) lines.Add(role);

        // Methods -> capability atoms
        if (iface.Methods != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in iface.Methods)
            {
                var line = BuildMethodLine(m);
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Dedupe overloads / duplicates
                var key = line.Trim();
                if (seen.Add(key))
                    lines.Add(key);
            }
        }

        // Properties -> get/set atoms
        if (iface.Properties != null)
        {
            foreach (var p in iface.Properties)
            {
                var propType = NormalizeType(p?.Type);
                if (string.IsNullOrWhiteSpace(p?.Name) || string.IsNullOrWhiteSpace(propType)) continue;

                // Primitive: use property name (more meaning). Complex: type is also useful.
                var propToken = IsPrimitive(propType) ? ToWords(p.Name).ToLowerInvariant() : NormalizeTypeShort(propType);

                if (p.HasGetter)
                    lines.Add($"get {propToken} -> {NormalizeTypeShort(propType)}{(p.HasSetter ? "" : " (readonly)")}");

                if (p.HasSetter)
                    lines.Add($"set {propToken} -> {NormalizeTypeShort(propType)}");
            }
        }

        return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    private static string BuildMethodLine(InterfaceMethodDescription m)
    {
        if (m == null || string.IsNullOrWhiteSpace(m.Name)) return null;

        var stem = StripAsyncSuffix(m.Name);
        var (leadToken, remainderTokens) = SplitLeadingToken(stem);

        var verb = MapVerb(leadToken);
        var qualifiers = ExtractQualifiers(stem, m.Parameters);

        // Return shape
        var returnShape = NormalizeReturnShape(m.ReturnType);

        // Subject: prefer first meaningful complex parameter type, otherwise parse from name remainder.
        var subject = ExtractSubjectFromParameters(m.Parameters);
        if (string.IsNullOrWhiteSpace(subject))
            subject = ExtractSubjectFromName(remainderTokens);

        // If we still can’t find a subject, fall back to the method stem (de-Async’d).
        if (string.IsNullOrWhiteSpace(subject))
            subject = stem;

        // Special handling: MarkAsX style (e.g. MarkAsRead)
        // If verb mapped to "mark" and name starts with MarkAs..., add "as <state>" qualifier.
        if (leadToken.Equals("MarkAs", StringComparison.OrdinalIgnoreCase))
        {
            var state = ExtractStateFromMarkAs(stem);
            if (!string.IsNullOrWhiteSpace(state))
                qualifiers.Insert(0, $"as {state}");
        }

        // Count detection: if name contains "Count" and verb isn't already "count", prefer "count".
        if (stem.IndexOf("Count", StringComparison.OrdinalIgnoreCase) >= 0 && !verb.Equals("count", StringComparison.OrdinalIgnoreCase))
            verb = "count";

        // Compose
        var sb = new StringBuilder();
        sb.Append(verb);
        sb.Append(' ');
        sb.Append(subject);

        if (qualifiers.Count > 0)
        {
            sb.Append(' ');
            sb.Append(string.Join(' ', qualifiers));
        }

        if (!string.IsNullOrWhiteSpace(returnShape))
        {
            sb.Append(" -> ");
            sb.Append(returnShape);
        }

        return sb.ToString().Trim();
    }

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;

        // Keep it short and stable; it’s just an anchor token.
        if (role.IndexOf("Repo", StringComparison.OrdinalIgnoreCase) >= 0) return "repository";
        if (role.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0) return "manager";
        if (role.IndexOf("Service", StringComparison.OrdinalIgnoreCase) >= 0) return "service";
        if (role.IndexOf("Validator", StringComparison.OrdinalIgnoreCase) >= 0) return "validator";
        return null;
    }

    private static string StripAsyncSuffix(string name)
        => name.EndsWith("Async", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) : name;

    // Splits "GetUserById" -> ("Get", "UserById")
    // Splits "MarkAsRead" -> ("MarkAs", "Read")
    private static (string leadToken, string remainder) SplitLeadingToken(string stem)
    {
        if (stem.StartsWith("MarkAs", StringComparison.Ordinal))
            return ("MarkAs", stem.Substring("MarkAs".Length));

        // Take leading PascalCase token
        var match = Regex.Match(stem, @"^[A-Z][a-z0-9]*");
        if (!match.Success) return (stem, "");
        var lead = match.Value;
        return (lead, stem.Substring(lead.Length));
    }

    private static string MapVerb(string leadToken)
        => VerbMap.TryGetValue(leadToken, out var mapped) ? mapped : leadToken.ToLowerInvariant();

    private static List<string> ExtractQualifiers(string methodStem, IReadOnlyList<InterfaceMethodParameterDescription> parameters)
    {
        var q = new List<string>();

        // From method name fragments
        if (methodStem.IndexOf("WithSecrets", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("with secrets");
        if (methodStem.IndexOf("ForOrg", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("for org");
        if (methodStem.IndexOf("ForUser", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("for user");
        if (methodStem.IndexOf("Unread", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("unread");
        if (methodStem.IndexOf("Active", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("active");
        if (methodStem.IndexOf("Cached", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("cached");
        if (methodStem.IndexOf("WithoutOrgs", StringComparison.OrdinalIgnoreCase) >= 0) q.Add("without orgs");

        // From parameters
        var p = parameters ?? Array.Empty<InterfaceMethodParameterDescription>();

        bool hasPartition = p.Any(x => x?.Name?.Equals("partitionKey", StringComparison.OrdinalIgnoreCase) == true);
        bool hasRow = p.Any(x => x?.Name?.Equals("rowKey", StringComparison.OrdinalIgnoreCase) == true);
        if (hasPartition && hasRow) q.Add("by partition key and row key");

        bool hasId = p.Any(x => IsPrimitive(NormalizeType(x?.Type)) && IsIdLike(x?.Name));
        if (hasId) q.Add("by id");

        bool hasStepUp = p.Any(x => x?.Name?.Equals("isStepUp", StringComparison.OrdinalIgnoreCase) == true);
        if (hasStepUp) q.Add("step-up");

        bool hasRpId = p.Any(x => x?.Name?.Equals("rpId", StringComparison.OrdinalIgnoreCase) == true);
        if (hasRpId) q.Add("for rp id");

        bool hasCredentialId = p.Any(x => x?.Name?.Equals("credentialId", StringComparison.OrdinalIgnoreCase) == true);
        if (hasCredentialId) q.Add("by credential id");

        return q;
    }

    private static string ExtractStateFromMarkAs(string stem)
    {
        // MarkAsRead -> "read"
        var idx = stem.IndexOf("MarkAs", StringComparison.Ordinal);
        if (idx < 0) return null;
        var rest = stem.Substring(idx + "MarkAs".Length);
        rest = rest.Trim();
        if (string.IsNullOrWhiteSpace(rest)) return null;
        return ToWords(rest).ToLowerInvariant();
    }

    private static string ExtractSubjectFromParameters(IReadOnlyList<InterfaceMethodParameterDescription> parameters)
    {
        if (parameters == null) return null;

        foreach (var p in parameters)
        {
            var type = NormalizeType(p?.Type);
            if (string.IsNullOrWhiteSpace(type)) continue;

            if (IgnoredParamTypes.Contains(type)) continue;

            // Ignore primitives unless we have no other clue
            if (IsPrimitive(type)) continue;

            // Ignore common “context” types by name too (org/user headers)
            if (type.Equals("EntityHeader", StringComparison.OrdinalIgnoreCase)) continue;

            return NormalizeTypeShort(type);
        }

        return null;
    }

    private static string ExtractSubjectFromName(string remainder)
    {
        // remainder is whatever follows the leading verb token.
        // For "GetUserById", remainder = "UserById" -> "user"
        // For "GetUnreadInboxItems", remainder = "UnreadInboxItems" -> "inbox items"
        if (string.IsNullOrWhiteSpace(remainder)) return null;

        // Strip common suffix clauses that are qualifiers
        remainder = remainder.Replace("WithSecrets", "", StringComparison.OrdinalIgnoreCase);
        remainder = remainder.Replace("ForOrg", "", StringComparison.OrdinalIgnoreCase);
        remainder = remainder.Replace("ForUser", "", StringComparison.OrdinalIgnoreCase);
        remainder = remainder.Replace("ById", "", StringComparison.OrdinalIgnoreCase);

        // Remove leading qualifier-ish tokens
        remainder = Regex.Replace(remainder, @"^(Unread|Active|Cached)", "", RegexOptions.IgnoreCase);

        remainder = remainder.Trim();
        if (string.IsNullOrWhiteSpace(remainder)) return null;

        return ToWords(remainder).ToLowerInvariant();
    }

    private static bool IsIdLike(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Equals("id", StringComparison.OrdinalIgnoreCase)) return true;
        return name.EndsWith("Id", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReturnShape(string rawReturnType)
    {
        if (string.IsNullOrWhiteSpace(rawReturnType)) return null;

        var t = rawReturnType.Trim();

        // Strip Task / Task<T>
        t = Regex.Replace(t, @"^Task\s*<\s*(.+)\s*>\s*$", "$1", RegexOptions.Singleline);
        t = Regex.Replace(t, @"^Task\s*$", "void", RegexOptions.Singleline);

        t = t.Trim();

        // Keep just outer generic name + generic arg for common wrappers
        // e.g. InvokeResult<PasskeySignInResult>, ListResponse<UserInfoSummary>, IEnumerable<EntityHeader>
        t = t.Replace("System.Collections.Generic.", "", StringComparison.Ordinal);
        t = t.Replace("System.Threading.Tasks.", "", StringComparison.Ordinal);

        // Normalize whitespace
        t = Regex.Replace(t, @"\s+", "");

        return t;
    }

    private static string NormalizeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();
        t = t.Replace("System.", "", StringComparison.Ordinal);
        t = t.Replace("System.Collections.Generic.", "", StringComparison.Ordinal);
        t = Regex.Replace(t, @"\s+", "");
        return t;
    }

    private static string NormalizeTypeShort(string type)
    {
        // If type is "Namespace.Foo.Bar", keep last segment.
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();
        var lastDot = t.LastIndexOf('.');
        return lastDot >= 0 ? t.Substring(lastDot + 1) : t;
    }

    private static bool IsPrimitive(string type)
        => !string.IsNullOrWhiteSpace(type) && PrimitiveTypes.Contains(NormalizeTypeShort(type));

    private static string ToWords(string pascalOrCamel)
    {
        if (string.IsNullOrWhiteSpace(pascalOrCamel)) return pascalOrCamel;
        // "UserById" -> "User By Id"
        return Regex.Replace(pascalOrCamel, @"(?<!^)([A-Z][a-z0-9]*)", " $1").Trim();
    }
}
