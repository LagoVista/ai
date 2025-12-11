using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Generates short session names using OpenAI via the existing LLM client.
    /// Uses the AgentContext's default conversation context.
    /// </summary>
    public class OpenAISessionNamingService : IAgentSessionNamingService
    {
        private readonly ITextLlmService _textService;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;


        public OpenAISessionNamingService(ITextLlmService structuredTextLlmService, IAdminLogger adminLogger,IAgentStreamingContext agentStreamingContext)
        {
            _textService = structuredTextLlmService ?? throw new ArgumentNullException(nameof(structuredTextLlmService));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        public async Task<string> GenerateNameAsync(
            AgentContext agentContext,
            string instruction,
            CancellationToken cancellationToken)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (instruction == null) instruction = string.Empty;


            var conversationCtx = agentContext.DefaultConversationContext == null ? agentContext.ConversationContexts.FirstOrDefault() :  agentContext.ConversationContexts.SingleOrDefault(ctx => ctx.Id == agentContext.DefaultConversationContext.Id);
            
            if (conversationCtx == null)
            {
                _adminLogger.AddError(
                    "[OpenAISessionNamingService_GenerateNameAsync]",
                    "AgentContext missing DefaultConversationContext.");

                return TruncateFallback(instruction);
            }

            var systemPrompt = new StringBuilder()
                .AppendLine("You generate short session names.")
                .AppendLine("Rules:")
                .AppendLine("- Max 60 characters.")
                .AppendLine("- No punctuation.")
                .AppendLine("- No quotes.")
                .AppendLine("- Plain simple words.")
                .AppendLine("- Capture the essence of the instruction.")
                .ToString();

            var settings = new OpenAISettings(agentContext.LlmApiKey);

            await _agentStreamingContext.AddWorkflowAsync("Getting a good name for this session...");

            var result = await _textService.ExecuteAsync(settings, systemPrompt, instruction);

            if (!result.Successful || result.Result == null)
            {
                _adminLogger.AddError("[OpenAISessionNamingService_GenerateNameAsync]",$"LLM call failed for naming - {result.ErrorMessage}");

                return TruncateFallback(instruction);
            }

            var text = result.Result.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return TruncateFallback(instruction);
            }

            // Remove punctuation as required
            var cleaned = RemovePunctuation(text).Trim();
            if (cleaned.Length > 60)
            {
                cleaned = cleaned.Substring(0, 60).Trim();
            }

            _adminLogger.AddError("[OpenAISessionNamingService_GenerateNameAsync]", $"Renamed session {instruction} - {result.Result}");

            await _agentStreamingContext.AddWorkflowAsync($"Let's call it {result.Result}...");

            return string.IsNullOrWhiteSpace(cleaned)
                ? TruncateFallback(instruction)
                : cleaned;
        }

        private static string RemovePunctuation(string input)
        {
            var chars = input.ToCharArray();
            var buffer = new System.Text.StringBuilder();

            foreach (var c in chars)
            {
                if (!char.IsPunctuation(c)) buffer.Append(c);
            }

            return buffer.ToString();
        }

        private static string TruncateFallback(string instruction)
        {
            if (string.IsNullOrWhiteSpace(instruction)) return "New Session";
            var cleaned = RemovePunctuation(instruction).Trim();
            return cleaned.Length > 60 ? cleaned.Substring(0, 60).Trim() : cleaned;
        }
    }


    internal class OpenAISettings : IOpenAISettings
    {
        public OpenAISettings(string apiKey, string url = "https://api.openai.com")
        {
            OpenAIUrl = url;
            OpenAIApiKey = apiKey;
        }

        public string OpenAIUrl { get; }

        public string OpenAIApiKey { get; }
    }
}
