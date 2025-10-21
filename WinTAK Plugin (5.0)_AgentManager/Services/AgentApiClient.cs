using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    public class AgentApiClient : IAgentApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public AgentApiClient(string baseUrl = "http://localhost:8000/api/v1")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/config/server");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ServerConfig> GetServerConfigAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/config/server");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ServerConfig>(json);
        }

        public async Task<List<CertificateInfo>> GetCertificatesAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/certificates");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<CertificateInfo>>(json);
        }

        public async Task<AgentInfo> AddPilotAsync(PilotConfig config)
        {
            var json = JsonConvert.SerializeObject(new
            {
                vehicle_id = config.VehicleId,
                cert_name = config.CertName,
                connection_port = config.ConnectionPort,
                vehicle_type = config.VehicleType,
                altitude = config.Altitude
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/pilots", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<AgentInfo>(responseJson);
        }

        public async Task<bool> RemovePilotAsync(string pilotId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/pilots/{pilotId}");
            response.EnsureSuccessStatusCode();
            return true;
        }

        public async Task<AgentInfo> StartMissionControlAsync(MissionControlConfig config)
        {
            var json = JsonConvert.SerializeObject(new
            {
                cert_name = config.CertName
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/mission-control", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<AgentInfo>(responseJson);
        }

        public async Task<bool> StopMissionControlAsync(string mcId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/mission-control/{mcId}");
            response.EnsureSuccessStatusCode();
            return true;
        }

        public async Task<List<AgentInfo>> GetActiveAgentsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/pilots");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<AgentInfo>>(json);
        }

        public async Task<AgentStatus> GetAgentStatusAsync(string agentId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/pilots/{agentId}/status");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<AgentStatus>(json);
        }
    }
}
