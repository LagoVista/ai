// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 7ade203a3cc78b6fc9e6ca5849d8cee4466cb49ee85f28c43d3e27573dbadc66
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public class NotebookConnector
    {
        IHubManager _hubManager;
        public NotebookConnector(IHubManager hubManager)
        {
            _hubManager = hubManager;
        }

        public async Task<ListResponse<Notebook>> GetFilesAsync(EntityHeader org, EntityHeader user)
        {
            var hub = await _hubManager.GetHubForOrgAsync(org, user);

            var uri = $"{hub.Result.GetFullUri()}/user/{org.Id}/api/contents/nuviot?type=directory&_=1535292903800&token={hub.Result.AccessToken}";
            var userToken = "68d425712f6c4cb89d4ce3b2a4c269d5";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue($"token", userToken);
            var json = await client.GetStringAsync(uri);
            Console.WriteLine(json);
            var files = JsonConvert.DeserializeObject<List<Notebook>>(json);
            return ListResponse<Notebook>.Create(files);
        }
    }
}
