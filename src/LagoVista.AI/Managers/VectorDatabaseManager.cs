using LagoVista.AI.Models;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class VectorDatabaseManager : ManagerBase, IVectorDatabaseManager
    {
        private readonly IVectorDatabaseRepo _repo;
        private readonly ISecureStorage _secureStorage;

        public VectorDatabaseManager(IVectorDatabaseRepo repo, ISecureStorage secureStorage, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new NullReferenceException(nameof(repo));
            _secureStorage = secureStorage ?? throw new NullReferenceException(nameof(secureStorage));
        }

        public async Task<InvokeResult> AddVectorDatabaseAsync(Models.VectorDatabase db, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(db, Actions.Create);
            if(String.IsNullOrEmpty(db.VectorDatabaseApiKey) &&
               String.IsNullOrEmpty(db.OpenAIApiKey) &&
               String.IsNullOrEmpty(db.AzureApiToken))
            {
                return InvokeResult.FromError("At least one of the following secrets must be provided: AzureApiToken, OpenAIApiKey, VectorDatabaseApiKey");
            }

            var addResult = await _secureStorage.AddSecretAsync(org, db.AzureApiToken);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            db.AzureApiTokenSecretid = addResult.Result;
            db.AzureApiToken = null;

            addResult = await _secureStorage.AddSecretAsync(org, db.OpenAIApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            db.OpenAIApiKeySecretId = addResult.Result;
            db.OpenAIApiKey = null;
            
            addResult = await _secureStorage.AddSecretAsync(org, db.VectorDatabaseApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            db.VectorDatabaseApiKeySecretId = addResult.Result;
            db.VectorDatabaseApiKey = null;

            await AuthorizeAsync(db, AuthorizeResult.AuthorizeActions.Create, user, org);
            await _repo.AddVectorDatabaseAsync(db);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteVectorDatabaseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetVectorDatabaseAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteVectorDatabaseAsync(id);
            return InvokeResult.Success;
        }

        public async Task<Models.VectorDatabase> GetVectorDatabaseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetVectorDatabaseAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<Models.VectorDatabase> GetVectorDatabaseWithSecretsAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetVectorDatabaseAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org, "withSecrets");

            var secret = await _secureStorage.GetSecretAsync(org, model.AzureApiTokenSecretid, user);
            model.AzureApiToken = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(VectorDatabase.AzureApiTokenSecretid),"N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.OpenAIApiKeySecretId, user);
            model.OpenAIApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(VectorDatabase.OpenAIApiKeySecretId), "N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.VectorDatabaseApiKeySecretId, user);
            model.VectorDatabaseApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(VectorDatabase.VectorDatabaseApiKeySecretId), "N/A");

            return model;
        }

        public async Task<Models.VectorDatabase> GetVectorDatabaseAsyncWithSecrets(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetVectorDatabaseAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<VectorDatabaseSummary>> GetVectorDatabasesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.Label));
            return await _repo.GetVectorDatabasesForOrgAsync(org.Id, listRequest);
        }

        public async Task<InvokeResult> UpdateVectorDatabaseAsync(VectorDatabase db, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(db, Actions.Create);
            await AuthorizeAsync(db, AuthorizeResult.AuthorizeActions.Update, user, org);

            if (!String.IsNullOrEmpty(db.AzureApiToken))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.AzureApiToken);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.AzureApiTokenSecretid = addResult.Result;
                db.AzureApiToken = null;
            }

            if (!String.IsNullOrEmpty(db.OpenAIApiKey))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.OpenAIApiKey);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.OpenAIApiKeySecretId = addResult.Result;
                db.OpenAIApiKey = null;
            }

            if (!String.IsNullOrEmpty(db.VectorDatabaseApiKey))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.VectorDatabaseApiKey);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.VectorDatabaseApiKeySecretId = addResult.Result;
                db.VectorDatabaseApiKey = null;
            }

            await _repo.UpdateVectorDatabaseAsync(db);
            return InvokeResult.Success;
        }
    }
}