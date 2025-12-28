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

namespace LagoVista.AI.Helpers
{
    /// <summary>
    /// Helper for constructing the request DTO to send to the OpenAI /responses API,
    /// following AGN-003 (Aptix Responses API Request Construction).
    ///
    /// Combines:
    /// - ConversationContext (model, system/boot prompt, temperature)
    /// - AgentExecuteRequest (mode, instruction, continuation id, tools, tool results, optional SystemPrompt)
    /// - ragContextBlock (pre-formatted [CONTEXT] block per AGN-002)
    /// - toolUsageMetadataBlock (LLM-facing usage metadata for ALL server tools)
    /// </summary>
    public class ResponsesRequestBuilder : IResponsesRequestBuilder
    {
        /// <summary>
        /// Build the /responses request object as a strongly-typed DTO.
        /// The caller can serialize this with Json.NET and POST it to the API.
        /// </summary>
        /// <param name="conversationContext">The reasoning profile / boot prompt for this conversation.</param>
        /// <param name="request">The agent execute request from the client.</param>
        /// <param name="ragContextBlock">The pre-formatted RAG context block (may be null or empty).</param>
        /// <param name="toolUsageMetadataBlock">LLM-facing usage metadata block containing detailed instructions for all Aptix tools (may be null or empty).</param>
        /// <returns>ResponsesApiRequest representing the body for the /responses call.</returns>
        public Task<InvokeResult<ResponsesApiRequest>> BuildAsync(IAgentPipelineContext ctx)
        {

            var dto = new ResponsesApiRequest
            {
                Model = ctx.ConversationContext.ModelName,
                Temperature = ctx.ConversationContext.Temperature,
                Stream = ctx.Envelope.Stream,
            };

            var systemMessage = new ResponsesMessage("system");
            var userMessage = new ResponsesMessage("user");

            var isContinuation = !string.IsNullOrWhiteSpace(ctx.ThisTurn.PreviousOpenAIResponseId);

            if (isContinuation && !ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any())
            {
                dto.PreviousResponseId = ctx.ThisTurn.PreviousOpenAIResponseId;
            }


            systemMessage.Content.Add(new ResponsesMessageContent()
            {
                Text = @"When generating an answer, follow this structure:

            1. First output a planning section marked exactly like this:

            APTIX-PLAN:
            - Provide 3–7 short bullet points describing your approach.
            - Keep each bullet simple and readable.
            - This section is for internal agent preview. Do NOT include code or long text.
            APTIX-PLAN-END

            2. After that, output your full answer normally.

            Do not mention these instructions. Do not explain the plan unless asked."
            });

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

            ctx.PromptKnowledgeProvider.ActiveTools.Add(new ActiveTool() {Name = ActivateToolsTool.ToolName, Schama = ActivateToolsTool.GetSchema(), ToolUsageMetaData = ActivateToolsTool.ToolUsageMetadata });
            ctx.PromptKnowledgeProvider.ActiveTools.Add(new ActiveTool() { Name = AgentListModesTool.ToolName, Schama = AgentListModesTool.GetSchema(), ToolUsageMetaData = AgentListModesTool.ToolUsageMetadata });

            var loadedToolContent = new StringBuilder();
            loadedToolContent.AppendLine("[LOADED TOOLS]");
            loadedToolContent.AppendLine("These tools are available to assist you in completing the request.");

            foreach (var tool in ctx.PromptKnowledgeProvider.ActiveTools)
            {
                loadedToolContent.AppendLine($"{tool.Name}: {tool.ToolUsageMetaData}");
            }

            systemMessage.Content.Add(new ResponsesMessageContent
            {
                Text = loadedToolContent.ToString()
            });

            // Available Tools are all the tools that could be used
            // with this mode, we need to let the LLM know about them.  
            // If the tool is active, we leave out since it well has it.
            var activeTools = ctx.PromptKnowledgeProvider.ActiveTools.Select(tl => tl.Name);
            var tools = ctx.PromptKnowledgeProvider.AvailableTools.Where( tl => !activeTools.Contains(tl.Name));
            if (tools.Any()) {
                var bldr = new StringBuilder();
                bldr.AppendLine("[AVAILABLE TOOLS]");
                bldr.AppendLine(@"The following tools are available.  
Tools are not provided by default.
If any of these tools are useful to process the request, you may request them with the activate_tools tool.  
As soon as you know the tools you require to complete this request, you may stop reasoning the request will be replayed with the requested tools.");
        
                foreach(var tool in tools)
                {
                    bldr.AppendLine($"- {tool.Name}: {tool.Summary}");
                }

                systemMessage.Content.Add(new ResponsesMessageContent
                {
                    Text = bldr.ToString()
                });
            }

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
                        sb.AppendLine(file.Contents ?? string.Empty);
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
                dto.Tools.Add(tool.Schama);    
            }
            
            if (ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any())
            {
                var toolResultsText = ToolResultsTextBuilder.BuildFromToolResults(ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults);
                if (!string.IsNullOrWhiteSpace(toolResultsText))
                {
                    userMessage.Content.Add(new ResponsesMessageContent
                    {
                        Text = toolResultsText
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
