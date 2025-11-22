using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ManagerDescriptionBuilderTests
    {
        private const string SourceText = @"// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c8a9b0de2a6bf14f063c55f46c52d46ee0035077eb8b79425e721a25b3db6fe3
// IndexVersion: 2
// --- END CODE INDEX META ---
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
    public class AgentContextTestManager : ManagerBase, IAgentContextTestManager
    {
        private readonly IAgentConextRepo _repo;
        private readonly ISecureStorage _secureStorage;

        public AgentContextTestManager(IAgentConextRepo repo, ISecureStorage secureStorage, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new NullReferenceException(nameof(repo));
            _secureStorage = secureStorage ?? throw new NullReferenceException(nameof(secureStorage));
        }

        public async Task<InvokeResult> AddAgentContextTestAsync(Models.AgentContextTest AgentContextTest, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(AgentContextTest, Actions.Create);
            if(String.IsNullOrEmpty(AgentContextTest.VectorDatabaseApiKey) &&
               String.IsNullOrEmpty(AgentContextTest.LlmApiKey) &&
               String.IsNullOrEmpty(AgentContextTest.AzureApiToken))
            {
                return InvokeResult.FromError("At least one of the following secrets must be provided: AzureApiToken, OpenAIApiKey, VectorDatabaseApiKey");
            }

            var addResult = await _secureStorage.AddSecretAsync(org, AgentContextTest.AzureApiToken);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            AgentContextTest.AzureApiTokenSecretId = addResult.Result;
            AgentContextTest.AzureApiToken = null;

            addResult = await _secureStorage.AddSecretAsync(org, AgentContextTest.LlmApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            AgentContextTest.LlmApiKeySecretId = addResult.Result;
            AgentContextTest.LlmApiKey = null;
            
            addResult = await _secureStorage.AddSecretAsync(org, AgentContextTest.VectorDatabaseApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            AgentContextTest.VectorDatabaseApiKeySecretId = addResult.Result;
            AgentContextTest.VectorDatabaseApiKey = null;

            await AuthorizeAsync(AgentContextTest, AuthorizeResult.AuthorizeActions.Create, user, org);
            await _repo.AddAgentContextTestAsync(AgentContextTest);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteAgentContextTestAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetAgentContextTestAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteAgentContextTestAsync(id);
            return InvokeResult.Success;
        }

        public async Task<Models.AgentContextTest> GetAgentContextTestAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAgentContextTestAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<Models.AgentContextTest> GetAgentContextTestWithSecretsAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAgentContextTestAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org, "withSecrets");

            var secret = await _secureStorage.GetSecretAsync(org, model.AzureApiTokenSecretId, user);
            model.AzureApiToken = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContextTest.AzureApiTokenSecretId),"N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.LlmApiKeySecretId, user);
            model.LlmApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContextTest.LlmApiKeySecretId), "N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.VectorDatabaseApiKeySecretId, user);
            model.VectorDatabaseApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContextTest.VectorDatabaseApiKeySecretId), "N/A");
            return model;
        }

        public async Task<Models.AgentContextTest> GetVectorDatabaseAsyncWithSecrets(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAgentContextTestAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<AgentContextTestSummary>> GetAgentContextTestsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.Label));
            return await _repo.GetAgentContextTestSummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<InvokeResult> UpdateAgentContextTestAsync(AgentContextTest db, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(db, Actions.Create);
            await AuthorizeAsync(db, AuthorizeResult.AuthorizeActions.Update, user, org);

            if (!String.IsNullOrEmpty(db.AzureApiToken))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.AzureApiToken);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.AzureApiTokenSecretId = addResult.Result;
                db.AzureApiToken = null;
            }

            if (!String.IsNullOrEmpty(db.LlmApiKey))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.LlmApiKey);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.LlmApiKeySecretId = addResult.Result;
                db.LlmApiKey = null;
            }

            if (!String.IsNullOrEmpty(db.VectorDatabaseApiKey))
            {
                var addResult = await _secureStorage.AddSecretAsync(org, db.VectorDatabaseApiKey);
                if (!addResult.Successful) return addResult.ToInvokeResult();
                db.VectorDatabaseApiKeySecretId = addResult.Result;
                db.VectorDatabaseApiKey = null;
            }

            await _repo.UpdateAgentContextTestAsync(db);
            return InvokeResult.Success;
        }
    }
}";

        [Test]
        public void Builds_Basic_Manager_Metadata_Correctly()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null);
                Assert.That(description.ClassName, Is.EqualTo("AgentContextTestManager"));
                Assert.That(description.Namespace, Is.EqualTo("LagoVista.AI.Managers"));
                Assert.That(description.ManagerType, Is.EqualTo(ManagerType.ManagerOverview));
                Assert.That(description.PrimaryEntity, Is.EqualTo("AgentContextTest"));
            });
        }

        [Test]
        public void Captures_Constructors_And_Dependency_Interfaces()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.That(description.Constructors, Is.Not.Null);
            Assert.That(description.Constructors.Count, Is.EqualTo(1));

            var ctor = description.Constructors.Single();

            Assert.Multiple(() =>
            {
                Assert.That(ctor.SignatureText, Does.Contain("AgentContextTestManager"));
                Assert.That(ctor.SignatureText, Does.Contain("IAgentConextRepo"));
                Assert.That(ctor.BodyText, Does.Contain("_repo = repo"));
                Assert.That(ctor.LineStart, Is.Not.Null);
                Assert.That(ctor.LineEnd, Is.Not.Null);
                Assert.That(ctor.LineEnd, Is.GreaterThanOrEqualTo(ctor.LineStart));
            });

            // DependencyInterfaces should reflect the injected interfaces.
            Assert.That(description.DependencyInterfaces, Is.Not.Null);
            Assert.That(description.DependencyInterfaces, Does.Contain("IAgentConextRepo"));
            Assert.That(description.DependencyInterfaces, Does.Contain("ISecureStorage"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IAdminLogger"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IAppConfig"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IDependencyManager"));
            Assert.That(description.DependencyInterfaces, Does.Contain("ISecurity"));
        }

        [Test]
        public void Discovers_Manager_Methods_And_MethodKinds()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.That(description.Methods, Is.Not.Null);
            Assert.That(description.Methods.Count, Is.EqualTo(7));

            var addMethod = description.Methods.Single(m => m.MethodName == "AddAgentContextTestAsync");
            var deleteMethod = description.Methods.Single(m => m.MethodName == "DeleteAgentContextTestAsync");
            var getMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextTestAsync");
            var getWithSecrets = description.Methods.Single(m => m.MethodName == "GetAgentContextTestWithSecretsAsync");
            var getVectorWithSecrets = description.Methods.Single(m => m.MethodName == "GetVectorDatabaseAsyncWithSecrets");
            var listMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextTestsForOrgAsync");
            var updateMethod = description.Methods.Single(m => m.MethodName == "UpdateAgentContextTestAsync");

            Assert.Multiple(() =>
            {
                Assert.That(addMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Create));
                Assert.That(deleteMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Delete));
                Assert.That(updateMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Update));

                Assert.That(getMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(getWithSecrets.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(getVectorWithSecrets.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(listMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Query));

                Assert.That(addMethod.IsPublic, Is.True);
                Assert.That(deleteMethod.IsPublic, Is.True);
                Assert.That(getMethod.IsPublic, Is.True);
                Assert.That(updateMethod.IsPublic, Is.True);

                Assert.That(addMethod.BodyText, Does.Contain("ValidationCheck"));
                Assert.That(deleteMethod.BodyText, Does.Contain("DeleteAgentContextTestAsync"));
            });
        }

        [Test]
        public void Records_Method_Line_Numbers()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            foreach (var method in description.Methods)
            {
                Assert.That(method.LineStart, Is.Not.Null, $"Method {method.MethodName} should have a LineStart.");
                Assert.That(method.LineEnd, Is.Not.Null, $"Method {method.MethodName} should have a LineEnd.");
                Assert.That(method.LineEnd, Is.GreaterThanOrEqualTo(method.LineStart), $"Method {method.MethodName} has invalid line range.");
            }
        }
    }
}
