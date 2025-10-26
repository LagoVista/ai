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
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiConversation_Title, AIResources.Names.AIConversation_Description, AIResources.Names.AIConversation_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
       GetListUrl: "/api/ai/conversations", GetUrl: "/api/ai/conversation/{id}", SaveUrl: "/api/ai/conversation", FactoryUrl: "/api/ml/conversation/factory", DeleteUrl: "/api/ai/conversation/{id}",
       ListUIUrl: "/mlworkbench/conversations", EditUIUrl: "/mlworkbench/conversation/{id}", CreateUIUrl: "/mlworkbench/conversation/add")]
    public class AiConversation : EntityBase, IValidateable, ISummaryFactory
    {

        public AiConversationSummary CreateSummary()
        {
            var summary = new AiConversationSummary();
            summary.Populate(this);
            return summary;
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return this.CreateSummary();
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiConversation_Title, AIResources.Names.AIConversation_Description, AIResources.Names.AIConversation_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel,
        typeof(AIResources), FactoryUrl: "/api/ml/conversation/interaction/factory")]
    public class AiConversationInteraction
    {
        public string Id { get; set; }
        public EntityHeader User { get; set; }
        public string TimeStamp { get; set; }
        public string Prompt { get; set; }
        public string Response { get; set; }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiConversations_Title, AIResources.Names.AIConversation_Description, AIResources.Names.AIConversation_Description, EntityDescriptionAttribute.EntityTypes.Summary, typeof(AIResources),
        GetListUrl: "/api/ai/conversations", GetUrl: "/api/ai/conversation/{id}", SaveUrl: "/api/ai/conversation", FactoryUrl: "/api/ml/modellabel/factory", DeleteUrl: "/api/ai/conversation/{id}",
        ListUIUrl: "/mlworkbench/conversations", EditUIUrl: "/mlworkbench/conversation/{id}", CreateUIUrl: "/mlworkbench/conversation/add")]
    public class AiConversationSummary : SummaryData
    {

    }
}
