//using System.Collections.Generic;
//using System.IO;
//using System.Net.Http;
//using System;
//using System.Net.Http.Headers;
//using System.Net.Http.Json;
//using System.Text;
//using System.Text.Json;
//using Logzio.DotNet.Core.Shipping;
//using System.Threading.Tasks;
//using RagCli.Services; // your QdrantClient + IEmbedder live here
//using RagCli.Types;    // your types (if any)

//namespace RagCli.Rag
//{
//    public sealed class CodeRagAnswerService
//    {
//        private readonly QdrantClient _qdrant;
//        private readonly IEmbedder _embedder;
//        private readonly string _collection;
//        private readonly string _repoRoot;
//        private readonly HttpClient _llm;

//        public CodeRagAnswerService(QdrantClient qdrant, IEmbedder embedder, string collection, string repoRoot, string llmBaseUrl, string llmApiKey)
//        {
//            _qdrant = qdrant;
//            _embedder = embedder;
//            _collection = collection;
//            _repoRoot = repoRoot;

//            _llm = new HttpClient { BaseAddress = new Uri(llmBaseUrl) };
//            _llm.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", llmApiKey);
//            _llm.Timeout = TimeSpan.FromSeconds(60);
//        }

//        public async Task<AnswerResult> AnswerAsync(string question, string? repo = null, string? language = "csharp", int topK = 8)
//        {
//            // 1) Embed the user question
//            var qvec = await _embedder.EmbedAsync(question);

//            // 2) Retrieve candidates from Qdrant
//            var filter = new QdrantFilter { Must = new() };
//            if (!string.IsNullOrWhiteSpace(language))
//                filter.Must.Add(new QdrantCondition { Key = "language", Match = new() { Value = language } });
//            if (!string.IsNullOrWhiteSpace(repo))
//                filter.Must.Add(new QdrantCondition { Key = "repo", Match = new() { Value = repo } });

//            var hits = await _qdrant.SearchAsync(_collection, new QdrantSearchRequest
//            {
//                Vector = qvec,
//                Limit = Math.Clamp(topK * 3, 12, 50), // over-retrieve
//                WithPayload = true,
//                Filter = filter
//            });

//            // 3) Pick a diverse, small set for the prompt
//            var selected = SelectDiverse(hits, topK);

//            // 4) Resolve snippet text: prefer payload["text"], else read file slice
//            var snippets = new List<Snippet>();
//            foreach (var (hit, idx) in selected.Select((h, i) => (h, i)))
//            {
//                var p = hit.Payload!;
//                var path = p["path"]?.ToString() ?? "";
//                var start = Convert.ToInt32(p["start_line"]);
//                var end = Convert.ToInt32(p["end_line"]);
//                var tag = $"S{idx + 1}";

//                string? text = p.ContainsKey("text") ? p["text"]?.ToString() : null;
//                if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(path) && end >= start)
//                {
//                    text = ReadLines(Path.Combine(_repoRoot, path), start, end);
//                }

//                if (!string.IsNullOrEmpty(text))
//                {
//                    snippets.Add(new Snippet(tag, path, start, end, text));
//                }
//            }

//            // 5) Build prompt
//            var prompt = PromptBuilder.Build(question, snippets);

//            // 6) Call the LLM (OpenAI-compatible /chat/completions)
//            var model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "gpt-4o-mini";
//            var req = new
//            {
//                model,
//                temperature = 0.1,
//                messages = new[]
//                {
//                    new { role = "system", content = prompt.System },
//                    new { role = "user",   content = prompt.User },
//                    new { role = "assistant", content = prompt.Context }
//                }
//            };

//            var resp = await _llm.PostAsJsonAsync("chat/completions", req);
//            resp.EnsureSuccessStatusCode();
//            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
//            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

//            return new AnswerResult
//            {
//                Text = content.Trim(),
//                Sources = snippets.Select(s => new SourceRef { Tag = s.Tag, Path = s.Path, Start = s.Start, End = s.End }).ToList()
//            };
//        }

//        private static List<QdrantScoredPoint> SelectDiverse(List<QdrantScoredPoint> hits, int topK)
//        {
//            var byKey = new Dictionary<string, QdrantScoredPoint>();
//            foreach (var h in hits.OrderByDescending(h => h.Score))
//            {
//                var path = h.Payload!["path"]?.ToString() ?? "";
//                var sym = h.Payload!.ContainsKey("symbol") ? h.Payload["symbol"]?.ToString() ?? "" : "";
//                var key = $"{path}::{sym}";
//                if (!byKey.ContainsKey(key)) byKey[key] = h;
//                if (byKey.Count >= topK) break;
//            }
//            return byKey.Values.ToList();
//        }

//        private static string ReadLines(string fullPath, int start, int end)
//        {
//            if (!File.Exists(fullPath)) return string.Empty;
//            var all = File.ReadAllLines(fullPath);
//            start = Math.Max(1, start);
//            end = Math.Min(all.Length, end);
//            if (end < start) return string.Empty;

//            var sb = new StringBuilder();
//            for (int i = start - 1; i <= end - 1; i++)
//                sb.AppendLine(all[i]);
//            return sb.ToString();
//        }
//    }

//    public sealed class AnswerResult
//    {
//        public string Text { get; set; } = "";
//        public List<SourceRef> Sources { get; set; } = new();
//    }

//    public sealed class SourceRef
//    {
//        public string Tag { get; set; } = "";
//        public string Path { get; set; } = "";
//        public int Start { get; set; }
//        public int End { get; set; }
//    }

//    public sealed record Snippet(string Tag, string Path, int Start, int End, string Text);
//    }
