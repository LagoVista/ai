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
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.AgentContext_Role_Title, AIResources.Names.AgentContext_Role_Description,
        AIResources.Names.AgentContext_Role_Description, EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),

        FactoryUrl: "/api/ai/agentcontext/role/factory",

        ClusterKey: "agent", ModelType: EntityDescriptionAttribute.ModelTypes.Configuration, Shape: EntityDescriptionAttribute.EntityShapes.ChildObject,
        Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime, Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true,
        IndexTier: EntityDescriptionAttribute.IndexTiers.Aux, IndexPriority: 45, IndexTagsCsv: "ai,agent,prompting,child")]
    public class AgentContextRole : IFormDescriptor, IValidateable, IAgentKnowledgeProvider, IFormDescriptorCol2, IToolBoxProvider
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Role_ModelName, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string ModelName { get; set; } = "gpt-5.2";


        [FormField(LabelResource: AIResources.Names.AgentContext_Role_Temperature, HelpResource: AIResources.Names.AgentContext_Role_Temperature_Help,
            FieldType: FieldTypes.Decimal, IsRequired: true, ResourceType: typeof(AIResources))]
        public float Temperature { get; set; } = 0.5f;

        /// <summary>
        /// Optional welcome message shown when entering this mode.
        /// </summary>
        [FormField(LabelResource: AIResources.Names.AgentContext_Role_WelcomeMessage, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
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
        public List<string> Instructions { get; set; } = new List<string>();


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
                nameof(Instructions)
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(InstructionDdrs),
                nameof(ReferenceDdrs),
                nameof(ActiveTools),
                nameof(AvailableTools),
                nameof(ToolBoxes)
            };
        }
    }
}
