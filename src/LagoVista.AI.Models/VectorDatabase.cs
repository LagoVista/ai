using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.VectorDatabase_Title, AIResources.Names.VectorDatabase_Description, AIResources.Names.VectorDatabase_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        GetUrl: "/api/ml/vectordb/{id}", GetListUrl: "/api/ml/vectordbs", FactoryUrl: "/api/ml/vectordb/factory", SaveUrl: "/api/ml/vectordb", DeleteUrl: "/api/ml/vectordb/{id}",
        ListUIUrl: "/mlworkbench/vectordbs", EditUIUrl: "/mlworkbench/vectordb/{id}", CreateUIUrl: "/mlworkbench/vectordb/add", Icon: "icon-ae-database-3")]
    public class VectorDatabase : EntityBase, IFormDescriptor, ISummaryFactory, IFormConditionalFields, IValidateable
    {
        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; } = "icon-ae-database-3";

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_CollectionName, FieldType: FieldTypes.Text, IsRequired:true, ResourceType: typeof(AIResources))]
        public string CollectionName { get; set; }

        public string VectorDatabaseApiKeySecretId { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_ApiKey, FieldType: FieldTypes.Secret, SecureIdFieldName:nameof(VectorDatabaseApiKeySecretId), ResourceType: typeof(AIResources))]
        public string VectorDatabaseApiKey { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_Uri, IsRequired: true, FieldType: FieldTypes.WebLink,  ResourceType: typeof(AIResources))]
        public string VectorDatabaseUri { get; set; }

        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureAccountId, HelpResource: AIResources.Names.VectorDatabase_AzureAccountId_Help, IsRequired: true, FieldType: FieldTypes.Text, ResourceType: typeof(AIResources))]
        public string AzureAccountId { get; set; }

        public string AzureApiTokenSecretid { get; set; }
        [FormField(LabelResource: AIResources.Names.VectorDatabase_AzureApiToken, HelpResource: AIResources.Names.VectorDatabase_AzureApiToken_Help, SecureIdFieldName:nameof(AzureApiTokenSecretid), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string AzureApiToken { get; set; }

        public string OpenAIApiKeySecretId { get; set; }


        [FormField(LabelResource: AIResources.Names.VectorDatabase_OpenAPI_Token, HelpResource: AIResources.Names.VectorDatabase_OpenAPI_Token_Help, SecureIdFieldName:nameof(OpenAIApiKeySecretId), FieldType: FieldTypes.Secret, ResourceType: typeof(AIResources))]
        public string OpenAIApiKey { get; set; }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return this.CreateSummary();
        }

        public FormConditionals GetConditionalFields()
        {
            return new FormConditionals()
            {
                Conditionals = new List<FormConditional>()
                {
                    new FormConditional()
                    {
                        ForCreate = true,
                        ForUpdate = false,
                        RequiredFields = new List<string>() { nameof(VectorDatabaseApiKey), nameof(AzureApiToken), nameof(OpenAIApiKey) } }
                }
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Icon),
                nameof(CollectionName),
                nameof(VectorDatabaseUri),
                nameof(VectorDatabaseApiKey),
                nameof(AzureAccountId),
                nameof(AzureApiToken),
                nameof(OpenAIApiKey),
                nameof(Description)
            };
        }

        public VectorDatabaseSummary CreateSummary()
        {
            var db = new VectorDatabaseSummary();
            db.Populate(this);
            return db;
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.VectorDatabases_Title, AIResources.Names.VectorDatabase_Description, AIResources.Names.VectorDatabase_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
     GetUrl: "/api/ml/vectordb/{id}", GetListUrl: "/api/ml/vectordbs", FactoryUrl: "/api/ml/vectordb/factory", SaveUrl: "/api/ml/vectordb", DeleteUrl: "/api/ml/vectordb/{id}",
     ListUIUrl: "/mlworkbench/vectordbs", EditUIUrl: "/mlworkbench/vectordb/{id}", CreateUIUrl: "/mlworkbench/vectordb/add", Icon: "icon-ae-database-3")]
    public class VectorDatabaseSummary : SummaryData
    {

    }
}
