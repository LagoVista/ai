﻿using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models.TrainingData
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.TrainingDataSet_Title, AIResources.Names.TrainingDataSet_Help, AIResources.Names.TrainingDataSet_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class TrainingDataSet : IIDEntity, IKeyedEntity, INamedEntity, IDescriptionEntity, IAuditableEntity, IOwnedEntity, INoSQLEntity, IValidateable
    {
        public string DatabaseName { get; set; }
        public string EntityType { get; set; }

        public TrainingDataSet()
        {
            Labels = new List<EntityHeader>();
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Name, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Key, HelpResource: AIResources.Names.Common_Key_Help, FieldType: FieldTypes.Key, RegExValidationMessageResource: AIResources.Names.Common_Key_Validation, ResourceType: typeof(AIResources), IsRequired: true)]
        public String Key { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }
        public EntityHeader CreatedBy { get; set; }
        public EntityHeader LastUpdatedBy { get; set; }
        public bool IsPublic { get; set; }
        public EntityHeader OwnerOrganization { get; set; }
        public EntityHeader OwnerUser { get; set; }

        public List<EntityHeader> Labels { get; set; }

        public TrainingDataSetSummary GetSummary()
        {
            return new TrainingDataSetSummary()
            {
                Id = Id,
                Description = Description,
                IsPublic = IsPublic,
                Key = Key,
                Name = Name
            };
        }
    }

    public class TrainingDataSetSummary : SummaryData
    {

    }
}
