using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Logging;

namespace AetherBlackbox.Networking
{
    public class DiscoveryClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private DateTime lastFetchTime = DateTime.MinValue;
        private const int CacheDurationMinutes = 5;

        public DiscoveryClient()
        {
            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new Uri(NetworkManager.ApiBaseUrl);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}
