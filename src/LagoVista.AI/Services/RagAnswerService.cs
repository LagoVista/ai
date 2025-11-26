// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: ce4acfcee6a4021f0d967d95c54e97475fe820d9193577afc6f33af02dc79149
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using Newtonsoft.Json;
using static LagoVista.AI.Managers.OpenAIManager;

namespace LagoVista.AI.Services
{
    public sealed class RagAnswerService : IRagAnswerService
    {
        private readonly IOpenAISettings _openAiSettings;
        private IQdrantClient _qdrant;
        private IEmbedder _embedder;
        private HttpClient _llm;
        private readonly ILLMContentRepo _contentRepo;
        private readonly IAgentContextManager _vectorDbManager;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentTurnTranscriptStore _transcriptStore;
        private readonly IOrganizationRepo _orgRepo;

        public RagAnswerService(IAdminLogger adminLogger, IAgentTurnTranscriptStore transcriptStore, IAgentContextManager vectorDbManager, IOrganizationRepo orgRepo, IOpenAISettings openAiSettings, ILLMContentRepo contentRepo)
        {
            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _contentRepo = contentRepo ?? throw new ArgumentNullException(nameof(contentRepo));
            _openAiSettings = openAiSettings ?? throw new ArgumentNullException(nameof(openAiSettings));
            _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
            _vectorDbManager = vectorDbManager ?? throw new ArgumentNullException(nameof(vectorDbManager));
        }

        public Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8)
        {
            return AnswerAsync(vectorDatabaseId, question, null, null, org, user, repo, language, topK);
        }

        public Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, string conversationContextId, string previousResponseId, EntityHeader org, EntityHeader user, string repo, string language, int topK, string ragScope, string workspaceId, List<ActiveFile> activeFiles)
        {
            // Step 2.1: wiring only, ignore ragScope/workspaceId/activeFiles for now
            return AnswerAsync(vectorDatabaseId, question, conversationContextId, previousResponseId, org, user, repo, language, topK);
        }



        public async Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, string conversationContextId, string previousResponseId, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8)
        {
            if (string.IsNullOrEmpty(vectorDatabaseId)) throw new ArgumentNullException(nameof(vectorDatabaseId));

            var vectorDb = await _vectorDbManager.GetAgentContextWithSecretsAsync(vectorDatabaseId, org, user);

            if (vectorDb.ConversationContexts == null || vectorDb.ConversationContexts.Count == 0)
            {
                const string msg = "AgentContext has no ConversationContexts configured.";
                _adminLogger.AddError("[RagAnswerService_AnswerAsync__ConversationContext]", msg);
                return InvokeResult<AnswerResult>.FromError(msg);
            }

            ConversationContext conversationContext = null;

            if (!string.IsNullOrWhiteSpace(conversationContextId))
            {
                conversationContext = vectorDb.ConversationContexts.FirstOrDefault(ctx => ctx.Id == conversationContextId);
                if (conversationContext == null)
                {
                    var msg = $"ConversationContext '{conversationContextId}' not found on AgentContext '{vectorDb.Id}'.";
                    _adminLogger.AddError("[RagAnswerService_AnswerAsync__ConversationContext]", msg);
                    return InvokeResult<AnswerResult>.FromError(msg);
                }
            }
            else if (!EntityHeader.IsNullOrEmpty(vectorDb.DefaultConversationContext))
            {
                conversationContext = vectorDb.ConversationContexts.FirstOrDefault(ctx => ctx.Id == vectorDb.DefaultConversationContext.Id);
                if (conversationContext == null)
                {
                    var msg = $"Default ConversationContext '{vectorDb.DefaultConversationContext.Id}' not found on AgentContext '{vectorDb.Id}'.";
                    _adminLogger.AddError("[RagAnswerService_AnswerAsync__ConversationContext]", msg);
                    return InvokeResult<AnswerResult>.FromError(msg);
                }
            }
            else
            {
                // No explicit id and no default set, fall back to first defined context.
                conversationContext = vectorDb.ConversationContexts.First();
                _adminLogger.Trace($"[RagAnswerService_AnswerAsync__ConversationContext] No explicit or default context; using first context '{conversationContext.Id}'.");
            }

            _embedder = new OpenAIEmbedder(vectorDb, _openAiSettings, _adminLogger);
            _qdrant = new QdrantClient(vectorDb, _adminLogger);

            // 1) Embed the user question
            var qvec = await _embedder.EmbedAsync(question, -1);
            _llm = new HttpClient { BaseAddress = new Uri(_openAiSettings.OpenAIUrl) };
            _llm.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vectorDb.LlmApiKey);
            _llm.Timeout = TimeSpan.FromSeconds(60);

            // 2) Retrieve candidates from Qdrant
            var filter = new QdrantFilter { Must = new List<QdrantCondition>() };
            //if (!string.IsNullOrWhiteSpace(language))
            //{
            //    filter.Must.Add(new QdrantCondition { Key = "language", Match = new QdrantMatch { Value = language } });
            //}

            //if (!string.IsNullOrWhiteSpace(repo))
            //{
            //    filter.Must.Add(new QdrantCondition { Key = "repo", Match = new QdrantMatch { Value = repo } });
            //}

            var hits = await _qdrant.SearchAsync(vectorDb.VectorDatabaseCollectionName, new QdrantSearchRequest
            {
                Vector = qvec.Result.Vector,
                Limit = Math.Clamp(topK * 3, 12, 50),
                WithPayload = true,
                Filter = filter
            });

            // 3) Pick a diverse, small set for the prompt
            var selected = SelectDiverse(hits, topK);

            // 4) Resolve snippet text: prefer payload["text"], else read file slice
            var snippets = new List<Snippet>();
            foreach (var pair in selected.Select((h, i) => new { Hit = h, Index = i }))
            {
                var hit = pair.Hit;
                var idx = pair.Index;

                var p = hit.Payload!;
                var path = p["BlobUri"]?.ToString() ?? string.Empty;
                var start = -1;// Convert.ToInt32(String.IsNullOrEmpty(p["LineStart"].ToString()) ? p["LineStart"] : -1);
                var end = -1;// Convert.ToInt32(String.IsNullOrEmpty(p["LineEnd"].ToString()) ? p["LineEnd"] : -1);
                var symbolType = Convert.ToString(p["SymbolType"]);
                var symbol = Convert.ToString(p["Symbol"]);
                var title = p["Title"].ToString();

                var tag = $"S{idx + 1}";

                var text = String.Empty;
               
                var response = await GetContentAsync(vectorDb, path, start, end, org, user);
                if (response.Successful)
                {
                    text = response.Result;
                    if (!string.IsNullOrEmpty(text))
                    {
                        snippets.Add(new Snippet(tag, path, title, start, end, text, symbol, symbolType));
                    }
                }
                else
                    Console.WriteLine($"ERROR retrieving content for {tag}: {response.ErrorMessage}");
            }

            // 5) Build prompt
            var prompt = PromptBuilder.Build(question, snippets);

            // 6) Call the LLM (OpenAI-compatible /chat/completions)
            var model = conversationContext.ModelName;

            var req = new
            {
                model,
                store = true,
                previous_response_id = previousResponseId,
                temperature = conversationContext.Temperature,
                // The Responses API uses "input" instead of "messages".
                // We can still send a multi-message conversation structure.
                input = new[]
                {
                    new { role = "system",    content = conversationContext.System },
                    new { role = "user",      content = prompt.User },
                    new { role = "assistant", content = prompt.Context }
                }
            };

            var sw = Stopwatch.StartNew();

            // NOTE: keep BaseAddress as-is; just change the endpoint path.
            var resp = await _llm.PostAsJsonAsync("/v1/responses", req);
            var body = await resp.Content.ReadAsStringAsync();

            
            if (!resp.IsSuccessStatusCode)
            {
                var errorMessage = $"LLM call failed with HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase}).";

                try
                {
                    var errorWrapper = JsonConvert.DeserializeObject<OpenAIErrorResponse>(body);
                    if (errorWrapper?.Error != null && !string.IsNullOrWhiteSpace(errorWrapper.Error.Message))
                    {
                        errorMessage = errorWrapper.Error.Message;
                    }
                }
                catch
                {
                    // ignore JSON parse errors
                }

                _adminLogger.AddError("[RagAnswerService_AnswerAsync__LLM]", "LLM request failed.", ((int)resp.StatusCode).ToString().ToKVP("statusCode"));
                _adminLogger.AddError("[RagAnswerService_AnswerAsync__LLM]", $"LLM response body: {body}");

                return InvokeResult<AnswerResult>.FromError(errorMessage);
            }

            // Parse Responses API result
            var responsesResult = JsonConvert.DeserializeObject<ResponsesApiResponse>(body);
            if (responsesResult == null)
            {
                _adminLogger.AddError("[RagAnswerService_AnswerAsync__LLM]", "LLM response was null or could not be deserialized.");
                return InvokeResult<AnswerResult>.FromError("LLM response was null or could not be deserialized.");
            }

            _adminLogger.Trace($"[RagAnswerService_AnswerAsync__LLM] Prompt returned in {sw.Elapsed.TotalMilliseconds}ms; Response Id: {responsesResult.Id}");
           

            // Prefer the convenience field if present.
            string content = responsesResult.OutputText;

            foreach(var cnt in responsesResult.Output)
            {
                if (cnt.Content != null)
                {
                    foreach (var c in cnt.Content)
                    {
                        if (!string.IsNullOrWhiteSpace(c.Text))
                        {
                            Console.WriteLine($"TYPE {c.Type}\r\n\t{c.Text}");
                            Console.WriteLine("--");
                        }
                    }
                }
            }

            // Fallback: read from output[0].content[0].text
            if (string.IsNullOrWhiteSpace(content) && responsesResult.Output != null && responsesResult.Output.Any())
            {
                var msg = responsesResult.Output.FirstOrDefault(o => o.Type == "message");
                if(msg != null)
                {
                    var firstContent = msg.Content.FirstOrDefault(o=>o.Type == "output_text");
                    content = firstContent?.Text;
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _adminLogger.AddError("[RagAnswerService_AnswerAsync__LLM]", "LLM response did not contain any output text.");
                return InvokeResult<AnswerResult>.FromError("LLM response did not contain any output text.");
            }

            return InvokeResult<AnswerResult>.Create(new AnswerResult
            {
                Text = content.Trim(),
                OpenAiResponeId = responsesResult.Id,
                Sources = snippets
                    .Select(s => new SourceRef
                    {
                        Tag = s.Tag,
                        Path = s.Path,
                        FileName = s.Title,
                        Start = s.Start,
                        End = s.End,
                        Excerpt = s.Text,
                        Symbol = s.Symbol,
                        SymbolType = s.SymbolType
                    })
                    .ToList()
            });
        }

        private static List<QdrantScoredPoint> SelectDiverse(List<QdrantScoredPoint> hits, int topK)
        {
            var byKey = new Dictionary<string, QdrantScoredPoint>();

            foreach (var h in hits.OrderByDescending(h => h.Score))
            {
                var path = h.Payload!["Path"]?.ToString() ?? h.Payload!["BlobUri"]?.ToString() ?? string.Empty;
                var sym = h.Payload!.ContainsKey("Symbol") ? h.Payload["Symbol"]?.ToString() ?? string.Empty : string.Empty;
                var key = $"{path}::{sym}";
                if (!byKey.ContainsKey(key))
                {
                    byKey[key] = h;
                }

                if (byKey.Count >= topK)
                {
                    break;
                }
            }

            return byKey.Values.ToList();
        }

        public async Task<InvokeResult<string>> GetContentAsync(AgentContext vectorDb, string path, int start, int end, EntityHeader org, EntityHeader user)
        {
            var response = await _contentRepo.GetTextContentAsync(vectorDb, path);
            if (!response.Successful)
            {
                return InvokeResult<string>.FromError(response.ErrorMessage);
            }

            return response;
            /*
            var text = response.Result.Split("\n");
            start = Math.Max(1, start);
            end = Math.Min(text.Length, end);
            if (end < start)
            {
                return InvokeResult<string>.FromError("End is less than start");
            }

            var sb = new StringBuilder();
            for (var i = start - 1; i <= end - 1; i++)
            {
                sb.AppendLine(text[i]);
            }

            return InvokeResult<string>.Create(sb.ToString());*/
        }

        public async Task<InvokeResult<string>> GetContentAsync(string vectorDbId, string path, int start, int end, EntityHeader org, EntityHeader user)
        {
            var vectorDb = await _vectorDbManager.GetAgentContextAsync(vectorDbId, org, user);
            return await GetContentAsync(vectorDb, path, start, end, org, user);
        }

        public async Task<InvokeResult<string>> GetContentAsync(string path, int start, int end, EntityHeader org, EntityHeader user)
        {
            var orgInfo = await _orgRepo.GetOrganizationAsync(org.Id);
            if (EntityHeader.IsNullOrEmpty(orgInfo.DefaultVectorDatabase))
            {
                return InvokeResult<string>.FromError("Organization does not have a default vector database configured.");
            }

            return await GetContentAsync(orgInfo.DefaultVectorDatabase.Id, path, start, end, org, user);
        }

        public async Task<InvokeResult<AnswerResult>> AnswerAsync(string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8)
        {
            var orgInfo = await _orgRepo.GetOrganizationAsync(org.Id);
            if (EntityHeader.IsNullOrEmpty(orgInfo.DefaultVectorDatabase))
            {
                return InvokeResult<AnswerResult>.FromError("Organization does not have a default vector database configured.");
            }

            return await AnswerAsync(orgInfo.DefaultVectorDatabase.Id, question, org, user, repo, language, topK);
        }
    }

    public sealed class OpenAIErrorResponse
    {
        [JsonProperty("error")]
        public OpenAIError Error { get; set; }
    }

    public sealed class OpenAIError
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }

   

}
