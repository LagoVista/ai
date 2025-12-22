// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 9691650f74ac0a4a1a4874cef7dbe09f9dae22cae37448f3f6ff08b058887e32
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContext_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        GetUrl: "/api/ai/agentcontext/{id}", GetListUrl: "/api/ai/agentcontexts", FactoryUrl: "/api/ai/agentcontext/factory", SaveUrl: "/api/ai/agentcontext", DeleteUrl: "/api/ai/agentcontext/{id}",
        ListUIUrl: "/mlworkbench/agents", EditUIUrl: "/mlworkbench/agent/{id}", CreateUIUrl: "/mlworkbench/agent/add", Icon: "icon-ae-database-3")]
    public class AgentContext : EntityBase, IFormDescriptor, ISummaryFactory, IFormConditionalFields, IValidateable, IFormDescriptorCol2, IFormDescriptorBottom
    {
        public const string LlmProvider_OpenAI = "openai";

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; } = "icon-ae-database-3";

        [FormField(LabelResource: AIResources.Names.VectorDatabase_CollectionName, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string VectorDatabaseCollectionName { get; set; }

        public string VectorDatabaseApiKeySecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_ApiKey, FieldType: FieldTypes.Secret, SecureIdFieldName: nameof(VectorDatabaseApiKeySecretId), ResourceType: typeof(AIResources))]
        public string VectorDatabaseApiKey { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_Uri, IsRequired: true, FieldType: FieldTypes.WebLink, ResourceType: typeof(AIResources))]
        public string VectorDatabaseUri { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureAccountId, HelpResource: AIResources.Names.VectorDatabase_AzureAccountId_Help, IsRequired: true, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string AzureAccountId { get; set; }

        public string AzureApiTokenSecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureApiToken, HelpResource: AIResources.Names.VectorDatabase_AzureApiToken_Help, SecureIdFieldName: nameof(AzureApiTokenSecretId), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string AzureApiToken { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureBlobContainerName, HelpResource: AIResources.Names.VectorDatabase_AzureBlobContainerName_Help, IsRequired: true, ResourceType: typeof(AIResources))]
        public string BlobContainerName { get; set; }

        public string LlmApiKeySecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDB_LLMEmbeddingModelName, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string EmbeddingModel { get; set; } = "text-embedding-3-large";

        [FormField(LabelResource: AIResources.Names.AgentContext_LlmProvider, WaterMark: AIResources.Names.AgentContext_LlmProvider_Select, FieldType: FieldTypes.Picker, EnumType: typeof(LlmProviders), IsRequired: true, ResourceType: typeof(AIResources))]
        public EntityHeader<LlmProviders> LlmProvider { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_OpenAPI_Token, HelpResource: AIResources.Names.VectorDatabase_OpenAPI_Token_Help, SecureIdFieldName: nameof(LlmApiKeySecretId), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string LlmApiKey { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultConversationContext, PickerProviderFieldName: nameof(ConversationContexts), WaterMark: AIResources.Names.AgentContext_DefaultConversationContext_Select, FieldType: FieldTypes.EntityHeaderPicker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultConversationContext { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContexts, HelpResource: AIResources.Names.AgentContext_ConversationContext_Description, FieldType: FieldTypes.ChildListInline, FactoryUrl: "/api/ai/agent/conversation/context/factory",
            ResourceType: typeof(AIResources))]
        public List<ConversationContext> ConversationContexts { get; set; } = new List<ConversationContext>();

        [FormField(LabelResource: AIResources.Names.AgentContext_MaxTokenCount, HelpResource: AIResources.Names.AgentContext_MaxTokenCount_Help, FieldType: FieldTypes.Integer, ResourceType: typeof(AIResources))]
        public int MaxTokenCount { get; set; } = 256 * 1024;

        [FormField(LabelResource: AIResources.Names.AgentContext_CompletionReservePercent, HelpResource: AIResources.Names.AgentContext_CompletionReservePercent_Help, FieldType: FieldTypes.Percent, ResourceType: typeof(AIResources))]
        public int CompletionReservePercent { get; set; } = 5;



        public List<AgentMode> AgentModes { get; set; } = new List<AgentMode>();

        public string BuildSystemPrompt(string currentModeKey)
        {
            var current = AgentModes.SingleOrDefault(mode => mode.Key == currentModeKey);

            if(current == null)
            {
                throw new RecordNotFoundException(nameof(AgentMode), currentModeKey);
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Current Mode: {current.Key}");
            sb.AppendLine();
            sb.AppendLine("Available Modes:");

            foreach (var mode in AgentModes)
            {
                sb.Append("- ")
                  .Append(mode.Key)
                  .Append(": ")
                  .AppendLine(mode.WhenToUse ?? mode.Description ?? string.Empty);
            }

            sb.AppendLine();
            sb.AppendLine("Mode Switching:");
            sb.AppendLine("- If the user’s request clearly matches another mode’s \"when to use\" description, you may recommend switching.");
            sb.AppendLine("- If the user expresses interest in switching, follow the instructions in the agent_change_mode tool.");
            sb.AppendLine("- If you need more detail about modes, call the agent_list_modes tool.");

            return sb.ToString();
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return this.CreateSummary();
        }

        public FormConditionals GetConditionalFields()
        {
            return new FormConditionals()
            {
                Conditionals = new List<FormConditional>()
                {
                    new FormConditional()
                    {
                        ForCreate = true,
                        ForUpdate = false,
                        RequiredFields = new List<string>() { nameof(VectorDatabaseApiKey), nameof(AzureApiToken), nameof(LlmApiKey) } }
                }
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Icon),
                nameof(VectorDatabaseCollectionName),
                nameof(VectorDatabaseUri),
                nameof(VectorDatabaseApiKey),
                nameof(AzureAccountId),
                nameof(AzureApiToken),
                nameof(BlobContainerName),
                nameof(Description)
            };
        }

        public AgentContextSummary CreateSummary()
        {
            var db = new AgentContextSummary();
            db.Populate(this);
            return db;
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(LlmProvider),
                nameof(LlmApiKey),
                nameof(EmbeddingModel),
                nameof(MaxTokenCount),
                nameof(CompletionReservePercent),
                nameof(DefaultConversationContext),
                nameof(ConversationContexts),
            };
        }

        public List<string> GetFormFieldsBottom()
        {
            return new List<string>()
            {
                nameof(Description)
            };
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentContext_ConversationContext_Title, AIResources.Names.AgentContext_ConversationContext_Description, AIResources.Names.AgentContext_ConversationContext_Description, EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),
    FactoryUrl: "/api/ai/agent/conversation/context/factory")]
    public class ConversationContext : IFormDescriptor, IValidateable, IConversationContext
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_ModelName, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string ModelName { get; set; } = "gpt-5";

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_System, HelpResource: AIResources.Names.AgentContext_ConversationContext_System_Help,
            FieldType: FieldTypes.MultiLineText, IsRequired: true, ResourceType: typeof(AIResources))]
        public List<string> SystemPrompts { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_Temperature, HelpResource: AIResources.Names.AgentContext_ConversationContext_Temperature_Help,
            FieldType: FieldTypes.Decimal, IsRequired: true, ResourceType: typeof(AIResources))]
        public float Temperature { get; set; } = 0.5f;

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(ModelName),
                nameof(Temperature),
            };
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContexts_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
       GetUrl: "/api/ai/agentcontext/{id}", GetListUrl: "/api/ai/agentcontexts", FactoryUrl: "/api/ai/agentcontext/factory", SaveUrl: "/api/ai/agentcontext", DeleteUrl: "/api/ai/agentcontext/{id}",
       ListUIUrl: "/mlworkbench/agents", EditUIUrl: "/mlworkbench/agent/{id}", CreateUIUrl: "/mlworkbench/agent/add", Icon: "icon-ae-database-3")]
    public class AgentContextSummary : SummaryData
    {

    }
}
