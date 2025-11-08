// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 572442d7b57da586682178b76051dc3327dd23658f1a2d879709e14bd0683bd7
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public class HubConnector
    {
        IHubManager _hubManager;
        public HubConnector(IHubManager hubManager)
        {
            _hubManager = hubManager;
        }

        public async Task<InvokeResult<TokenResponse>> GetOrgAccessToken(EntityHeader org, EntityHeader user)
        {
            var hub = await _hubManager.GetHubForOrgAsync(org, user);
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue($"token", hub.Result.AccessToken);
            var uri = $"{hub.Result.GetFullUri()}/hub/api/users/{org.Id}/tokens";
            var response = await client.PostAsync(uri, new StringContent(String.Empty));
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
                return InvokeResult<TokenResponse>.Create(tokenResponse);
            }
            else
            {
                return InvokeResult<TokenResponse>.FromError($"Non success code from server: {response.ReasonPhrase}");
            }
        }
    }
}
