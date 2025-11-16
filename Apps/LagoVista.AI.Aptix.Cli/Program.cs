using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.AgentClient;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

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
            var clientId = !String.IsNullOrWhiteSpace(inlineClientId)
                ? inlineClientId
                : config.ClientAppId;

            if (String.IsNullOrWhiteSpace(clientId))
            {
                Console.WriteLine("Client app id is not configured.");
                Console.WriteLine("Specify --clientid or set 'clientAppId' in aptix.config.json.");
                return -1;
            }

            // --- Token resolution (no secrets in config) ---
            var inlineToken = ParseInlineToken(args);
            var envToken = Environment.GetEnvironmentVariable("APTIX_AI_TOKEN");

            string token;
            if (!String.IsNullOrWhiteSpace(inlineToken))
            {
                token = inlineToken;
            }
            else if (!String.IsNullOrWhiteSpace(envToken))
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

            var agentClient = new AgentExecutionClient(httpClient);

            var command = args[0].ToLowerInvariant();
            var verbose = HasFlag(args, "--verbose", "-v");

            switch (command)
            {
                case "ask":
                    return await HandleAskAsync(args, config, agentClient, verbose);

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

                if (!String.IsNullOrWhiteSpace(shortName)
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
            Console.WriteLine(
                "  aptix ask  [--dev|--local] [--clientid <id>] [--token <token>] [--verbose] \"your question\"");
            Console.WriteLine(
                "  aptix ping [--dev|--local] [--clientid <id>] [--token <token>] [--verbose]");
            Console.WriteLine();
            Console.WriteLine("Environment variables:");
            Console.WriteLine("  APTIX_AI_TOKEN    If set, used as the token.");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  --dev             Use https://dev-api.nuviot.com");
            Console.WriteLine(
                "  --local           Use https://localhost:5001 (Make sure API server, Not Portal is running locally)");
            Console.WriteLine("  --clientid        Override client app id used in Authorization header");
            Console.WriteLine("  --token, -t       Override token used in Authorization header");
            Console.WriteLine("  --verbose, -v     Print raw JSON / raw responses");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  aptix ping --dev");
            Console.WriteLine(
                "  APTIX_AI_TOKEN=Secret123 aptix ask --clientid MyApp \"Show me repo layout.\"");
        }

        private static async Task<int> HandleAskAsync(string[] args, AptixConfig config,
            AgentExecutionClient agentClient, bool verbose)
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
            var conversationContext = EntityHeader.Create(config.ConversationContextId,
                config.ConversationContextName);

            var response = await agentClient.AskAsync(
                agentContext,
                conversationContext,
                question,
                workspaceId: config.DefaultWorkspaceId,
                repo: config.DefaultRepo,
                language: config.DefaultLanguage,
                ragScope: config.DefaultRagScope,
                activeFiles: new List<ActiveFile>(),
                cancellationToken: CancellationToken.None);

           
            if (response == null)
            {
                Console.WriteLine("No response from agent.");
                return -1;
            }

            if (String.Equals(response.Kind, "error", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Agent error: {response.ErrorCode} - {response.ErrorMessage}");
                return -1;
            }

            Console.WriteLine("=== Aptix Answer ===\n");
            Console.WriteLine(response.Text);

            if (response.Sources != null && response.Sources.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Sources ===");
                foreach (var src in response.Sources)
                {
                    Console.WriteLine($"[{src.Tag}] {src.Path}:{src.Start}-{src.End} ({src.SymbolType} {src.Symbol})");
                }
            }

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine("=== Raw Response (JSON) ===\n");
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            }

            await Task.Yield();
            return 0;
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

                if (!String.IsNullOrWhiteSpace(content))
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
