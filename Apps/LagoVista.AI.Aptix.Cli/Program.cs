using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.AgentClient;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;

namespace LagoVista.AI.Aptix.Cli
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return -1;
            }

            var config = AptixConfigLoader.Load();
            if (config == null)
            {
                return -1;
            }

            // --- Determine environment (prod/dev/local) ---
            var isLocal = HasFlag(args, "--local");
            var isDev = HasFlag(args, "--dev");

            if (isLocal && isDev)
            {
                Console.WriteLine("Please specify only one of --local or --dev.");
                return -1;
            }

            var baseUrl = isLocal
                ? "https://localhost:5001"
                : (isDev ? "https://dev-api.nuviot.com" : "https://api.nuviot.com");

            // --- ClientId resolution ---
            var inlineClientId = ParseInlineClientId(args);
            var clientId = !string.IsNullOrWhiteSpace(inlineClientId)
                ? inlineClientId
                : config.ClientAppId;

            if (string.IsNullOrWhiteSpace(clientId))
            {
                Console.WriteLine("Client app id is not configured.");
                Console.WriteLine("Specify --clientid or set 'clientAppId' in aptix.config.json.");
                return -1;
            }

            // --- Token resolution (no secrets in config) ---
            var inlineToken = ParseInlineToken(args);
            var envToken = Environment.GetEnvironmentVariable("APTIX_AI_TOKEN");

            string token;
            if (!string.IsNullOrWhiteSpace(inlineToken))
            {
                token = inlineToken;
            }
            else if (!string.IsNullOrWhiteSpace(envToken))
            {
                token = envToken;
            }
            else
            {
                Console.WriteLine("Authorization token not provided.");
                Console.WriteLine("Please supply --token <token> or set APTIX_AI_TOKEN.");
                return -1;
            }

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            // Final Authorization header: APIToken ClientId:Token
            httpClient.DefaultRequestHeaders.Add("Authorization", $"APIToken {clientId}:{token}");

            // Banner / eye candy
            PrintBanner(baseUrl, isLocal, isDev);

            var command = args[0].ToLowerInvariant();
            var verbose = HasFlag(args, "--verbose", "-v");

            switch (command)
            {
                case "ask":
                    return await HandleAskAsync(args, config, httpClient, verbose);

                case "ping":
                    return await HandlePingAsync(httpClient, verbose);

                default:
                    Console.WriteLine($"Unknown command '{command}'.");
                    PrintUsage();
                    return -1;
            }
        }

        private static string ParseInlineToken(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--token", StringComparison.OrdinalIgnoreCase)
                    || args[i].Equals("-t", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }

                    Console.WriteLine("Missing value for --token flag.");
                    return null;
                }
            }

            return null;
        }

        private static string ParseInlineClientId(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--clientid", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }

                    Console.WriteLine("Missing value for --clientid flag.");
                    return null;
                }
            }

            return null;
        }

        private static bool HasFlag(string[] args, string longName, string shortName = null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(longName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(shortName)
                    && args[i].Equals(shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrintBanner(string baseUrl, bool isLocal, bool isDev)
        {
            var env = isLocal ? "LOCAL" : (isDev ? "DEV" : "PROD");

            Console.WriteLine("========================================");
            Console.WriteLine("  Aptix CLI — AI Dev Companion");
            Console.WriteLine($"  Environment : {env}");
            Console.WriteLine($"  Base URL    : {baseUrl}");
            Console.WriteLine("========================================");
            Console.WriteLine();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Aptix CLI");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  aptix ask  [--dev|--local] [--clientid <id>] [--token <token>] [--verbose] \"your question\"");
            Console.WriteLine("  aptix ping [--dev|--local] [--clientid <id>] [--token <token>] [--verbose]");
            Console.WriteLine();
            Console.WriteLine("Environment variables:");
            Console.WriteLine("  APTIX_AI_TOKEN    If set, used as the token.");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  --dev             Use https://dev-api.nuviot.com");
            Console.WriteLine("  --local           Use https://localhost:5001 (Make sure API server, Not Portal is running locally)");
            Console.WriteLine("  --clientid        Override client app id used in Authorization header");
            Console.WriteLine("  --token, -t       Override token used in Authorization header");
            Console.WriteLine("  --verbose, -v     Print raw JSON / raw responses");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  aptix ping --dev");
            Console.WriteLine("  APTIX_AI_TOKEN=Secret123 aptix ask --clientid MyApp \"Show me repo layout.\"");
        }

        private static async Task<int> HandleAskAsync(string[] args, AptixConfig config, HttpClient httpClient, bool verbose)
        {
            // Remove flags and their values from the question
            var cleanedArgs = new List<string>();

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.Equals("--token", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("-t", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("--clientid", StringComparison.OrdinalIgnoreCase))
                {
                    i++; // skip value
                    continue;
                }

                if (arg.Equals("--dev", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("--local", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cleanedArgs.Add(arg);
            }

            if (cleanedArgs.Count == 0)
            {
                Console.WriteLine("Missing question for 'ask'.");
                return -1;
            }

            var question = string.Join(" ", cleanedArgs);

            var agentContext = EntityHeader.Create(config.AgentContextId, config.AgentContextName);
            var role = EntityHeader.Create(config.RoleId, config.RoleName);

            var envelope = new AgentRequestEnvelope
            {
                ClientKind = AgentClientKind.Cli,
                SessionId = null,
                PreviousTurnId = null,
                OperationKind = EntityHeader<OperationKinds>.Create(OperationKinds.Text),
                AgentContext = agentContext,
                Role = role,
                WorkspaceId = config.DefaultWorkspaceId,
                Repo = config.DefaultRepo,
                Language = config.DefaultLanguage,
                Instruction = question,
                ActiveFiles = new List<ActiveFileDescriptor>(),
                RagFilters = BuildRagFilters(config.DefaultRagScope)
            };


            var requestJson = JsonConvert.SerializeObject(envelope);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            HttpResponseMessage responseMessage;
            try
            {
                responseMessage = await httpClient.PostAsync("/api/ai/agent/execute", content, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP error calling agent: {ex.Message}");

                if (verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine(ex.ToString());
                }

                return -1;
            }

            if (!responseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine($"Agent call failed: HTTP {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}");

                if (verbose)
                {
                    var body = await responseMessage.Content.ReadAsStringAsync();
                    Console.WriteLine();
                    Console.WriteLine("Response body:");
                    Console.WriteLine(body);
                }

                return -1;
            }

            InvokeResult<AgentExecutionResponse> invokeResult;

            try
            {
                var json = await responseMessage.Content.ReadAsStringAsync();
                invokeResult = JsonConvert.DeserializeObject<InvokeResult<AgentExecutionResponse>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing agent response: {ex.Message}");

                if (verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine(ex.ToString());
                }

                return -1;
            }

            if (invokeResult == null)
            {
                Console.WriteLine("No response from agent.");
                return -1;
            }

            if (!invokeResult.Successful)
            {
                Console.WriteLine("Agent reported an error.");

                if (invokeResult.Errors != null)
                {
                    foreach (var err in invokeResult.Errors)
                    {
                        Console.WriteLine($"- {err.ErrorCode}: {err.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(invokeResult.ErrorMessage))
                {
                    Console.WriteLine($"Message: {invokeResult.ErrorMessage}");
                }

                return -1;
            }

            var payload = invokeResult.Result;
            if (payload == null)
            {
                Console.WriteLine("Agent returned an empty payload.");
                return -1;
            }

            Console.WriteLine("=== Aptix Answer ===\n");
            Console.WriteLine(string.IsNullOrWhiteSpace(payload.AgentAnswerFullText) ? payload.AgentAnswer : payload.AgentAnswerFullText);

            if (payload.ChunkRefs != null && payload.ChunkRefs.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Sources ===");
                foreach (var chunk in payload.ChunkRefs)
                {
                    Console.WriteLine($"[{chunk.ChunkId}] {chunk.Path}:{chunk.StartLine}-{chunk.EndLine}");
                }
            }

            if (payload.Warnings != null && payload.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Warnings ===");
                foreach (var warning in payload.Warnings)
                {
                    Console.WriteLine($"- {warning}");
                }
            }

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine("=== Raw Response (JSON) ===\n");
                var rawJson = JsonConvert.SerializeObject(invokeResult);
                Console.WriteLine(rawJson);
            }

            await Task.Yield();
            return 0;
        }

        private static Dictionary<string, string> BuildRagFilters(string ragScope)
        {
            var filters = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(ragScope))
            {
                filters["scope"] = ragScope;
            }

            return filters;
        }

        private static async Task<int> HandlePingAsync(HttpClient httpClient, bool verbose)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/ai/agent/ping");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ping failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                    if (verbose)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine();
                        Console.WriteLine("Response body:");
                        Console.WriteLine(body);
                    }

                    return -1;
                }

                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Ping succeeded.");

                if (!string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"Response: {content}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ping error: {ex.Message}");

                if (verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine(ex.ToString());
                }

                return -1;
            }
        }
    }
}
