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
using Newtonsoft.Json.Linq;

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

        const string progressXPKey = "progress-xp";
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

            var xp = Convert.ToSingle(settings["progressXP"]);
            var tick = settings["spawnDelay"];
            var hoops = JsonSerializer.Deserialize<List<Hoop>>(settings["hoops"].ToString());
            int score = 0;

            if (hoops == null || hoops.Count <= 0)
            {
                return 0;
            }

            var currentHoop = hoops.Find(h => h.ID == data.HoopId);
            if (currentHoop == null)
            {
                return 0;
            }
            score = currentHoop.Score;

            var currentDailyHoopCount = 0;
            var currentProgressXP = 0;

            var csResult = await _apiClient.CloudSaveData.GetItemsAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new List<string> { dailyHoopCountKey, progressXPKey });

            if (csResult.Data.Results.Count > 0)
            {
                var csItem = csResult.Data.Results.First();
                if (csItem != null)
                {
                    var lastModified = csItem.Modified;
                    // Check whether the last value was set today in order to increment
                    if (lastModified.Date?.Date == DateTime.Today)
                    {
                        currentDailyHoopCount = (int)csItem.Value;
                    }
                }
            }

            var csTask = _apiClient.CloudSaveData.SetItemBatchAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(dailyHoopCountKey, currentDailyHoopCount + 1),
                    new(progressXPKey, currentProgressXP + xp)
                }
                ));

            var lbTask = _apiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                ctx, ctx.AccessToken, Guid.Parse(ctx.ProjectId), leaderboardId, ctx.PlayerId, new LeaderboardScore(score));

            await Task.WhenAll(Task.Run(() => csTask), Task.Run(() => lbTask));

            return (int)lbTask.Result.Data.Score;
        }
    }
}