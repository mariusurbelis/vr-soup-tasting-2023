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

        const string configKeySessionLength = "sessionLength";
        const string configKeyHoops = "hoops";
        const string configKeyProgressXP = "progressXP";

        const string progressXPKey = "progressXP";
        const string dailyHoopCountKey = "dailyHoopScores";
        const string sessionStartKey = "sessionStart";
        const string sessionScoreKey = "sessionScore";
        const string leaderboardId = "scores";

        public VRProgressService(IGameApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        private async Task AwaitBatch(IEnumerable<Func<Task>> tasks)
        {
            try
            {
                var tasksToRun = tasks.Select(async fn => await fn());
                await Task.WhenAll(tasksToRun);
            }
            catch (AggregateException ex)
            {
                foreach (var innerEx in ex.InnerExceptions)
                {
                    //_logger.LogError("Failed to execute task {Task} in the batch. Error: {Error}", innerEx.Source, innerEx.Message);
                }

                throw new Exception($"Failed to execute a task in the batch. Error: {ex.Message}");
            }
        }

        public async Task<bool> StartSession(IExecutionContext context)
        {
            var csGetTaskResults = new List<Item>();
            float sessionLength = 0;

            async Task GetRemoteConfig()
            {
                var rcResult = await _apiClient.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId,
                                context.EnvironmentId, null, new List<string> { configKeySessionLength });
                var settings = rcResult.Data.Configs.Settings;
                sessionLength = Convert.ToSingle(settings[configKeySessionLength]);
            }

            async Task GetCloudSaveData()
            {
                var csGetTask = await _apiClient.CloudSaveData.GetItemsAsync(context, context.AccessToken, context.ProjectId,
                    context.PlayerId, new List<string> { sessionStartKey });
                csGetTaskResults = csGetTask.Data.Results;
            }

            await AwaitBatch(new List<Func<Task>>() { GetCloudSaveData, GetRemoteConfig });

            var sessionStartItem = csGetTaskResults.Find((item) => item.Key == sessionStartKey);

            if (sessionStartItem != null)
            {
                var lastModified = sessionStartItem.Modified;
                if (DateTime.Now.AddMinutes(sessionLength) > lastModified.Date?.Date)
                {
                    return false;
                }
            }

            var csUpdateTask = await _apiClient.CloudSaveData.SetItemBatchAsync(
                context, context.AccessToken, context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(sessionStartKey, DateTime.Now.ToString()),
                }
            ));

            return true;
        }

        public async Task<bool> EndSession(IExecutionContext context)
        {
            var csGetTaskResults = new List<Item>();
            var sessionScore = 0;

            var csGetTask = await _apiClient.CloudSaveData.GetItemsAsync(context, context.AccessToken, context.ProjectId,
                    context.PlayerId, new List<string> { sessionStartKey, sessionScoreKey });
            csGetTaskResults = csGetTask.Data.Results;

            var sessionScoreItem = csGetTaskResults.Find((item) => item.Key == sessionScoreKey);

            if (sessionScoreItem != null)
            {
                sessionScore = Convert.ToInt32(sessionScoreItem.Value);
            }

            if (sessionScore > 0)
            {
                var lbUpdateTask = await _apiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                    context, context.AccessToken, Guid.Parse(context.ProjectId), leaderboardId, context.PlayerId, new LeaderboardScore(sessionScore));
            }

            return true;
        }


        public async Task<bool> AddScore(IExecutionContext context, ScoreEventData data)
        {
            var csGetTaskResults = new List<Item>();
            float sessionLength = 0;
            var hoops = new List<Hoop>();
            int sessionScore = 0;
            int hoopScore = 0;
            float progressXP = 0;
            float currentProgressXP = 0;
            var currentDailyHoopCount = 0;
            var currentSessionScore = 0;

            async Task GetRemoteConfig()
            {
                var rcResult = await _apiClient.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId,
                                context.EnvironmentId, null, new List<string> { configKeySessionLength, configKeyHoops, configKeyProgressXP });
                var settings = rcResult.Data.Configs.Settings;
                sessionLength = Convert.ToSingle(settings[configKeySessionLength]);
                progressXP = Convert.ToSingle(settings[configKeyProgressXP]);
                hoops = JsonSerializer.Deserialize<List<Hoop>>(settings[configKeyHoops].ToString());
            }

            async Task GetCloudSaveData()
            {
                var csGetTask = await _apiClient.CloudSaveData.GetItemsAsync(context, context.AccessToken, context.ProjectId,
                    context.PlayerId, new List<string> { sessionStartKey, sessionScoreKey, dailyHoopCountKey });
                csGetTaskResults = csGetTask.Data.Results;
            }

            await AwaitBatch(new List<Func<Task>>() { GetCloudSaveData, GetRemoteConfig });

            var currentHoop = hoops.Find(h => h.ID == data.HoopId);
            if (currentHoop == null)
            {
                return false;
            }
            hoopScore = currentHoop.Score;

            if (csGetTaskResults.Count > 0)
            {
                var dailyHoopCountItem = csGetTaskResults.Find((item) => item.Key == dailyHoopCountKey);
                var progressXPItem = csGetTaskResults.Find((item) => item.Key == progressXPKey);
                var sessionScoreItem = csGetTaskResults.Find((item) => item.Key == sessionScoreKey);
                var sessionStartItem = csGetTaskResults.Find((item) => item.Key == sessionStartKey);

                if (sessionStartItem != null)
                {
                    var lastModified = sessionStartItem.Modified;
                    if (DateTime.Now.AddMinutes(sessionLength) > lastModified.Date?.Date)
                    {
                        return false;
                    }
                }

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

                if (sessionScoreItem != null)
                {
                    sessionScore = Convert.ToInt32(sessionScoreItem.Value);
                }
            }

            var csUpdateTask = await _apiClient.CloudSaveData.SetItemBatchAsync(
                context, context.AccessToken, context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(dailyHoopCountKey, currentDailyHoopCount + 1),
                    new(progressXPKey, currentProgressXP + progressXP),
                    new(sessionScoreKey, currentSessionScore + sessionScore),
                }
            ));

            return true;
        }
    }
}