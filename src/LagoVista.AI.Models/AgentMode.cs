using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Full design-time definition of a single Aptix Agent Mode.
    /// Backed by AGN-013. Instances are immutable at runtime and
    /// loaded into the Agent Mode Catalog at startup.
    /// </summary>
    /// 

    public enum AgentModeStatuses
    {
        [EnumLabel(AgentMode.AgentMode_AgentModeStaus_New, AIResources.Names.Common_Status_Active, typeof(AIResources))]
        New,
        [EnumLabel(AgentMode.AgentMode_AgentModeStaus_Experimental, AIResources.Names.Common_Status_Experimental, typeof(AIResources))]
        Experimental,
        [EnumLabel(AgentMode.AgentMode_AgentModeStaus_Active, AIResources.Names.Common_Status_Active, typeof(AIResources))]
        Active,
        [EnumLabel(AgentMode.AgentMode_AgentModeStaus_Deprecated, AIResources.Names.AgentMode_AgentModeStaus_Deprecated, typeof(AIResources))]
        Deprecated,
        [EnumLabel(AgentMode.AgentMode_AgentModeStaus_Obsolete, AIResources.Names.AgentMode_AgentModeStaus_Obsolete, typeof(AIResources))]
        Obsolete
    }


    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentContext_Mode_Title, AIResources.Names.AgentContext_Mode_Description, AIResources.Names.AgentContext_Mode_Description, EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),
    FactoryUrl: "/api/ai/agentcontext/mode/factory")]
    public sealed class AgentMode : IFormDescriptor, IFormDescriptorCol2
    {
        public const string AgentMode_AgentModeStaus_New = "new";
        public const string AgentMode_AgentModeStaus_Experimental = "experimental";
        public const string AgentMode_AgentModeStaus_Active = "active";
        public const string AgentMode_AgentModeStaus_Deprecated = "deprecated";
        public const string AgentMode_AgentModeStaus_Obsolete = "obsolete";

        // 3.1 Identity & UI Metadata

        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.AgentMode_Key, HelpResource: AIResources.Names.AgentMode_Key_Help, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }


        [FormField(LabelResource: AIResources.Names.AgentMode_Description, HelpResource: AIResources.Names.AgentMode_Description_Help, FieldType: FieldTypes.MultiLineText, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentMode_WhenToUse, HelpResource: AIResources.Names.AgentMode_WhenToUse_Help, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string WhenToUse { get; set; }
        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; } = "icon-ae-database-3";


        // 3.2 User Interaction Metadata

        [FormField(LabelResource: AIResources.Names.AgentMode_WelcomeMessage, HelpResource: AIResources.Names.AgentMode_WelcomeMessage_Help, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string WelcomeMessage { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentMode_BootstrapInstructions, HelpResource: AIResources.Names.AgentMode_BootstrapInstructions_Help, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string BootstrapInstructions { get; set; }


        [FormField(LabelResource: AIResources.Names.AgentMode_BehaviorHints, HelpResource: AIResources.Names.AgentMode_BehaviorHints_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] BehaviorHints { get; set; } = Array.Empty<string>();

        [FormField(LabelResource: AIResources.Names.AgentMode_HumanRoleHints, HelpResource: AIResources.Names.AgentMode_HumanRoleHints_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] HumanRoleHints { get; set; } = Array.Empty<string>();


        [FormField(LabelResource: AIResources.Names.AgentMode_AgentInstructionDdrs, HelpResource: AIResources.Names.AgentMode_AgentInstructionDdrs_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<EntityHeader> AgentInstructionDdrs { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentMode_ReferenceDdrs, HelpResource: AIResources.Names.AgentMode_ReferenceDdrs_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ReferenceDdrs { get; set; } = new List<EntityHeader>();

        // 3.3 Tools

        [FormField(LabelResource: AIResources.Names.AgentMode_AssociatedToolIds, HelpResource: AIResources.Names.AgentMode_AssociatedToolIds_Help, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ActiveTools { get; set; } = new List<EntityHeader>();


        [FormField(LabelResource: AIResources.Names.AgentMode_AssociatedToolIds, HelpResource: AIResources.Names.AgentMode_AssociatedToolIds_Help, FieldType: FieldTypes.EntityHeaderPicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> AvailableToools { get; set; } = new List<EntityHeader>();


        // 3.4 RAG Scoping Metadata

        [FormField(LabelResource: AIResources.Names.AgentMode_RagScopeHints, HelpResource: AIResources.Names.AgentMode_RagScopeHints_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] RagScopeHints { get; set; } = Array.Empty<string>();

        // 3.5 Recognition Metadata

        [FormField(LabelResource: AIResources.Names.AgentMode_StrongSignals, HelpResource: AIResources.Names.AgentMode_StrongSignals_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] StrongSignals { get; set; } = Array.Empty<string>();

        [FormField(LabelResource: AIResources.Names.AgentMode_WeakSignals, HelpResource: AIResources.Names.AgentMode_WeakSignals_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] WeakSignals { get; set; } = Array.Empty<string>();

        [FormField(LabelResource: AIResources.Names.AgentMode_ExampleUtterances, HelpResource: AIResources.Names.AgentMode_ExampleUtterances_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public string[] ExampleUtterances { get; set; } = Array.Empty<string>();

        [FormField(LabelResource: AIResources.Names.AgentMode_Instructions, HelpResource: AIResources.Names.AgentMode_Instructions_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<string> Instructions { get; set; } = new List<string>();

        // 3.6 Lifecycle Metadata

        [FormField(LabelResource: AIResources.Names.AgentMode_Status, HelpResource: AIResources.Names.AgentMode_Status_Help, EnumType: typeof(AgentModeStatuses),
            FieldType: FieldTypes.Picker, IsRequired: true, ResourceType: typeof(AIResources))]
        public EntityHeader<AgentModeStatuses> ModeStatus { get; set; } = EntityHeader<AgentModeStatuses>.Create(AgentModeStatuses.New);

        [FormField(LabelResource: AIResources.Names.AgentMode_ToolBoxes, HelpResource: AIResources.Names.AgentMode_ToolBoxes_Help, FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ToolBoxes { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentMode_Version, HelpResource: AIResources.Names.AgentMode_Version_Help, IsRequired:true, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string Version { get; set; } = "1.0.0";

        [FormField(LabelResource: AIResources.Names.AgentMode_IsDefault, HelpResource: AIResources.Names.AgentMode_IsDefault_Help, FieldType: FieldTypes.CheckBox, ResourceType: typeof(AIResources))]
        public bool IsDefault { get; set; }

        public AgentModeSummary CreateSummary()
        {
            return new AgentModeSummary
            {
                Id = this.Id,
                Key = this.Key,
                Name = this.Name,
                Description = this.Description ?? this.WhenToUse,
                SystemPromptSummary = this.WhenToUse,
                IsDefault = this.IsDefault,
                ModeStatus = this.ModeStatus.Text,
                Version = this.Version,
                WhenToUse = this.WhenToUse,
                HumanRoleHints = this.HumanRoleHints ?? Array.Empty<string>(),
                ExampleUtterances = this.ExampleUtterances ?? Array.Empty<string>()
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Icon),
                nameof(IsDefault),
                nameof(Version),
                nameof(ModeStatus),
                nameof(Description),
                nameof(WhenToUse),
                nameof(WelcomeMessage),
                nameof(BootstrapInstructions),
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(BehaviorHints),
                nameof(HumanRoleHints),
                nameof(RagScopeHints),
                nameof(StrongSignals),
                nameof(WeakSignals),
                nameof(ExampleUtterances),
                nameof(Instructions),
            };
        }
    }

    public class BootStrapTool
    {
        public string ToolName { get; set; }
        public string[] Arguments { get; set; }
    }


    /// <summary>
    /// Minimal mode summary DTO exposed by IAgentModeCatalogService.
    /// </summary>
    public sealed class AgentModeSummary
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ModeStatus { get; set; }
        public string WhenToUse { get; set; }
        public string Version { get; set; }
        public string SystemPromptSummary { get; set; }
        public bool IsDefault { get; set; }
        public string[] HumanRoleHints { get; set; }
        public string[] ExampleUtterances { get; set; }
    
    
    }
}
