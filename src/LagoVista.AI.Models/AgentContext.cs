// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 9691650f74ac0a4a1a4874cef7dbe09f9dae22cae37448f3f6ff08b058887e32
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;

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

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultRole, PickerProviderFieldName: nameof(Roles), WaterMark: AIResources.Names.AgentContext_DefaultRole_Select, FieldType: FieldTypes.EntityHeaderPicker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultRole { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Roles, HelpResource: AIResources.Names.AgentContext_Role_Description, FieldType: FieldTypes.ChildList, FactoryUrl: "/api/ai/agentcontext/role/factory",
            ResourceType: typeof(AIResources))]
        public List<AgentContextRole> Roles { get; set; } = new List<AgentContextRole>();


        [FormField(LabelResource: AIResources.Names.AgentContext_MaxTokenCount, HelpResource: AIResources.Names.AgentContext_MaxTokenCount_Help, FieldType: FieldTypes.Integer, ResourceType: typeof(AIResources))]
        public int MaxTokenCount { get; set; } = 400000;

        [FormField(LabelResource: AIResources.Names.AgentContext_CompletionReservePercent, HelpResource: AIResources.Names.AgentContext_CompletionReservePercent_Help, FieldType: FieldTypes.Percent, ResourceType: typeof(AIResources))]
        public int CompletionReservePercent { get; set; } = 15;


        /// <summary>
        /// Optional welcome message shown when entering this mode.
        /// </summary>
        public string WelcomeMessage { get; set; }

        /// <summary>
        /// Instructions to be sent over with the initial turn
        /// </summary>
        public string BoolstrapInstructions { get; set; }

        /// <summary>
        /// Mode-specific behavior instructions for the LLM when this
        /// mode is active (go into the Active Mode Behavior Block).
        /// </summary>
        public string[] AgentInstructionDdrs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// DDR's that produce patterns, practices and standards that can be used when the LLM reasons.
        /// </summary>
        public string[] ReferenceDdrs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Tool IDs that are enabled when this mode is active.
        /// </summary>
        public string[] AssociatedToolIds { get; set; } = Array.Empty<string>();

        public List<EntityHeader> ToolBoxes { get; set; } = new List<EntityHeader>();

        public List<string> Instructions { get; set; }= new List<string>();

        /// <summary>
        /// Optional grouping hints for UI or LLM reasoning, e.g. "authoring",
        /// "read-only", "diagnostics".
        /// </summary>
        public string[] ToolGroupHints { get; set; } = Array.Empty<string>();

        [FormField(LabelResource: AIResources.Names.AgentContext_Modes, HelpResource: AIResources.Names.AgentContext_Mode_Help, FieldType: FieldTypes.ChildList, FactoryUrl: "/api/ai/agentcontext/mode/factory",
           ResourceType: typeof(AIResources))]
        public List<AgentMode> AgentModes { get; set; } = new List<AgentMode>();

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
                nameof(DefaultRole),
                nameof(Roles),
                nameof(AgentModes),
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

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentContext_Role_Title, AIResources.Names.AgentContext_Role_Description, AIResources.Names.AgentContext_Role_Description, EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),
    FactoryUrl: "/api/ai/agentcontext/role/factory")]
    public class AgentContextRole : IFormDescriptor, IValidateable
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Role_ModelName, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string ModelName { get; set; } = "gpt-5.2";


        [FormField(LabelResource: AIResources.Names.AgentContext_Role_Temperature, HelpResource: AIResources.Names.AgentContext_Role_Temperature_Help,
            FieldType: FieldTypes.Decimal, IsRequired: true, ResourceType: typeof(AIResources))]
        public float Temperature { get; set; } = 0.5f;

        [FormField(LabelResource: AIResources.Names.AgentContext_Role_Persona_Instructions, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string PersonaInstructions { get; set; }

        /// <summary>
        /// Optional welcome message shown when entering this mode.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.AgentContext_Role_WelcomeMessage, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string WelcomeMessage { get; set; }

        /// <summary>
        /// Mode-specific behavior instructions for the LLM when this
        /// mode is active (go into the Active Mode Behavior Block).
        /// </summary>
        public string[] AgentInstructionDdrs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// DDR's that produce patterns, practices and standards that can be used when the LLM reasons.
        /// </summary>
        public string[] ReferenceDdrs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Tool IDs that are enabled when this mode is active.
        /// </summary>
        public string[] AssociatedToolIds { get; set; } = Array.Empty<string>();


        public List<EntityHeader> ToolBoxes { get; set; } = new List<EntityHeader>();

        public EntityHeader ToEntityHeader()
        {
            return EntityHeader.Create(Id, Name);
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(ModelName),
                nameof(Temperature),
                nameof(WelcomeMessage),
                nameof(PersonaInstructions),
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
