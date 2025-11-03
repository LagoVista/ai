using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.SourceOrganization_Title, AIResources.Names.SourceOrganization_Help, AIResources.Names.SourceOrganization_Help,
    EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),
    FactoryUrl: "/api/ai/agentcontext/sourceorg/factory")]

    public class SourceOrganization : IValidateable, IFormDescriptor
    {
        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganization_AppId, HelpResource:AIResources.Names.SourceOrganization_AppId_Help, 
            FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))] 
        public string AppId { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganization_InstallationId, HelpResource: AIResources.Names.SourceOrganization_InstallationId_Help,
          FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string InstallationId { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganization_PrivateKey, HelpResource: AIResources.Names.SourceOrganization_PrivateKey_Help,
          FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string PrivateKey { get; set; } = "";

        [FormField(LabelResource: AIResources.Names.SourceOrganization_WebHookSecret, HelpResource: AIResources.Names.SourceOrganization_WebHookSecret_Help,
          FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string WebhookSecret { get; set; } = "";

        [FormField(LabelResource: AIResources.Names.SourceOrganization_Name, HelpResource: AIResources.Names.SourceOrganization_Name_Help,
          FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string SourceOrganizationName { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganization_ProductName, HelpResource: AIResources.Names.SourceOrganization_ProductName_Help,
          FieldType: FieldTypes.Text, ValidationRegEx: "^[A-Za-z0-9][A-Za-z0-9\\-_]*(/[0-9]+(\\.[0-9]+)*)?$", IsRequired: true, ResourceType: typeof(AIResources))]
        public string ProductName { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganization_Repositories, HelpResource: AIResources.Names.SourceOrganization_Name_Help,
          FactoryUrl: "/api/ai/agentcontext/sourceorg/repo/factory", FieldType: FieldTypes.ChildListInline, IsRequired: true, ResourceType: typeof(AIResources))]
        public List<SourceOrganizationRepo> Repositories { get; set; } = new List<SourceOrganizationRepo>();    

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(ProductName),
                nameof(AppId),
                nameof(InstallationId),
                nameof(PrivateKey),
                nameof(WebhookSecret),
                nameof(SourceOrganizationName),
                nameof(Repositories)
            };
        }
    }
}
