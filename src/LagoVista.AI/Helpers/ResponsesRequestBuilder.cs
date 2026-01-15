using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using System.Text;
using System.Linq;
using LagoVista.AI.Interfaces;
using System.Threading.Tasks;
using LagoVista.Core.Validation;
using LagoVista.AI.Services.Tools;
using LagoVista.AI.Managers;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Helper for constructing the request DTO to send to the OpenAI /responses API,
    /// following AGN-003 (Aptix Responses API Request Construction).
    ///
    /// Combines:
    /// - AgentContextRole (model, system/boot prompt, temperature)
    /// - AgentExecuteRequest (mode, instruction, continuation id, tools, tool results, optional SystemPrompt)
    /// - ragContextBlock (pre-formatted [CONTEXT] block per AGN-002)
    /// - toolUsageMetadataBlock (LLM-facing usage metadata for ALL server tools)
    /// </summary>
    public class ResponsesRequestBuilder : IResponsesRequestBuilder
    {
        private readonly IAdminLogger _adminLogger;
        private readonly IServerToolSchemaProvider _toolSchemaProvider;

        public ResponsesRequestBuilder(IAdminLogger adminLogger, IServerToolSchemaProvider toolSchemaProvider)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _toolSchemaProvider = toolSchemaProvider ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        /// <summary>
        /// Build the /responses request object as a strongly-typed DTO.
        /// The caller can serialize this with Json.NET and POST it to the API.
        /// </summary>
        /// <returns>ResponsesApiRequest representing the body for the /responses call.</returns>
        public Task<InvokeResult<ResponsesApiRequest>> BuildAsync(IAgentPipelineContext ctx)
        {

            var dto = new ResponsesApiRequest
            {
                Model = ctx.Role.ModelName,
                Temperature = ctx.Role.Temperature,
                Stream = ctx.Envelope.Stream,
            };

            var systemMessage = new ResponsesInputMessage("system");
            var userMessage = new ResponsesInputMessage("user");

            var isContinuation = !string.IsNullOrWhiteSpace(ctx.ThisTurn.PreviousOpenAIResponseId);
            
            if (isContinuation || ctx.ThisTurn.Iterations.Any())
            {
                var lastIteration = ctx.ThisTurn.Iterations.LastOrDefault();
                var previousResponseId = lastIteration == null ? ctx.ThisTurn.PreviousOpenAIResponseId : lastIteration.OpenAiResponseId;

                if(String.IsNullOrEmpty(previousResponseId))
                    return Task.FromResult(InvokeResult<ResponsesApiRequest>.FromError("Cannot continue response, previous response ID is missing."));

                dto.PreviousResponseId = previousResponseId;

                _adminLogger.Trace($"{this.Tag()} Previous Response Id {previousResponseId}.");
            }
            else
            {
                _adminLogger.Trace($"{this.Tag()} No Previous Respons Id");
            }

            // Ensure NewChapterInitialPrompt (rehydrate capsule) is injected early in system content.
            // Everything else follows existing register order.
            var orderedRegisters = ctx.PromptKnowledgeProvider.Registers
                .OrderByDescending(r => r.Kind == KnowledgeKind.NewChapterInitialPrompt)
                .ToList();

            foreach (var register in orderedRegisters)
            {
                if (register.Classification == Models.Context.ContextClassification.Session)
                {
                    foreach (var item in register.Items)
                    {
                        if (!String.IsNullOrEmpty(item))
                        {
                            systemMessage.Content.Add(new ResponsesMessageContent
                            {
                                Text = item.Replace("\n", "\\n").Replace("\r", "\\r")
                            });
                        }
                    }
                }

                if (register.Classification == Models.Context.ContextClassification.Consumable)
                {
                    foreach (var item in register.Items)
                    {
                        if (!String.IsNullOrEmpty(item))
                        {
                            userMessage.Content.Add(new ResponsesMessageContent
                            {
                                Text = item.Replace("\n", "\\n").Replace("\r", "\\r")
                            });
                        }
                    }
                }
            }

            if (!String.IsNullOrEmpty(ctx.Envelope.OriginalInstructions))
            {
                userMessage.Content.Add(new ResponsesMessageContent
                {
                    Text = ctx.Envelope.OriginalInstructions
                });
            }
            // ---------------------------------------------------------------------

            // IMAGE ATTACHMENTS (from client-side chat composer)
            // ---------------------------------------------------------------------
            if (ctx.Envelope.ClipBoardImages!= null && ctx.Envelope.ClipBoardImages.Any())
            {
                foreach (var img in ctx.Envelope.ClipBoardImages)
                {
                    if (img == null) continue;
                    if (string.IsNullOrWhiteSpace(img.DataBase64)) continue;
                    if (string.IsNullOrWhiteSpace(img.MimeType)) continue;

                    var dataUrl = $"data:{img.MimeType};base64,{img.DataBase64}";

                    userMessage.Content.Add(new ResponsesMessageContent
                    {
                        Type = "input_image",
                        ImageUrl = dataUrl
                    });
                }
            }

            // ---------------------------------------------------------------------
            // ACTIVE FILES (Attach editor contents for reasoning)
            // ---------------------------------------------------------------------
            if (ctx.Envelope.InputArtifacts != null && ctx.Envelope.InputArtifacts.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine("[ACTIVE FILES]");

                foreach (var file in ctx.Envelope.InputArtifacts)
                {
                    if (!String.IsNullOrEmpty(file.Contents))
                    {
                        sb.AppendLine($"<<<BEGINFILE>>>");
                        sb.AppendLine("<<METADATA>>");
                        sb.AppendLine($"@Source: {file.FileSource};");
                        sb.AppendLine($"@Workspace: {file.WorkSpace};");
                        sb.AppendLine($"@Relative Path: {file.RelativePath};");
                        sb.AppendLine($"@File Name: {file.FileName};");
                        sb.AppendLine($"@SHA 256: {file.Sha256Hash};");
                        sb.AppendLine($"@Language: {file.Language};");
                        sb.AppendLine("<<ENDMETADATA>>");
                        sb.Append("<<CONTENT>>");
                        sb.Append(file.Contents);
                        sb.Append($"<<ENDCONTENT>>");
                        sb.AppendLine("<<ENDFILE>>");
                    }
                }

                userMessage.Content.Add(new ResponsesMessageContent
                {
                    Text = sb.ToString()
                });
            }

            foreach(var tool in ctx.PromptKnowledgeProvider.ActiveTools)
            {
                dto.Tools.Add(_toolSchemaProvider.GetToolSchema(tool));    
            }
            
            if (ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any())
            {
           
                var toolResults = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults;
                foreach (var result in toolResults)
                {
                    if (String.IsNullOrEmpty(result.ResultJson) && String.IsNullOrEmpty(result.ErrorMessage))
                        return Task.FromResult(InvokeResult<ResponsesApiRequest>.FromError("Invalid tool Result: Both ResultJson and Error message are both null"));


                    string resultJson;

                    if (!string.IsNullOrWhiteSpace(result.ResultJson))
                    {
                        resultJson = result.ResultJson;
                    }
                    else
                    {
                        var error = result.ErrorMessage?.Trim();

                        bool isJson =
                            !string.IsNullOrEmpty(error) &&
                            (error.StartsWith("{") || error.StartsWith("["));

                        var errorPayload = isJson
                            ? error
                            : JsonConvert.SerializeObject(error ?? "Unknown error");

                        resultJson = $@"{{ ""status"": ""failed"", ""result"": {errorPayload} }}";
                    }


                    dto.Input.Add(new ResponsesFunctionCallOutput
                    {
                        CallId = result.ToolCallId,
                        Output = resultJson, 
                    });
                }
            }

            dto.Input.Add(systemMessage);
            dto.Input.Add(userMessage);

            ctx.PromptKnowledgeProvider.Reset();
            
            return Task.FromResult(InvokeResult<ResponsesApiRequest>.Create(dto));

        }
    }
}
