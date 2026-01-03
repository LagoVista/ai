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
        public List<EntityHeader> ActiveToolss { get; set; } = new List<EntityHeader>();

        public List<EntityHeader> AgentInstructionDdrs { get; set; } = new List<EntityHeader>();


        /// <summary>
        /// DDR's that produce patterns, practices and standards that can be used when the LLM reasons.
        /// </summary>
        public List<EntityHeader> ReferenceDdrs { get; set; } = new List<EntityHeader>();

        /// <summary>
        /// Tool IDs that are enabled when this mode is active.
        /// </summary>
        public List<EntityHeader> ActiveTools { get; set; } = new List<EntityHeader>();


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
}
