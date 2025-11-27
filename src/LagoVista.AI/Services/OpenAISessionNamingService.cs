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
        private readonly ILLMClient _llmClient;
        private readonly IAdminLogger _adminLogger;

        public OpenAISessionNamingService(
            ILLMClient llmClient,
            IAdminLogger adminLogger)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
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

            var result = await _llmClient.GetAnswerAsync(
                agentContext,
                conversationCtx,
                new AgentExecuteRequest()
                {
                    Instruction = instruction,
                },
                "",
                systemPrompt);

            if (!result.Successful || result.Result == null)
            {
                _adminLogger.AddError(
                    "[OpenAISessionNamingService_GenerateNameAsync]",
                    "LLM call failed for naming.");

                return TruncateFallback(instruction);
            }

            var text = result.Result.Text?.Trim();
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
}
