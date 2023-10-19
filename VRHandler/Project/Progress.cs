using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using Unity.Services.CloudCode.Shared;

namespace HelloWorld
{
    public class Hoop
    {
        [JsonPropertyName("id")] public int ID { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }

        [JsonPropertyName("x")] public float PosX { get; set; }
        [JsonPropertyName("y")] public float PosY { get; set; }
        [JsonPropertyName("z")] public float PosZ { get; set; }
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

        public async Task<AddScoreResult> AddScore(IExecutionContext ctx, ScoreEventData data)
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
                return new AddScoreResult();
            }

            var currentHoop = hoops.Find(h => h.ID == data.HoopId);
            if (currentHoop == null)
            {
                return new AddScoreResult();
            }
            score = currentHoop.Score;

            double currentScore = 0;
            var currentDailyHoopCount = 0;
            float currentProgressXP = 0;

            var csGetTask = _apiClient.CloudSaveData.GetItemsAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new List<string> { dailyHoopCountKey, progressXPKey });
            var lbGetTask = _apiClient.Leaderboards.GetLeaderboardPlayerScoreAsync(
                ctx, ctx.AccessToken, Guid.Parse(ctx.ProjectId), leaderboardId, ctx.PlayerId);


            var getTask = Task.WhenAll(Task.Run(() => csGetTask), Task.Run(() => lbGetTask));

            try
            {
                await getTask;
            }
            catch (ApiException ex)
            {
                // TODO: handle the leaderboard 404 API exception on first submit
            }

            if (csGetTask.Result.Data.Results.Count > 0)
            {
                var dailyHoopCountItem = csGetTask.Result.Data.Results.Find((item) => item.Key == dailyHoopCountKey);
                var progressXPItem = csGetTask.Result.Data.Results.Find((item) => item.Key == progressXPKey);

                if (dailyHoopCountItem != null)
                {
                    var lastModified = dailyHoopCountItem.Modified;
                    // Check whether the last value was set today in order to increment
                    if (lastModified.Date?.Date == DateTime.Today)
                    {
                        currentDailyHoopCount = Convert.ToInt32(dailyHoopCountItem.Value);
                    }
                }

                if (progressXPItem != null)
                {
                    currentProgressXP = Convert.ToSingle(progressXPItem.Value);
                }
            }

            if (lbGetTask.Result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                currentScore = lbGetTask.Result.Data.Score;
            }

            var csUpdateTask = _apiClient.CloudSaveData.SetItemBatchAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(dailyHoopCountKey, currentDailyHoopCount + 1),
                    new(progressXPKey, currentProgressXP + xp)
                }
                ));

            var lbUpdateTask = _apiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                ctx, ctx.AccessToken, Guid.Parse(ctx.ProjectId), leaderboardId, ctx.PlayerId, new LeaderboardScore(currentScore + score));

            await Task.WhenAll(Task.Run(() => csUpdateTask), Task.Run(() => lbUpdateTask));

            return new AddScoreResult
            {
                Score = lbUpdateTask.Result.Data.Score,
                Rank = lbUpdateTask.Result.Data.Rank
            };
        }
    }
}