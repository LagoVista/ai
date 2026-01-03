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

           // if (isContinuation && !ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any())
            if(isContinuation || ctx.ThisTurn.Iterations.Any())
            {
                var lastIteration = ctx.ThisTurn.Iterations.LastOrDefault();
                var previousResponseId = lastIteration == null ? ctx.ThisTurn.PreviousOpenAIResponseId : lastIteration.OpenAiResponseId;

                if(String.IsNullOrEmpty(previousResponseId))
                    return Task.FromResult(InvokeResult<ResponsesApiRequest>.FromError("Cannot continue response, previous response ID is missing."));

                dto.PreviousResponseId = previousResponseId;

                _adminLogger.Trace($"[Builder_Response_Chain] Previous Response Id {previousResponseId}.");
            }
            else
            {
                _adminLogger.Trace($"[Builder_Response_Chain] No Previous Respons Id");
            }

            foreach (var register in ctx.PromptKnowledgeProvider.Registers)
            {
                if (register.Classification == Models.Context.ContextClassification.Session)
                {
                    foreach (var item in register.Items)
                    {
                        systemMessage.Content.Add(new ResponsesMessageContent
                        {
                            Text = item
                        });
                    }
                }

                if (register.Classification == Models.Context.ContextClassification.Consumable)
                {
                    foreach (var item in register.Items)
                    {
                        userMessage.Content.Add(new ResponsesMessageContent
                        {
                            Text = item
                        });
                    }
                }
            }

            ctx.PromptKnowledgeProvider.ActiveTools.Add(ActivateToolsTool.ToolName);
            ctx.PromptKnowledgeProvider.ActiveTools.Add(AgentListModesTool.ToolName);

            var instructionBlock = "[MODE: " + ctx.Session.Mode + "]\n\n[INSTRUCTION]\n" + (ctx.Envelope.Instructions ?? string.Empty);

            userMessage.Content.Add(new ResponsesMessageContent
            {
                Text = instructionBlock
            });
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
                        sb.AppendLine($"--- BEGIN ACTIVE FILE ---");
                        sb.AppendLine($"Relative Path: {file.RelativePath}");
                        sb.AppendLine($"File Name: {file.FileName}");
                        sb.AppendLine($"Language: {file.Language}");
                        sb.AppendLine();
                        sb.AppendLine(file.Contents);
                        sb.AppendLine($"--- END ACTIVE FILE ---");
                        sb.AppendLine();
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
                    dto.Input.Add(new ResponsesFunctionCallOutput
                    {
                        CallId = result.ToolCallId,
                        Output = result.ResultJson, 
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
