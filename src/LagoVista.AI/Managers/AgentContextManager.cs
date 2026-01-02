// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c8a9b0de2a6bf14f063c55f46c52d46ee0035077eb8b79425e721a25b3db6fe3
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class AgentContextManager : ManagerBase, IAgentContextManager
    {
        private readonly IAgentConextRepo _repo;
        private readonly ISecureStorage _secureStorage;

        public AgentContextManager(IAgentConextRepo repo, ISecureStorage secureStorage, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo ?? throw new NullReferenceException(nameof(repo));
            _secureStorage = secureStorage ?? throw new NullReferenceException(nameof(secureStorage));
        }

        public async Task<InvokeResult> AddAgentContextAsync(Models.AgentContext agentContext, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(agentContext, Actions.Create);
            if(String.IsNullOrEmpty(agentContext.VectorDatabaseApiKey) &&
               String.IsNullOrEmpty(agentContext.LlmApiKey) &&
               String.IsNullOrEmpty(agentContext.AzureApiToken))
            {
                return InvokeResult.FromError("At least one of the following secrets must be provided: AzureApiToken, OpenAIApiKey, VectorDatabaseApiKey");
            }

            var addResult = await _secureStorage.AddSecretAsync(org, agentContext.AzureApiToken);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            agentContext.AzureApiTokenSecretId = addResult.Result;
            agentContext.AzureApiToken = null;

            addResult = await _secureStorage.AddSecretAsync(org, agentContext.LlmApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            agentContext.LlmApiKeySecretId = addResult.Result;
            agentContext.LlmApiKey = null;
            
            addResult = await _secureStorage.AddSecretAsync(org, agentContext.VectorDatabaseApiKey);
            if (!addResult.Successful) return addResult.ToInvokeResult();
            agentContext.VectorDatabaseApiKeySecretId = addResult.Result;
            agentContext.VectorDatabaseApiKey = null;

            await AuthorizeAsync(agentContext, AuthorizeResult.AuthorizeActions.Create, user, org);
            AddGeneralMode(agentContext, org, user);
            await _repo.AddAgentContextAsync(agentContext);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteAgentContextAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetAgentContextAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteAgentContextAsync(id);
            return InvokeResult.Success;
        }

        public async Task<Models.AgentContext> GetAgentContextAsync(string id, EntityHeader org, EntityHeader user)
        {
            var agentContext = await _repo.GetAgentContextAsync(id);
            await AuthorizeAsync(agentContext, AuthorizeResult.AuthorizeActions.Read, user, org);

            // Ensure "general" exists as a baseline mode (mirrors orchestrator behavior).
            if (!agentContext.AgentModes.Any(mode => mode.Key == AgentSession.DefaultMode))
            {
                AddGeneralMode(agentContext, org, user);
                await UpdateAgentContextAsync(agentContext, org, user);
            }

            return agentContext;
        }

        private void AddGeneralMode(AgentContext context, EntityHeader org, EntityHeader user)
        {
            var mode = new AgentMode
            {
                Id = Guid.NewGuid().ToId(),
                Key = "general",
                DisplayName = "General Mode",
                Description = "General-purpose assistance for everyday Q&A, explanation, and lightweight help.",
                WhenToUse = "Use this mode for everyday Q&A, explanation, and lightweight assistance.",
                WelcomeMessage = "You are now in General mode. Use this mode for broad questions and lightweight assistance",
                AgentInstructionDdrs = new[]
                {
                    "You are operating in General mode. Provide helpful and accurate responses to a wide range of user queries.",
                    "Focus on clarity and conciseness in your answers.",
                    "If you don't know the answer, admit it rather than making something up."
                },
                BehaviorHints = new[] { "preferConversationalTone" },
                HumanRoleHints = new[] { "The human is seeking general information and assistance." },
                AssociatedToolIds = new[] { "activate_tools", "list_agent_modes" },
                RagScopeHints = Array.Empty<string>(),
                StrongSignals = Array.Empty<string>(),
                WeakSignals = Array.Empty<string>(),
                ExampleUtterances = new[]
                {
                    "Review this PR diff and suggest improvements.",
                    "Does this function handle edge cases?",
                    "Propose a minimal patch to fix naming and add a comment.",
                    "Flag any security issues in this handler."
                },
                IsDefault = true
            };

            context.AgentModes.Add(mode);
        }

        public async Task<Models.AgentContext> GetAgentContextWithSecretsAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAgentContextAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org, "withSecrets");

            var secret = await _secureStorage.GetSecretAsync(org, model.AzureApiTokenSecretId, user);
            model.AzureApiToken = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContext.AzureApiTokenSecretId),"N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.LlmApiKeySecretId, user);
            model.LlmApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContext.LlmApiKeySecretId), "N/A");

            secret = await _secureStorage.GetSecretAsync(org, model.VectorDatabaseApiKeySecretId, user);
            model.VectorDatabaseApiKey = secret.Successful ? secret.Result : throw new RecordNotFoundException(nameof(AgentContext.VectorDatabaseApiKeySecretId), "N/A");
            return model;
        }

        public async Task<Models.AgentContext> GetVectorDatabaseAsyncWithSecrets(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAgentContextAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<AgentContextSummary>> GetAgentContextsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.Label));
            return await _repo.GetAgentContextSummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<InvokeResult> UpdateAgentContextAsync(AgentContext db, EntityHeader org, EntityHeader user)
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

            await _repo.UpdateAgentContextAsync(db);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> AddAgentModeAsync(string agentContextId, AgentMode agentMode, EntityHeader org, EntityHeader user)
        {
            var ctx = await this.GetAgentContextAsync(agentContextId, org, user);
            ctx.AgentModes.Add(agentMode);

            await UpdateAgentContextAsync(ctx, org, user);

            return InvokeResult.Success;
        }
    }
}