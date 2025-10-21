using System.Collections.Generic;
using System.Threading.Tasks;
using AgentManagerPlugin.Models;

namespace AgentManagerPlugin.Services
{
    public interface IAgentApiClient
    {
        Task<AgentInfo> AddPilotAsync(PilotConfig config);
        Task<bool> RemovePilotAsync(string pilotId);
        Task<AgentInfo> StartMissionControlAsync(MissionControlConfig config);
        Task<bool> StopMissionControlAsync(string mcId);
        Task<List<AgentInfo>> GetActiveAgentsAsync();
        Task<AgentStatus> GetAgentStatusAsync(string agentId);
        Task<List<CertificateInfo>> GetCertificatesAsync();
        Task<ServerConfig> GetServerConfigAsync();
        Task<bool> IsApiAvailableAsync();
    }
}
