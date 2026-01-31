// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 4eb16c000f5ffef751cdf885d0ff27aea7d4b79a68331d5b418f42d6ef70557f
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.SourceOrganization_Title, AIResources.Names.SourceOrganization_Help, AIResources.Names.SourceOrganization_Help,
        EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),

        FactoryUrl: "/api/ai/agentcontext/sourceorg/factory",

        ClusterKey: "integration", ModelType: EntityDescriptionAttribute.ModelTypes.Integration, Shape: EntityDescriptionAttribute.EntityShapes.Entity,
        Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime, Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true,
        IndexTier: EntityDescriptionAttribute.IndexTiers.Secondary, IndexPriority: 65, IndexTagsCsv: "ai,integration,source-control")]

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
