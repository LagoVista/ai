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
        public Task<InvokeResult< ResponsesApiRequest>> BuildAsync(IAgentPipelineContext ctx) 
        {

            var dto = new ResponsesApiRequest
            {
                Model = ctx.ConversationContext.ModelName,
                Temperature = ctx.ConversationContext.Temperature,
                Stream = ctx.Envelope.Stream,
            };

            var isContinuation = !string.IsNullOrWhiteSpace(ctx.ThisTurn.PreviousOpenAIResponseId) && String.IsNullOrEmpty(ctx.PromptKnowledgeProvider.ToolCallManifest.ResultsJson);

            if (isContinuation)
            {
                dto.PreviousResponseId = ctx.ThisTurn.PreviousOpenAIResponseId;
            }

            var systemMessage = new ResponsesMessage("system");
            var userMessage = new ResponsesMessage("user");

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
              
            var instructionBlock =
                "[MODE: " + ctx.Session.Mode + "]\n\n[INSTRUCTION]\n" + (ctx.Envelope.Instructions ?? string.Empty);

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

            foreach(var tool in ctx.PromptKnowledgeProvider.AvailableToolSchemas)
            {
                dto.Tools.Add(new  JObject(tool));    
            }

            dto.Input.Add(systemMessage);
            dto.Input.Add(userMessage);
         
            return Task.FromResult(InvokeResult<ResponsesApiRequest>.Create(dto));

//            if (conversationContext == null) throw new ArgumentNullException(nameof(conversationContext));
//            if (request == null) throw new ArgumentNullException(nameof(request));

//            var isContinuation = !string.IsNullOrWhiteSpace(request.ResponseContinuationId) && String.IsNullOrEmpty(request.ToolResultsJson);


//            if (isContinuation)
//            {
//                dto.PreviousResponseId = request.ResponseContinuationId;
//            }

//            // ---------------------------------------------------------------------
//            // (1) SYSTEM MESSAGE — ALWAYS INCLUDED
//            // ---------------------------------------------------------------------

//            var hasAnySystemContent =
//                (conversationContext.SystemPrompts != null && conversationContext.SystemPrompts.Count > 0)
//                || !string.IsNullOrWhiteSpace(request.SystemPrompt)
//                || !string.IsNullOrWhiteSpace(toolUsageMetadataBlock);

//            if (hasAnySystemContent)
//            {
//                var systemMessage = new ResponsesMessage
//                {
//                    Role = "system",
//                    Content = new List<ResponsesMessageContent>()
//                };

//                // Boot / conversation-level prompts
//                if (conversationContext.SystemPrompts != null)
//                {
//                    foreach (var systemPrompt in conversationContext.SystemPrompts)
//                    {
//                        if (!string.IsNullOrWhiteSpace(systemPrompt))
//                        {
//                            systemMessage.Content.Add(new ResponsesMessageContent
//                            {
//                                Text = systemPrompt
//                            });
//                        }
//                    }
//                }

//                // Optional per-request SystemPrompt
//                if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
//                {
//                    systemMessage.Content.Add(new ResponsesMessageContent
//                    {
//                        Text = request.SystemPrompt
//                    });
//                }

//                systemMessage.Content.Add(new ResponsesMessageContent()
//                {
//                    Text = @"When generating an answer, follow this structure:

//1. First output a planning section marked exactly like this:

//APTIX-PLAN:
//- Provide 3–7 short bullet points describing your approach.
//- Keep each bullet simple and readable.
//- This section is for internal agent preview. Do NOT include code or long text.
//APTIX-PLAN-END

//2. After that, output your full answer normally.

//Do not mention these instructions. Do not explain the plan unless asked.
//"
//                });

//                // Optional tool usage metadata for all tools
//                if (!string.IsNullOrWhiteSpace(toolUsageMetadataBlock))
//                {
//                    systemMessage.Content.Add(new ResponsesMessageContent
//                    {
//                        Text = toolUsageMetadataBlock
//                    });
//                }

//                dto.Input.Add(systemMessage);
//            }

//            // ---------------------------------------------------------------------
//            // (2) USER MESSAGE
//            // ---------------------------------------------------------------------

//            var userMessage = new ResponsesMessage
//            {
//                Role = "user",
//                Content = new List<ResponsesMessageContent>()
//            };

//            var instructionBlock =
//                "[MODE: " + request.Mode + "]\n\n[INSTRUCTION]\n" + (request.Instruction ?? string.Empty);

//            userMessage.Content.Add(new ResponsesMessageContent
//            {
//                Text = instructionBlock
//            });
//            // ---------------------------------------------------------------------
//            // IMAGE ATTACHMENTS (from client-side chat composer)
//            // ---------------------------------------------------------------------
//            if (request.ImageAttachments != null && request.ImageAttachments.Any())
//            {
//                foreach (var img in request.ImageAttachments)
//                {
//                    if (img == null) continue;
//                    if (string.IsNullOrWhiteSpace(img.DataBase64)) continue;
//                    if (string.IsNullOrWhiteSpace(img.MimeType)) continue;

//                    var dataUrl = $"data:{img.MimeType};base64,{img.DataBase64}";

//                    userMessage.Content.Add(new ResponsesMessageContent
//                    {
//                        Type = "input_image",
//                        ImageUrl = dataUrl
//                    });
//                }
//            }


//            // ---------------------------------------------------------------------
//            // ACTIVE FILES (Attach editor contents for reasoning)
//            // ---------------------------------------------------------------------
//            if (request.ActiveFiles != null && request.ActiveFiles.Any())
//            {
//                var sb = new StringBuilder();
//                sb.AppendLine("[ACTIVE FILES]");

//                foreach (var file in request.ActiveFiles)
//                {
//                    sb.AppendLine($"--- BEGIN ACTIVE FILE ---");
//                    sb.AppendLine($"Absolute Path: {file.AbsolutePath}");
//                    sb.AppendLine($"Relative Path: {file.RelativePath}");
//                    sb.AppendLine($"File Name: {file.FileName}");
//                    sb.AppendLine($"SHA256 Hash: {file.Sha256Hash}");
//                    sb.AppendLine($"Language: {file.Language}");
//                    sb.AppendLine();
//                    sb.AppendLine(file.Contents ?? string.Empty);
//                    sb.AppendLine($"--- END ACTIVE FILE ---");
//                    sb.AppendLine();
//                }

//                userMessage.Content.Add(new ResponsesMessageContent
//                {
//                    Text = sb.ToString()
//                });
//            }

//            if (!string.IsNullOrWhiteSpace(ragContextBlock))
//            {
//                userMessage.Content.Add(new ResponsesMessageContent
//                {
//                    Text = ragContextBlock
//                });
//            }

//            if (!string.IsNullOrWhiteSpace(request.ToolResultsJson))
//            {
//                var toolResultsText = ToolResultsTextBuilder.BuildFromToolResultsJson(request.ToolResultsJson);
//                if (!string.IsNullOrWhiteSpace(toolResultsText))
//                {
//                    userMessage.Content.Add(new ResponsesMessageContent
//                    {
//                        Text = toolResultsText
//                    });
//                }
//            }

//            dto.Input.Add(userMessage);

//            // ---------------------------------------------------------------------
//            // (3) TOOLS — INCLUDED ON EVERY TURN WHEN PRESENT
//            // ---------------------------------------------------------------------

//            if (!string.IsNullOrWhiteSpace(request.ToolsJson))
//            {
//                try
//                {
//                    var toolsToken = JToken.Parse(request.ToolsJson);
//                    if (toolsToken is JArray toolsArray && toolsArray.Count > 0)
//                    {
//                        dto.Tools = new List<JObject>();
//                        foreach (var tool in toolsArray)
//                        {
//                            if (tool is JObject obj)
//                            {
//                                dto.Tools.Add(obj);
//                            }
//                        }
//                    }
//                }
//                catch (JsonException)
//                {
//                    // Ignore malformed ToolsJson; higher-level code may log
//                }
//            }

//            // ---------------------------------------------------------------------
//            // (4) TOOL CHOICE (optional)
//            // ---------------------------------------------------------------------

//            if (!string.IsNullOrWhiteSpace(request.ToolChoiceName))
//            {
//                dto.ToolChoice = new ResponsesToolChoice
//                {
//                    Name = request.ToolChoiceName
//                };
//            }


//            return dto;
        }
    }
}
