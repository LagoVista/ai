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
    public class AgentToolBox : EntityBase, IValidateable, ISummaryFactory, IFormDescriptor
    {

        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources), IsRequired: true, IsUserEditable: true)]
        public string Icon { get; set; } = "icon-ae-direction";


        [FormField(LabelResource: AIResources.Names.AgentToolBox_SummaryInstructions, HelpResource: AIResources.Names.AgentToolBox_SummaryInstructions_Help, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string SummaryInstructions { get; set; }
        public List<EntityHeader> Tools { get; set; } = new List<EntityHeader>();
        public List<EntityHeader> InstructionDdrs { get; set; } = new List<EntityHeader>();
        public List<EntityHeader> ReferenceDdrs { get; set; } = new List<EntityHeader>();

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
