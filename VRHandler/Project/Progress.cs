using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System;

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
        IGameApiClient _apiClient;

        const string dailyHoopCountKey = "daily-hoop-count";
        const string leaderboardId = "scores";

        public VRProgressService(IGameApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<int> AddScore(IExecutionContext ctx, ScoreEventData data)
        {
            var rcResult = _apiClient.RemoteConfigSettings.AssignSettingsGetAsync(ctx, ctx.AccessToken, ctx.ProjectId,
                            ctx.ProjectId, null, new List<string> { "progressXP", "spawnDelay", "hoops" });

            var settings = rcResult.Result.Data.Configs.Settings;

            var xp = settings["progressXP"];
            var tick = settings["spawnDelay"];
            var hoops = JsonSerializer.Deserialize<Hoop[]>((string)settings["hoops"]);
            int score = 0;

            if (hoops == null || hoops.Length <= 0)
            {
                return 0;
            }

            foreach (var item in hoops)
            {
                if (item.ID == data.HoopId)
                {
                    score = item.Score;
                    break;
                }
            }

            var csResult = await _apiClient.CloudSaveData.GetItemsAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new List<string> { dailyHoopCountKey });
            var csItem = csResult.Data.Results.First();
            var currentDailyHoopCount = 0;

            if (csItem != null)
            {
                var lastModified = csItem.Modified;
                // Check whether the last value was set today in order to increment
                if (lastModified.Date?.Date == DateTime.Today)
                {
                    currentDailyHoopCount = (int)csItem.Value;
                }
            }

            await _apiClient.CloudSaveData.SetItemAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new SetItemBody(dailyHoopCountKey, currentDailyHoopCount + 1));

            var addScoreResult = await _apiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                ctx, ctx.AccessToken, Guid.Parse(ctx.ProjectId), leaderboardId, ctx.PlayerId, new LeaderboardScore(score));

            return (int)addScoreResult.Data.Score;
        }
    }
}