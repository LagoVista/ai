using System.Text.Json;
using RagCli.Services;
using RagCli.Types;

var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText("appsettings.json"))!;
var qdrant = new QdrantClient(cfg.Qdrant.Endpoint, cfg.Qdrant.ApiKey);
var collectionName = cfg.Qdrant.Collection;

// 1) Ensure collection exists
await qdrant.EnsureCollectionAsync(new QdrantCollectionConfig
{
    Name = collectionName,
    VectorSize = cfg.Qdrant.VectorSize,
    Distance = cfg.Qdrant.Distance
});

// 2) Ingest & index
var chunker = new SimpleChunker();
IEmbedder embedder = new StubEmbedder(cfg.Qdrant.VectorSize); // TODO: replace with a real embedder

foreach (var root in cfg.Ingestion.RootPaths)
{
    foreach (var file in FileWalker.EnumerateFiles(root, cfg.Ingestion.Include, cfg.Ingestion.Exclude))
    {
        var relPath = Path.GetRelativePath(root, file);
        var text = await File.ReadAllTextAsync(file);
        var chunks = chunker.Chunk(text, relPath);

        var points = new List<QdrantPoint>();
        foreach (var ch in chunks)
        {
            var vec = await embedder.EmbedAsync(ch.Text);
            points.Add(new QdrantPoint
            {
                Id = QdrantPoint.NewId(),
                Vector = vec,
                Payload = new Dictionary<string, object?>
                {
                    ["repo"] = Path.GetFileName(root),
                    ["path"] = relPath,
                    ["language"] = LanguageGuesser.FromPath(relPath),
                    ["symbol"] = ch.Symbol,
                    ["start_line"] = ch.StartLine,
                    ["end_line"] = ch.EndLine,
                    ["kind"] = ch.Kind
                }
            });
        }

        if (points.Count > 0)
        {
            Console.WriteLine($"Upserting {points.Count} chunks from {relPath}...");
            await qdrant.UpsertAsync(collectionName, points);
        }
    }
}

Console.WriteLine("Ingestion complete.\n");

// 3) Query loop
while (true)
{
    Console.Write("Query> ");
    var q = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(q)) break;

    var qVec = await embedder.EmbedAsync(q);

    var results = await qdrant.SearchAsync(collectionName, new QdrantSearchRequest
    {
        Vector = qVec,
        Limit = 8,
        WithPayload = true,
        Filter = new QdrantFilter
        {
            Must = new()
            {
                new QdrantCondition { Key = "language", Match = new() { Value = "csharp" } }
            }
        }
    });

    foreach (var r in results)
    {
        var pl = r.Payload!;
        Console.WriteLine($"score={r.Score:F3} {pl["repo"]}/{pl["path"]} [{pl["start_line"]}-{pl["end_line"]}] symbol={pl.GetValueOrDefault("symbol", "-")}");
    }
}
