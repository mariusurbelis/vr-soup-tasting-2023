using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelloWorld
{
    public class Hoop
    {
        [JsonPropertyName("id")] public int ID { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }

        [JsonPropertyName("x")] public int PosX { get; set; }
        [JsonPropertyName("y")] public int PosY { get; set; }
        [JsonPropertyName("z")] public int PosZ { get; set; }
    }

    public class VRProgressService : IProgressService
    {
        PushClient _pushClient;
        IGameApiClient _apiClient;
        ILogger<VRHandlerModule> _logger;
        
        public VRProgressService(ILogger<VRHandlerModule> logger, IGameApiClient apiClient, PushClient pushClient)
        {
            _pushClient = pushClient;
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task<int> AddScore(IExecutionContext ctx, ScoreEventData data)
        {
            var result = _apiClient.RemoteConfigSettings.AssignSettingsGetAsync(ctx, ctx.AccessToken, ctx.ProjectId,
                            ctx.ProjectId, null, new List<string> { "progressXP", "spawnDelay", "hoops" });

            var settings = result.Result.Data.Configs.Settings;

            var xp = settings["progressXP"];
            var tick = settings["spawnDelay"];
            var hoops = JsonSerializer.Deserialize<Hoop[]>((string)settings["hoops"]);
            int score = 0;

            foreach (var item in hoops)
            {
                if (item.ID == data.HoopId) {
                    score = item.Score;
                    break;
                }
            }

            return score;
        }
    }
}