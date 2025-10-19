using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using System.Linq;
using static LagoVista.AI.Managers.OpenAIManager;
using LagoVista.Core.Validation;
using RingCentral;    // your types (if any)

namespace LagoVista.AI.Services
{
    public sealed class CodeRagAnswerService : ICodeRagAnswerService
    {
        private readonly IQdrantClient _qdrant;
        private readonly IEmbedder _embedder;
        private readonly string _collection;
        private readonly HttpClient _llm;
        private readonly string _orgId = "AA2C78499D0140A5A9CE4B7581EF9691";
        private readonly ILLMContentRepo _contentRepo;

        public CodeRagAnswerService(IOpenAISettings openAiSettings, IQdrantClient qdrant, IEmbedder embedder, ILLMContentRepo contentRepo )
        {
            _qdrant = qdrant;
            _embedder = embedder;
            _collection = "slappcontent";
            _contentRepo = contentRepo ?? throw new ArgumentNullException(nameof(contentRepo));

            _llm = new HttpClient { BaseAddress = new Uri(openAiSettings.OpenAIUrl) };
            _llm.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiSettings.OpenAIApiKey);
            _llm.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<InvokeResult<AnswerResult>> AnswerAsync(string question, string repo = null, string language = "csharp", int topK = 8)
        {
            // 1) Embed the user question
            var qvec = await _embedder.EmbedAsync(question);

            // 2) Retrieve candidates from Qdrant
            var filter = new QdrantFilter { Must = new List<QdrantCondition>() };
            if (!string.IsNullOrWhiteSpace(language))
                filter.Must.Add(new QdrantCondition { Key = "language", Match = new QdrantMatch() { Value = language } });
            if (!string.IsNullOrWhiteSpace(repo))
                filter.Must.Add(new QdrantCondition { Key = "repo", Match = new QdrantMatch() { Value = repo } });

            var hits = await _qdrant.SearchAsync(_collection, new QdrantSearchRequest
            {
                Vector = qvec,
                Limit = Math.Clamp(topK * 3, 12, 50), // over-retrieve
                WithPayload = true,
                Filter = filter
            });

            Console.WriteLine("Found " + hits.Count + " candidate snippets.");

            // 3) Pick a diverse, small set for the prompt
            var selected = SelectDiverse(hits, topK);

           // 4) Resolve snippet text: prefer payload["text"], else read file slice
            var snippets = new List<Snippet>();
            foreach (var (hit, idx) in selected.Select((h, i) => (h, i)))
            {
                var p = hit.Payload!;
                var path = p["path"]?.ToString() ?? "";
                var start = Convert.ToInt32(p["start_line"]);
                var end = Convert.ToInt32(p["end_line"]);
                var fileName = Convert.ToString(p["fileName"]);
                var symbolType = Convert.ToString(p["kind"]);
                var symbol = Convert.ToString(p["symbol"]);

                var tag = $"S{idx + 1}";

                string text = p.ContainsKey("text") ? p["text"].ToString() : null;
                if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(path) && end >= start)
                {
                    var response = await GetContentAsync(path, fileName, start, end);
                    if (response.Successful)
                        text = response.Result;
                }

                Console.WriteLine($"MATCH: {tag}: {path} [{start}-{end}] (score={hit.Score:F3})");

                if (!string.IsNullOrEmpty(text))
                {
                    snippets.Add(new Snippet(tag, path, fileName, start, end, text, symbol, symbolType));
                }
            }

            // 5) Build prompt
            var prompt = PromptBuilder.Build(question, snippets);

            // 6) Call the LLM (OpenAI-compatible /chat/completions)
            var model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "gpt-4o-mini";
            var req = new
            {
                model,
                temperature = 0.1,
                messages = new[]
                {
                    new { role = "system", content = prompt.System },
                    new { role = "user",   content = prompt.User },
                    new { role = "assistant", content = prompt.Context }
                }
            };

            var resp = await _llm.PostAsJsonAsync("/v1/chat/completions", req);
            resp.EnsureSuccessStatusCode();
            var oaiResponse = await resp.Content.ReadAsAsync<OpenAIResponse>();
            var content = oaiResponse.choices.First().message.content ?? "";

            return InvokeResult<AnswerResult>.Create(new AnswerResult
            {
                Text = content.Trim(),
                Sources = snippets.Select(s => new SourceRef { Tag = s.Tag, Path = s.Path, FileName=s.FileName, Start = s.Start, End = s.End, Excerpt = s.Text, Symbol = s.Symbol, SymbolType = s.SymbolType }).ToList()
            });
        }

        private static List<QdrantScoredPoint> SelectDiverse(List<QdrantScoredPoint> hits, int topK)
        {
            var byKey = new Dictionary<string, QdrantScoredPoint>();
            foreach (var h in hits.OrderByDescending(h => h.Score))
            {
                var path = h.Payload!["path"]?.ToString() ?? "";
                var sym = h.Payload!.ContainsKey("symbol") ? h.Payload["symbol"]?.ToString() ?? "" : "";
                var key = $"{path}::{sym}";
                if (!byKey.ContainsKey(key)) byKey[key] = h;
                if (byKey.Count >= topK) break;
            }
            return byKey.Values.ToList();
        }

        public async Task<InvokeResult<string>> GetContentAsync(string path, string fileName, int start, int end)
        {
            var response = await _contentRepo.GetTextContentAsync(_orgId, path, fileName);
            if(!response.Successful)
            {
                return InvokeResult<string>.FromError(response.ErrorMessage);
            }

            var text = response.Result.Split("\n");
            start = Math.Max(1, start);
            end = Math.Min(text.Length, end);
            if (end < start) return InvokeResult<string>.FromError("End is less than start");

            var sb = new StringBuilder();
            for (int i = start - 1; i <= end - 1; i++)
                sb.AppendLine(text[i]);

            return InvokeResult<string>.Create(sb.ToString());
        }
    }

    public sealed class AnswerResult
    {
        public string Text { get; set; } = "";
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }

    public sealed class SourceRef
    {
        public string Tag { get; set; } = "";
        public string Path { get; set; } = "";
        public string FileName { get; set; } = "";
        public int Start { get; set; }
        public int End { get; set; }
        public string Excerpt { get; set; }
        public string Symbol { get; set; }
        public string SymbolType { get; set; }
    }

    public sealed class Snippet
    {
        public Snippet(string tag, string path, string fileName, int start, int end, string text, string symbol, string symbolType)
        {
            Tag = tag;
            Path = path;
            FileName = fileName;
            Start = start;
            End = end;
            Text = text;
            Symbol = symbol;
            SymbolType = symbolType;
        }

        public string Tag { get; }
        public string Path { get; }
        public string FileName { get; }
        public int Start { get; }
        public int End { get; }
        public string Text { get; }
        public string Symbol { get; }
        public string SymbolType { get; }

    }


    public static class PromptBuilder
    {
        public static ChatPrompt Build(string question, List<Snippet> snippets, int maxContextTokens = 6000)
        {
            var sb = new StringBuilder();
            int budget = 0;

            foreach (var s in snippets)
            {
                var est = TokenEstimator.EstimateTokens(s.Text);
                if (budget + est > maxContextTokens) break;
                budget += est;

                sb.AppendLine($"[{s.Tag}] {s.Path}:{s.Start}-{s.End}");
                sb.AppendLine("```");
                sb.AppendLine(s.Text.TrimEnd());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            var system =
@"You are a senior software engineer assistant.
Use only the provided context when applicable.
If the answer is not in the context, say so.
Always cite sources using [S#] tags.  
Format the output as html that can be included
in another web page, only include the content 
within the body tag, do not include the HTML or header.
The color of the text should look good on a dark background 
and the syntax of the code should be highlighted appropriately
for the language";

            var user = question;
            var context = "Context snippets (cite with [S#]):\n\n" + sb.ToString();

            return new ChatPrompt(system, user, context);
        }
    }

    
    public class ChatPrompt
    {
        public ChatPrompt(string system, string user, string context)
        {
            System = system;
            User = user;
            Context = context;
        }

        public string System { get;  }
        public string User { get; }
        public string Context { get; }
    }
 
    /// <summary>
    /// Rough token estimator for OpenAI embeddings.
    /// OpenAI tokens are roughly ~4 UTF-8 bytes on average for English and code.
    /// This provides a quick, conservative estimate to prevent oversize embedding requests.
    /// </summary>
    public static class TokenEstimator
    {
        public static int EstimateTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;

            // bytes/4 heuristic for rough estimate
            int bytes = System.Text.Encoding.UTF8.GetByteCount(s);
            int approx = bytes / 4;

            // ensure at least #words count
            int words = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            approx = Math.Max(approx, words);

            // add small overhead for punctuation/newlines
            return approx + 2;
        }
    }
}
