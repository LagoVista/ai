// --- BEGIN CODE INDEX META (do not edit) ---c
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
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.AiAgentContext_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),

        GetUrl: "/api/ai/agentcontext/{id}", GetListUrl: "/api/ai/agentcontexts", FactoryUrl: "/api/ai/agentcontext/factory", SaveUrl: "/api/ai/agentcontext",
        DeleteUrl: "/api/ai/agentcontext/{id}",

        PreviewUIUrl: "/mlworkbench/agent/{id}", ListUIUrl: "/mlworkbench/agents", EditUIUrl: "/mlworkbench/agent/{id}", CreateUIUrl: "/mlworkbench/agent/add",

        Icon: "icon-ae-database-3", ClusterKey: "agent", ModelType: EntityDescriptionAttribute.ModelTypes.Configuration,
        Shape: EntityDescriptionAttribute.EntityShapes.Entity, Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime,
        Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true, IndexTier: EntityDescriptionAttribute.IndexTiers.Primary,
        IndexPriority: 90, IndexTagsCsv: "ai,agent,configuration")]
    public class AgentContext : EntityBase, IFormDescriptor, ISummaryFactory, IFormConditionalFields, IValidateable, IFormDescriptorCol2, IFormDescriptorBottom, IAgentKnowledgeProvider, IToolBoxProvider
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

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultRole, PickerProviderFieldName: nameof(Roles), IsRequired:true, WaterMark: AIResources.Names.AgentContext_DefaultRole_Select, FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultRole { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultMode, PickerProviderFieldName: nameof(AgentModes), IsRequired:true, WaterMark: AIResources.Names.AgentContext_DefaultMode_Select, FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultMode { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Roles, HelpResource: AIResources.Names.AgentContext_Role_Description,AllowAddChild:true, CanAddRows: true, FieldType: FieldTypes.ChildList, FactoryUrl: "/api/ai/agentcontext/role/factory",
            ResourceType: typeof(AIResources))]
        public List<AgentContextRole> Roles { get; set; } = new List<AgentContextRole>();

        [FormField(LabelResource: AIResources.Names.AgentContext_DefaultPersona, HelpResource: AIResources.Names.AgentContext_DefaultPersona_Help, IsRequired:true, AllowAddChild: true, CanAddRows: true, 
            WaterMark:AIResources.Names.AgentContext_DefaultPersona_Select, FieldType: FieldTypes.EntithHeaderPickerDropDown, EditorPath: "/mlworkbench/agentpersona/{id}", EntityHeaderPickerUrl: "/api/ai/agentpersonas", FactoryUrl: "/api/ai/agentpersona/factory",
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultAgentPersona { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_MaxTokenCount, HelpResource: AIResources.Names.AgentContext_MaxTokenCount_Help, FieldType: FieldTypes.Integer, ResourceType: typeof(AIResources))]
        public int MaxTokenCount { get; set; } = 400000;

        [FormField(LabelResource: AIResources.Names.AgentContext_CompletionReservePercent, HelpResource: AIResources.Names.AgentContext_CompletionReservePercent_Help, FieldType: FieldTypes.Percent, ResourceType: typeof(AIResources))]
        public int CompletionReservePercent { get; set; } = 15;

        [FormField(LabelResource: AIResources.Names.AgentContext_WelcomeMessage, HelpResource: AIResources.Names.AgentContext_WelcomeMessage_Help, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string WelcomeMessage { get; set; }


        [FormField(LabelResource: AIResources.Names.AgentContext_InstructionDDRs, HelpResource: AIResources.Names.AgentContext_InstructionDDRs_Help, EntityHeaderPickerUrl: "/api/ddrs", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> InstructionDdrs { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_ReferenceDDRs, HelpResource: AIResources.Names.AgentContext_ReferenceDDRs_Help, EntityHeaderPickerUrl: "/api/ddrs", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ReferenceDdrs { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_ActiveTools, HelpResource: AIResources.Names.AgentContext_ActiveTools_Help, EntityHeaderPickerUrl: "/api/ai/agenttools", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ActiveTools { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_AvailableTools, HelpResource: AIResources.Names.AgentContext_AvailableTools_Help, EntityHeaderPickerUrl: "/api/ai/agenttools", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> AvailableTools { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_ToolBoxes, HelpResource: AIResources.Names.AgentContext_ToolBoxes_Help, EntityHeaderPickerUrl: "/api/ai/toolboxes", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ToolBoxes { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_Instructions, HelpResource: AIResources.Names.AgentContext_Instructions_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<string> Instructions { get; set; }= new List<string>();


        [FormField(LabelResource: AIResources.Names.AgentContext_Modes, HelpResource: AIResources.Names.AgentContext_Mode_Help, ChildListDisplayMember: nameof(AgentMode.Name), AllowAddChild: true, CanAddRows: true, FieldType: FieldTypes.ChildList, FactoryUrl: "/api/ai/agentcontext/mode/factory",
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
                nameof(DefaultRole),
                nameof(DefaultMode),
                nameof(DefaultAgentPersona),
                nameof(VectorDatabaseCollectionName),
                nameof(VectorDatabaseUri),
                nameof(VectorDatabaseApiKey),
                nameof(AzureAccountId),
                nameof(AzureApiToken),
                nameof(BlobContainerName),
                nameof(Instructions),
                nameof(LlmProvider),
                nameof(LlmApiKey),
                nameof(EmbeddingModel),
                nameof(MaxTokenCount),
                nameof(CompletionReservePercent),
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
                nameof(ToolBoxes),
                nameof(InstructionDdrs),
                nameof(ReferenceDdrs),
                nameof(AvailableTools),
                nameof(ActiveTools),
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

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContexts_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
       GetUrl: "/api/ai/agentcontext/{id}", GetListUrl: "/api/ai/agentcontexts", FactoryUrl: "/api/ai/agentcontext/factory", SaveUrl: "/api/ai/agentcontext", DeleteUrl: "/api/ai/agentcontext/{id}",
       ListUIUrl: "/mlworkbench/agents", EditUIUrl: "/mlworkbench/agent/{id}", CreateUIUrl: "/mlworkbench/agent/add", Icon: "icon-ae-database-3")]
    public class AgentContextSummary : SummaryData
    {

    }
}
