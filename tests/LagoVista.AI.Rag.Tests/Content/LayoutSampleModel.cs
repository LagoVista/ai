using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin,
        AIResources.Names.AiAgentContext_Title,
        AIResources.Names.AiAgentContext_Description,
        AIResources.Names.AiAgentContext_Description,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel,
        typeof(AIResources),
        GetUrl: "/api/ai/layoutsample/{id}",
        GetListUrl: "/api/ai/layoutsamples",
        FactoryUrl: "/api/ai/layoutsample/factory",
        SaveUrl: "/api/ai/layoutsample",
        DeleteUrl: "/api/ai/layoutsample/{id}",
        ListUIUrl: "/mlworkbench/layoutsamples",
        EditUIUrl: "/mlworkbench/layoutsample/{id}",
        CreateUIUrl: "/mlworkbench/layoutsample/add")]
    public class LayoutSampleModel : IFormDescriptor,
        IFormDescriptorCol2,
        IFormDescriptorBottom,
        IFormDescriptorAdvanced,
        IFormDescriptorAdvancedCol2,
        IFormDescriptorInlineFields,
        IFormMobileFields,
        IFormDescriptorSimple,
        IFormDescriptorQuickCreate,
        IFormAdditionalActions
    {
        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources), IsRequired: true)]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, FieldType: FieldTypes.Key, ResourceType: typeof(AIResources))]
        public string Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        // ----- Primary form layout -----
        public List<string> GetFormFields()
        {
            return new List<string>
            {
                nameof(Name)
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>
            {
                nameof(Key)
            };
        }

        public List<string> GetFormFieldsBottom()
        {
            return new List<string>
            {
                nameof(Description)
            };
        }

        // ----- Advanced layout -----
        public List<string> GetAdvancedFields()
        {
            return new List<string>
            {
                nameof(Name)
            };
        }

        public List<string> GetAdvancedFieldsCol2()
        {
            return new List<string>
            {
                nameof(Description)
            };
        }

        // ----- Inline / Mobile / Simple / QuickCreate -----
        public List<string> GetInlineFields()
        {
            return new List<string>
            {
                nameof(Name)
            };
        }

        public List<string> GetMobileFields()
        {
            return new List<string>
            {
                nameof(Key)
            };
        }

        public List<string> GetSimpleFields()
        {
            return new List<string>
            {
                nameof(Name),
                nameof(Key)
            };
        }

        public List<string> GetQuickCreateFields()
        {
            return new List<string>
            {
                nameof(Name)
            };
        }

        // ----- Additional actions -----
        public IEnumerable<FormAdditionalAction> GetAdditionalActions()
        {
            return new List<FormAdditionalAction>
            {
                new FormAdditionalAction
                {
                    Title = AIResources.Names.AgentContext_DefaultConversationContext,
                    Icon = "icon-plus",
                    Help = AIResources.Names.AgentContext_ConversationContext_Description,
                    Key = "addContext",
                    ForCreate = true,
                    ForEdit = true
                }
            };
        }
    }
}
