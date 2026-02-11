using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace EntityHeaderIndexBuilder;

internal static class SqliteIndexLoader
{
    public static int Run(string root, string dbPath, string entityHeaderCsv, string entityDescCsv, string controllerRoutesCsv)
    {
        Console.WriteLine("Mode    : SQLite Loader");
        Console.WriteLine($"DB      : {dbPath}");
        Console.WriteLine($"Input   : {Path.GetFileName(entityHeaderCsv)}, {Path.GetFileName(entityDescCsv)}, {Path.GetFileName(controllerRoutesCsv)}");

        if (!File.Exists(entityHeaderCsv) || !File.Exists(entityDescCsv) || !File.Exists(controllerRoutesCsv))
        {
            Console.Error.WriteLine("One or more input CSV files are missing in Output/. Run the scanners first.");
            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY;";
            cmd.ExecuteNonQuery();
        }

        CreateSchema(conn);

        TruncateGeneratedTables(conn);

        LoadEntityHeaderFields(conn, entityHeaderCsv);
        LoadEntityDescriptions(conn, entityDescCsv);
        LoadControllerRoutes(conn, controllerRoutesCsv);

        PrintStats(conn);

        Console.WriteLine("Done.");
        return 0;
    }

    private static void CreateSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS entity_header_fields (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    relative_path TEXT,
    fqcn TEXT,
    class_name TEXT,
    property_name TEXT,
    property_type TEXT,
    is_fkey INTEGER,
    is_top_level INTEGER,
    path TEXT,
    generic_arg0_type TEXT,
    generic_arg0_is_enum INTEGER,
    field_type TEXT,
    picker_url TEXT,
    picker_url_norm TEXT,
    key_fqcn_prop TEXT UNIQUE,
    status TEXT
);

CREATE TABLE IF NOT EXISTS entity_descriptions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    relative_path TEXT,
    fqcn TEXT,
    class_name TEXT,
    inherits_summary_data INTEGER,
    inherits_entity_base INTEGER,
    get_url TEXT, get_url_norm TEXT,
    get_list_url TEXT, get_list_url_norm TEXT,
    factory_url TEXT, factory_url_norm TEXT,
    save_url TEXT, save_url_norm TEXT,
    delete_url TEXT, delete_url_norm TEXT,
    preview_ui_url TEXT, preview_ui_url_norm TEXT,
    list_ui_url TEXT, list_ui_url_norm TEXT,
    edit_ui_url TEXT, edit_ui_url_norm TEXT,
    create_ui_url TEXT, create_ui_url_norm TEXT,
    icon TEXT,
    cluster_key TEXT,
    key_fqcn TEXT UNIQUE
);

CREATE TABLE IF NOT EXISTS controller_routes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    relative_path TEXT,
    fqcn TEXT,
    class_name TEXT,
    method_name TEXT,
    http_verb TEXT,
    class_route TEXT,
    method_route TEXT,
    full_route TEXT,
    full_route_norm TEXT,
    return_type TEXT,
    is_async_task INTEGER,
    has_list_request_param INTEGER,
    has_from_body_param INTEGER,
    from_body_type TEXT,
    from_body_type_name TEXT,
    key_route TEXT UNIQUE
);

-- This table is your “master decisions” layer. Never truncate it.
CREATE TABLE IF NOT EXISTS resolution_overrides (
    key TEXT PRIMARY KEY,
    ignore INTEGER DEFAULT 0,
    forced_controller_route_norm TEXT NULL,
    forced_entity_fqcn TEXT NULL,
    notes TEXT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ehf_picker_norm ON entity_header_fields(picker_url_norm);
CREATE INDEX IF NOT EXISTS ix_cr_route_norm ON controller_routes(full_route_norm);
CREATE INDEX IF NOT EXISTS ix_ed_save_norm ON entity_descriptions(save_url_norm);
CREATE INDEX IF NOT EXISTS ix_ed_factory_norm ON entity_descriptions(factory_url_norm);
CREATE INDEX IF NOT EXISTS ix_ed_getlist_norm ON entity_descriptions(get_list_url_norm);
";
        cmd.ExecuteNonQuery();
    }

    private static void TruncateGeneratedTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DELETE FROM entity_header_fields;
DELETE FROM entity_descriptions;
DELETE FROM controller_routes;
";
        cmd.ExecuteNonQuery();
    }

    private static void LoadEntityHeaderFields(SqliteConnection conn, string csvPath)
    {
        Console.WriteLine($"Loading : {Path.GetFileName(csvPath)}");

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO entity_header_fields(
  relative_path,fqcn,class_name,property_name,property_type,is_fkey,is_top_level, path,  generic_arg0_type,generic_arg0_is_enum,field_type,picker_url,picker_url_norm,key_fqcn_prop, status
) VALUES (
  $relative_path,$fqcn,$class_name,$property_name,$property_type,$is_fkey, is_top_level, path, $generic_arg0_type,$generic_arg0_is_enum,$field_type,$picker_url,$picker_url_norm,$key_fqcn_prop, $status
);";

        var rows = ReadCsv(csvPath).ToList();
        if (rows.Count == 0) { tx.Commit(); return; }

        foreach (var r in rows)
        {
            // Header: RelativePath,FullyQualifiedClassName,ClassName,PropertyName,PropertyType,GenericArg0Type,GenericArg0IsEnum,FieldType,EntityHeaderPickerUrl
            var relativePath = r.Get("RelativePath");
            var fqcn = r.Get("FullyQualifiedClassName");
            var className = r.Get("ClassName");
            var propName = r.Get("PropertyName");
            var isFKey = r.Get("IsFkey") == "Y";
            var isTopLevel = r.Get("IsTopLevel") == "Y";
            var path = r.Get("Path");
            var propType = r.Get("PropertyType");
            var genArg0 = r.Get("GenericArg0Type");
            var genArg0IsEnum = ParseBoolish(r.Get("GenericArg0IsEnum"));
            var fieldType = r.Get("FieldType");
            var pickerUrl = r.Get("EntityHeaderPickerUrl");
            var pickerNorm = NormalizeRoute(pickerUrl);
            var status = r.Get("Status");

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$relative_path", (object?)relativePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$fqcn", (object?)fqcn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$class_name", (object?)className ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$property_name", (object?)propName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$is_fkey", (object?)isFKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$is_top_level", (object?)isTopLevel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$path", (object?)path ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$property_type", (object?)propType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$generic_arg0_type", (object?)genArg0 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$generic_arg0_is_enum", genArg0IsEnum.HasValue ? (genArg0IsEnum.Value ? 1 : 0) : DBNull.Value);
            cmd.Parameters.AddWithValue("$field_type", (object?)fieldType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$picker_url", (object?)pickerUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$picker_url_norm", (object?)pickerNorm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$key_fqcn_prop", $"{fqcn}|{propName}");
            cmd.Parameters.AddWithValue("$status", $"{status}");

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void LoadEntityDescriptions(SqliteConnection conn, string csvPath)
    {
        Console.WriteLine($"Loading : {Path.GetFileName(csvPath)}");

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
INSERT INTO entity_descriptions(
  relative_path,fqcn,class_name, inherits_entity_base, inherits_summary_data,
  get_url,get_url_norm,
  get_list_url,get_list_url_norm,
  factory_url,factory_url_norm,
  save_url,save_url_norm,
  delete_url,delete_url_norm,
  preview_ui_url,preview_ui_url_norm,
  list_ui_url,list_ui_url_norm,
  edit_ui_url,edit_ui_url_norm,
  create_ui_url,create_ui_url_norm,
  icon,cluster_key,
  key_fqcn
) VALUES (
  $relative_path,$fqcn,$class_name, $inherits_entity_base, $inherits_summary_data,
  $get_url,$get_url_norm,
  $get_list_url,$get_list_url_norm,
  $factory_url,$factory_url_norm,
  $save_url,$save_url_norm,
  $delete_url,$delete_url_norm,
  $preview_ui_url,$preview_ui_url_norm,
  $list_ui_url,$list_ui_url_norm,
  $edit_ui_url,$edit_ui_url_norm,
  $create_ui_url,$create_ui_url_norm,
  $icon,$cluster_key,
  $key_fqcn
);";

        foreach (var r in ReadCsv(csvPath))
        {
            var relativePath = r.Get("RelativePath");
            var fqcn = r.Get("FullyQualifiedClassName");
            var className = r.Get("ClassName");
            var inherits = ParseBoolish(r.Get("InheritsSummaryData")) ?? false;
            var inheritEntityBases = ParseBoolish(r.Get("inheritsEntityBase")) ?? false;

            var getUrl = r.Get("GetUrl");
            var getListUrl = r.Get("GetListUrl");
            var factoryUrl = r.Get("FactoryUrl");
            var saveUrl = r.Get("SaveUrl");
            var deleteUrl = r.Get("DeleteUrl");

            var previewUi = r.Get("PreviewUIUrl");
            var listUi = r.Get("ListUIUrl");
            var editUi = r.Get("EditUIUrl");
            var createUi = r.Get("CreateUIUrl");

            var icon = r.Get("Icon");
            var clusterKey = r.Get("ClusterKey");

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$relative_path", (object?)relativePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$fqcn", (object?)fqcn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$class_name", (object?)className ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$inherits_summary_data", inherits ? 1 : 0);
            cmd.Parameters.AddWithValue("$inherits_entity_base", inheritEntityBases ? 1 : 0);

            cmd.Parameters.AddWithValue("$get_url", (object?)getUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$get_url_norm", (object?)NormalizeRoute(getUrl) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$get_list_url", (object?)getListUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$get_list_url_norm", (object?)NormalizeRoute(getListUrl) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$factory_url", (object?)factoryUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$factory_url_norm", (object?)NormalizeRoute(factoryUrl) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$save_url", (object?)saveUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$save_url_norm", (object?)NormalizeRoute(saveUrl) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$delete_url", (object?)deleteUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$delete_url_norm", (object?)NormalizeRoute(deleteUrl) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$preview_ui_url", (object?)previewUi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$preview_ui_url_norm", (object?)NormalizeRoute(previewUi) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$list_ui_url", (object?)listUi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$list_ui_url_norm", (object?)NormalizeRoute(listUi) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$edit_ui_url", (object?)editUi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$edit_ui_url_norm", (object?)NormalizeRoute(editUi) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$create_ui_url", (object?)createUi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$create_ui_url_norm", (object?)NormalizeRoute(createUi) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$icon", (object?)icon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cluster_key", (object?)clusterKey ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$key_fqcn", (object?)fqcn ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void LoadControllerRoutes(SqliteConnection conn, string csvPath)
    {
        Console.WriteLine($"Loading : {Path.GetFileName(csvPath)}");

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
INSERT INTO controller_routes(
  relative_path,fqcn,class_name,method_name,http_verb,
  class_route,method_route,full_route,full_route_norm,
  return_type,is_async_task,has_list_request_param,
  has_from_body_param,from_body_type,from_body_type_name,
  key_route
) VALUES (
  $relative_path,$fqcn,$class_name,$method_name,$http_verb,
  $class_route,$method_route,$full_route,$full_route_norm,
  $return_type,$is_async_task,$has_list_request_param,
  $has_from_body_param,$from_body_type,$from_body_type_name,
  $key_route
);";

        foreach (var r in ReadCsv(csvPath))
        {
            var rel = r.Get("RelativePath");
            var fqcn = r.Get("FullyQualifiedClassName");
            var className = r.Get("ClassName");
            var methodName = r.Get("MethodName");
            var verb = r.Get("HttpVerb");

            var classRoute = r.Get("ClassRoute");
            var methodRoute = r.Get("MethodRoute");
            var fullRoute = r.Get("FullRoute");
            var fullNorm = r.Get("FullRouteNormalized");

            var returnType = r.Get("ReturnType");
            var isAsyncTask = ParseBoolish(r.Get("IsAsyncTask")) ?? false;
            var hasListReq = ParseBoolish(r.Get("HasListRequestParam")) ?? false;

            var hasFromBody = ParseBoolish(r.Get("HasFromBodyParam")) ?? false;
            var fromBodyType = r.Get("FromBodyType");
            var fromBodyTypeName = r.Get("FromBodyTypeName");

            // Optional but nice to keep around
            var hasFromServices = ParseBoolish(r.Get("HasFromServicesParam")) ?? false;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$relative_path", (object?)rel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$fqcn", (object?)fqcn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$class_name", (object?)className ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$method_name", (object?)methodName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$http_verb", (object?)verb ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$class_route", (object?)classRoute ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$method_route", (object?)methodRoute ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$full_route", (object?)fullRoute ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$full_route_norm", (object?)fullNorm ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$return_type", (object?)returnType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$is_async_task", isAsyncTask ? 1 : 0);
            cmd.Parameters.AddWithValue("$has_list_request_param", hasListReq ? 1 : 0);

            cmd.Parameters.AddWithValue("$has_from_body_param", hasFromBody ? 1 : 0);
            cmd.Parameters.AddWithValue("$from_body_type", (object?)fromBodyType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$from_body_type_name", (object?)fromBodyTypeName ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$key_route", $"{verb}|{fullNorm}|{fqcn}|{methodName}");

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void PrintStats(SqliteConnection conn)
    {
        static long Scalar(SqliteConnection c, string sql)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            return (long)(cmd.ExecuteScalar() ?? 0L);
        }

        var ehf = Scalar(conn, "SELECT COUNT(*) FROM entity_header_fields;");
        var ed = Scalar(conn, "SELECT COUNT(*) FROM entity_descriptions;");
        var cr = Scalar(conn, "SELECT COUNT(*) FROM controller_routes;");

        var pickerWithUrl = Scalar(conn, "SELECT COUNT(*) FROM entity_header_fields WHERE picker_url_norm IS NOT NULL AND picker_url_norm <> '';");
        var pickerMatched = Scalar(conn, @"
SELECT COUNT(*)
FROM entity_header_fields e
JOIN controller_routes c
  ON e.picker_url_norm = c.full_route_norm
WHERE e.picker_url_norm IS NOT NULL AND e.picker_url_norm <> '';
");

        Console.WriteLine($"Rows    : entity_header_fields={ehf:n0}, entity_descriptions={ed:n0}, controller_routes={cr:n0}");
        Console.WriteLine($"Pickers : with_url={pickerWithUrl:n0}, matched_to_route={pickerMatched:n0}, unmatched={(pickerWithUrl - pickerMatched):n0}");
    }

    // ---------------- CSV reader (simple, handles quoted commas) ----------------

    private sealed class CsvRow
    {
        private readonly Dictionary<string, string?> _map;
        public CsvRow(Dictionary<string, string?> map) => _map = map;
        public string? Get(string col) => _map.TryGetValue(col, out var v) ? v : null;
    }

    private static IEnumerable<CsvRow> ReadCsv(string path)
    {
        using var sr = new StreamReader(File.OpenRead(path));
        var headerLine = sr.ReadLine();
        if (headerLine == null) yield break;

        var headers = ParseCsvLine(headerLine).ToArray();

        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line).ToArray();

            var map = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Length; i++)
            {
                var val = i < cols.Length ? cols[i] : null;
                map[headers[i]] = string.IsNullOrWhiteSpace(val) ? null : val;
            }

            yield return new CsvRow(map);
        }
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        // Minimal CSV parser: handles quotes and escaped quotes.
        var cur = "";
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cur += ch;
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    yield return cur;
                    cur = "";
                }
                else
                {
                    cur += ch;
                }
            }
        }

        yield return cur;
    }

    private static bool? ParseBoolish(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (s == "1") return true;
        if (s == "0") return false;

        return null;
    }

    private static string? NormalizeRoute(string? route)
    {
        // Same normalization rules as your ScanCommon.NormalizeRoute; duplicated here to keep loader standalone.
        if (string.IsNullOrWhiteSpace(route)) return null;

        var r = route.Trim();
        var q = r.IndexOf('?');
        if (q >= 0) r = r.Substring(0, q);

        r = r.Replace('\\', '/');
        while (r.Contains("//", StringComparison.Ordinal))
            r = r.Replace("//", "/", StringComparison.Ordinal);

        r = System.Text.RegularExpressions.Regex.Replace(r, @"\{[^}]+\}", "{x}");

        if (r.Length > 1 && r.EndsWith("/", StringComparison.Ordinal))
            r = r.TrimEnd('/');

        if (!r.StartsWith("/", StringComparison.Ordinal) && r.Contains('/', StringComparison.Ordinal))
            r = "/" + r;

        return r.ToLowerInvariant();
    }
}
