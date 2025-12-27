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
           GetUrl: "/api/ai/toolbox/{id}", GetListUrl: "/api/ai/toolboxs", FactoryUrl: "/api/ai/toolbox/factory", SaveUrl: "/api/ai/toolbox", DeleteUrl: "/api/ai/toolbox/{id}",
           ListUIUrl: "/mlworkbench/toolboxs", EditUIUrl: "/mlworkbench/toolbox/{id}", CreateUIUrl: "/mlworkbench/toolbox/add", Icon: "icon-ae-database-3")]
    public class AgentToolBox : EntityBase, IValidateable, ISummaryFactory
    {
        [FormField(LabelResource: AIResources.Names.AgentToolBox_SummaryInstructions, HelpResource: AIResources.Names.AgentToolBox_SummaryInstructions_Help, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Summary { get; set; }
        public List<string> ToolIds { get; set; }
        public List<string> InstructionDdrs { get; set;  }
        public List<string> ReferenceDdrs { get; set; }

        public AgentToolBoxSummary CreateSummary()
        {
            var summary = new AgentToolBoxSummary();
            summary.Populate(this);
            return summary;
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return CreateSummary();
        }
    }


    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentToolBoxes_Title, AIResources.Names.AgentToolBox_Help, AIResources.Names.AgentToolBox_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
           GetUrl: "/api/ai/toolbox/{id}", GetListUrl: "/api/ai/toolboxs", FactoryUrl: "/api/ai/toolbox/factory", SaveUrl: "/api/ai/toolbox", DeleteUrl: "/api/ai/toolbox/{id}",
           ListUIUrl: "/mlworkbench/toolboxs", EditUIUrl: "/mlworkbench/toolbox/{id}", CreateUIUrl: "/mlworkbench/toolbox/add", Icon: "icon-ae-database-3")]
    public class AgentToolBoxSummary : SummaryData
    {

    }
}
