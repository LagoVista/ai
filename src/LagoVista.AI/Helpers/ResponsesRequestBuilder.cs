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
    /// - AgentContextRoles (model, system/boot prompt, temperature)
    /// - AgentExecuteRequest (mode, instruction, continuation id, tools, tool results, optional SystemPrompt)
    /// - ragContextBlock (pre-formatted [CONTEXT] block per AGN-002)
    /// - toolUsageMetadataBlock (LLM-facing usage metadata for ALL server tools)
    /// </summary>
    public class ResponsesRequestBuilder : IResponsesRequestBuilder
    {
        private readonly IAdminLogger _adminLogger;
        public ResponsesRequestBuilder(IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

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

            ctx.PromptKnowledgeProvider.ActiveTools.Add(new ActiveTool() {Name = ActivateToolsTool.ToolName, Schama = ActivateToolsTool.GetSchema(), ToolUsageMetaData = ActivateToolsTool.ToolUsageMetadata });
            ctx.PromptKnowledgeProvider.ActiveTools.Add(new ActiveTool() { Name = AgentListModesTool.ToolName, Schama = AgentListModesTool.GetSchema(), ToolUsageMetaData = AgentListModesTool.ToolUsageMetadata });

            var loadedToolContent = new StringBuilder();
            loadedToolContent.AppendLine("## ACTIVE TOOLS");
            loadedToolContent.AppendLine(@"These tools are currently active and ready and can immediately be used.
There are additional tools that are available and can be loaded with the actives_tools tool that have previously been identified.");

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
                bldr.AppendLine("## AVAILABLE TOOLS");
                bldr.AppendLine(@"The following tools are available but not loaded.  
Some tools are not provided by default.
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
