using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentSession_Title, AIResources.Names.AgentToolBox_Help, AIResources.Names.AgentToolBox_Description, EntityDescriptionAttribute.EntityTypes.BusinessObject, typeof(AIResources),
           GetUrl: "/api/ai/toolbox/{id}", GetListUrl: "/api/ai/toolboxes", FactoryUrl: "/api/ai/toolbox/factory", SaveUrl: "/api/ai/toolbox", DeleteUrl: "/api/ai/toolbox/{id}",
           ListUIUrl: "/mlworkbench/toolboxs", EditUIUrl: "/mlworkbench/toolbox/{id}", CreateUIUrl: "/mlworkbench/toolbox/add", Icon: "icon-ae-direction")]
    public class AgentToolBox : EntityBase, IValidateable, ISummaryFactory, IFormDescriptor, IFormDescriptorCol2, IAgentKnowledgeProvider
    {

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources), IsRequired: true, IsUserEditable: true)]
        public string Icon { get; set; } = "icon-ae-direction";


        [FormField(LabelResource: AIResources.Names.AgentToolBox_SummaryInstructions, HelpResource: AIResources.Names.AgentToolBox_SummaryInstructions_Help, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string SummaryInstructions { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_InstructionDDRs, HelpResource: AIResources.Names.AgentContext_InstructionDDRs_Help, EntityHeaderPickerUrl: "/api/ddrs", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> InstructionDdrs { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_ReferenceDDRs, HelpResource: AIResources.Names.AgentContext_ReferenceDDRs_Help, EntityHeaderPickerUrl: "/api/ddrs", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ReferenceDdrs { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_ActiveTools, HelpResource: AIResources.Names.AgentContext_ActiveTools_Help, EntityHeaderPickerUrl: "/api/ai/agenttools", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> ActiveTools { get; set; } = new List<EntityHeader>();

        [FormField(LabelResource: AIResources.Names.AgentContext_AvailableTools, HelpResource: AIResources.Names.AgentContext_AvailableTools_Help, EntityHeaderPickerUrl: "/api/ai/agenttools", FieldType: FieldTypes.ChildListInlinePicker, ResourceType: typeof(AIResources))]
        public List<EntityHeader> AvailableTools { get; set; } = new List<EntityHeader>();
        [FormField(LabelResource: AIResources.Names.AgentContext_Instructions, HelpResource: AIResources.Names.AgentContext_Instructions_Help, FieldType: FieldTypes.StringList, ResourceType: typeof(AIResources))]
        public List<string> Instructions { get; set; } = new List<string>();


        public AgentToolBoxSummary CreateSummary()
        {
            var summary = new AgentToolBoxSummary();
            summary.Populate(this);
            return summary;
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Icon),
                nameof(Description),
                nameof(SummaryInstructions),
                nameof(Instructions),
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
            };
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return CreateSummary();
        }
    }


    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentToolBoxes_Title, AIResources.Names.AgentToolBox_Help, AIResources.Names.AgentToolBox_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
           GetUrl: "/api/ai/toolbox/{id}", GetListUrl: "/api/ai/toolboxes", FactoryUrl: "/api/ai/toolbox/factory", SaveUrl: "/api/ai/toolbox", DeleteUrl: "/api/ai/toolbox/{id}",
           ListUIUrl: "/mlworkbench/toolboxs", EditUIUrl: "/mlworkbench/toolbox/{id}", CreateUIUrl: "/mlworkbench/toolbox/add", Icon: "icon-ae-direction")]
    public class AgentToolBoxSummary : SummaryData
    {

    }
}
