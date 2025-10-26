using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{

    public enum LlmProviders
    {
        [EnumLabel(AgentContext.LlmProvider_OpenAI, AIResources.Names.LlmProvider_OpenAI, typeof(AIResources))]
        OpenAI
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContext_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        GetUrl: "/api/ai/agentcontext/{id}", GetListUrl: "/api/ai/agentcontexts", FactoryUrl: "/api/ai/agentcontext/factory", SaveUrl: "/api/ai/agentcontext", DeleteUrl: "/api/ai/agentcontext/{id}",
        ListUIUrl: "/mlworkbench/agents", EditUIUrl: "/mlworkbench/agent/{id}", CreateUIUrl: "/mlworkbench/agent/add", Icon: "icon-ae-database-3")]
    public class AgentContext : EntityBase, IFormDescriptor, ISummaryFactory, IFormConditionalFields, IValidateable, IFormDescriptorCol2, IFormDescriptorBottom
    {
        public const string LlmProvider_OpenAI = "openai";

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; } = "icon-ae-database-3";

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_CollectionName, FieldType: FieldTypes.Text, IsRequired:true, ResourceType: typeof(AIResources))]
        public string VectorDatabaseCollectionName { get; set; }

        public string VectorDatabaseApiKeySecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_ApiKey, FieldType: FieldTypes.Secret, SecureIdFieldName:nameof(VectorDatabaseApiKeySecretId), ResourceType: typeof(AIResources))]
        public string VectorDatabaseApiKey { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_Uri, IsRequired: true, FieldType: FieldTypes.WebLink,  ResourceType: typeof(AIResources))]
        public string VectorDatabaseUri { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureAccountId, HelpResource: AIResources.Names.VectorDatabase_AzureAccountId_Help, IsRequired: true, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string AzureAccountId { get; set; }

        public string AzureApiTokenSecretId { get; set; }
        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureApiToken, HelpResource: AIResources.Names.VectorDatabase_AzureApiToken_Help, SecureIdFieldName:nameof(AzureApiTokenSecretId), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string AzureApiToken { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureBlobContainerName, HelpResource: AIResources.Names.VectorDatabase_AzureBlobContainerName_Help, IsRequired:true, ResourceType: typeof(AIResources))]
        public string BlobContainerName { get; set; }

        public string LlmApiKeySecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_LlmProvider, WaterMark: AIResources.Names.AgentContext_LlmProvider_Select, FieldType:FieldTypes.Picker, EnumType: typeof(LlmProviders), IsRequired: true, ResourceType: typeof(AIResources))]
        public EntityHeader<LlmProviders> LlmProvider { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_OpenAPI_Token, HelpResource: AIResources.Names.VectorDatabase_OpenAPI_Token_Help, SecureIdFieldName:nameof(LlmApiKeySecretId), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string LlmApiKey { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultConversationContext, WaterMark:AIResources.Names.AgentContext_DefaultConversationContext_Select, FieldType: FieldTypes.EntityHeaderPicker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultConversationContext { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContexts, HelpResource: AIResources.Names.AgentContext_ConversationContext_Description, FieldType: FieldTypes.ChildListInline, FactoryUrl: "/api/ai/agent/conversation/context", 
            ResourceType: typeof(AIResources))]
        public List<ConversationContext> ConversationContexts { get; set; } = new List<ConversationContext>();

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
    FactoryUrl:"/api/ai/agent/conversation/context")]
    public class ConversationContext : IFormDescriptor, IValidateable
    {
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_ModelName,  FieldType:FieldTypes.Text,  IsRequired: true, ResourceType: typeof(AIResources))]
        public string ModelName { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_System, HelpResource:AIResources.Names.AgentContext_ConversationContext_System_Help, 
            FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string System { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_ConversationContext_System, HelpResource: AIResources.Names.AgentContext_ConversationContext_Temperature_Help,
            FieldType: FieldTypes.Decimal, IsRequired: true, ResourceType: typeof(AIResources))]
        public float Temperature { get; set; }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(ModelName),
                nameof(System),
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
