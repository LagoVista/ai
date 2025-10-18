using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using RagCli.Services;
using RagCli.Rag;
using RagCli.Types;

public static class WebHost
{

    public static async Task Start(String[] args) {

        var builder = WebApplication.CreateBuilder(args);

        // Load your existing config (or inline constants)
        var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText("appsettings.json"))!;
        Env.ApplyOverrides(cfg); // if you added env support earlier

        // Qdrant & Embedder from your existing starter
        builder.Services.AddSingleton(new QdrantClient(cfg.Qdrant.Endpoint, cfg.Qdrant.ApiKey));
        IEmbedder embedder = (Env.Get("EMBEDDINGS_PROVIDER", cfg.Embeddings.Provider)?.ToLowerInvariant() == "openai")
            ? new OpenAIEmbedder(
                apiKey: Env.Get("OPENAI_API_KEY", cfg.Embeddings.ApiKey),
                model: Env.Get("OPENAI_EMBEDDINGS_MODEL", cfg.Embeddings.Model),
                baseUrl: Env.Get("OPENAI_BASE_URL", "https://api.openai.com/v1"),
                expectedDims: cfg.Qdrant.VectorSize)
            : new StubEmbedder(cfg.Qdrant.VectorSize);
        builder.Services.AddSingleton(embedder);

        // RAG Answer service
        builder.Services.AddSingleton(sp =>
        {
            var qdrant = sp.GetRequiredService<QdrantClient>();
            var repoRoot = Env.Get("REPO_ROOT", Directory.GetCurrentDirectory());
            var llmBaseUrl = Env.Get("LLM_BASE_URL", "https://api.openai.com/v1");
            var llmApiKey = Env.Get("LLM_API_KEY", Env.Get("OPENAI_API_KEY", cfg.Embeddings.ApiKey));
            return new CodeRagAnswerService(qdrant, embedder, cfg.Qdrant.Collection, repoRoot, llmBaseUrl, llmApiKey);
        });

        var app = builder.Build();

        app.MapGet("/health", () => new { ok = true });

        app.MapPost("/answer", async (CodeRagAnswerService svc, AnswerRequest req) =>
        {
            var result = await svc.AnswerAsync(req.Question, req.Repo, req.Language, req.TopK);
            return Results.Ok(result);
        });

        app.Run();

public record AnswerRequest(string Question, string? Repo, string? Language = "csharp", int TopK = 8);
}
}