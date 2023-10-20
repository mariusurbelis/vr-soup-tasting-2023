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
using System.Linq;

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

        public interface ITask<T>
        {
            Task<T> Do();
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
            var csGetTaskResults = new List<Item>();

            var tasksFns = new List<Func<Task>>() {
                new Func<Task>(async () =>
                {
                    var csGetTask = await _apiClient.CloudSaveData.GetItemsAsync(ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new List<string> { dailyHoopCountKey, progressXPKey });
                    csGetTaskResults = csGetTask.Data.Results;
                    return;
                }),
                new Func<Task>(async () =>
                {
                    var lbGetTask = await _apiClient.Leaderboards.GetLeaderboardPlayerScoreAsync(ctx, ctx.AccessToken, Guid.Parse(ctx.ProjectId), leaderboardId, ctx.PlayerId);
                    currentScore = lbGetTask.Data.Score;
                    return;
                })
            };

            var tasks = tasksFns.Select(async p =>
            {
                try
                {
                    await p();
                }
                catch (Exception ex)
                {
                    // TODO: Handle the exceptions
                    // Leaderboards request will throw an API Exception if the player has set to submit a score
                }
                return Task.CompletedTask;
            });

            // TODO: Attempted to implement something following https://thesharperdev.com/csharps-whenall-and-exception-handling/ though the attempt feels a little ugly
            // The idea here is to do a number of async requests concurrently, but as some can throw an exception we want to check whether
            // the exception can be ignored or we need to rethrow it.
            await Task.WhenAll(tasks);

            if (csGetTaskResults.Count > 0)
            {
                var dailyHoopCountItem = csGetTaskResults.Find((item) => item.Key == dailyHoopCountKey);
                var progressXPItem = csGetTaskResults.Find((item) => item.Key == progressXPKey);

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