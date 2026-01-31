// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 69897ad6891a70ec4665234984d46bfd6745f9b683ddb9871a900a614000ea7e
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public enum MergeMethod
    {
        [EnumLabel(SourceOrganizationRepo.MergeMethod_Merge, AIResources.Names.MergeMethod_Merge, typeof(AIResources))]
        Merge,
        [EnumLabel(SourceOrganizationRepo.MergeMethod_Squash, AIResources.Names.MergeMethod_Squash, typeof(AIResources))]
        Squash,
        [EnumLabel(SourceOrganizationRepo.MergeMethod_Rebase, AIResources.Names.MergeMethod_Rebase, typeof(AIResources))]
        Rebase
    }

    public enum RespositoryType
    {
    }


    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.SourceOrganizationRepository_Title, AIResources.Names.SourceOrganizationRepository_Help,
        AIResources.Names.SourceOrganizationRepository_Help, EntityDescriptionAttribute.EntityTypes.ChildObject, typeof(AIResources),

        FactoryUrl: "/api/ai/agentcontext/sourceorg/repo/factory",

        ClusterKey: "integration", ModelType: EntityDescriptionAttribute.ModelTypes.Integration, Shape: EntityDescriptionAttribute.EntityShapes.Entity,
        Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime, Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true,
        IndexTier: EntityDescriptionAttribute.IndexTiers.Secondary, IndexPriority: 65, IndexTagsCsv: "ai,integration,source-control")]
    public class SourceOrganizationRepo : IValidateable, IFormDescriptor
    {
        public const string MergeMethod_Merge = "merge";
        public const string MergeMethod_Squash = "squash";
        public const string MergeMethod_Rebase = "rebase";

        public const string UserInterface = "userinterface";
        public const string BackEnd = "backend";

        public string Id { get; set; } = Guid.NewGuid().ToId();

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganizationRepository_RepoPath, FieldType: FieldTypes.Text, HelpResource: AIResources.Names.SourceOrganizationRepository_RepoPath_Help, ResourceType: typeof(AIResources))]
        public string RepoPath { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganizationRepository_MergeMethod, EnumType:typeof(MergeMethod), WaterMark:AIResources.Names.SourceOrganizationRepository_MergeMethod_Select, FieldType: FieldTypes.Picker,  ResourceType: typeof(AIResources))]
        public EntityHeader<MergeMethod> DefaultMergeMethod { get; set; }

        [FormField(LabelResource: AIResources.Names.SourceOrganizationRepository_DeleteOnMerge, FieldType: FieldTypes.Text, HelpResource:AIResources.Names.SourceOrganizationRepository_DeleteOnMerge_Help,  ResourceType: typeof(AIResources))]
        public bool DeleteOnMerge { get; set; } = true;

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(RepoPath),
                nameof(DefaultMergeMethod),
                nameof(DeleteOnMerge),
            };
        }
    }
}
