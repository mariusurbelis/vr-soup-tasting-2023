using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<VRHandlerModule> _logger;


        const string configKeySessionLength = "sessionLength";
        const string configKeyHoops = "hoops";
        const string configKeyProgressXP = "progressXP";

        const string progressXPKey = "progressXP";
        const string dailyHoopCountKey = "dailyHoopScores";
        const string sessionStartKey = "sessionStart";
        const string sessionScoreKey = "sessionScore";
        const string leaderboardId = "scores";

        public VRProgressService(ILogger<VRHandlerModule> logger, IGameApiClient apiClient)
        {
            _logger = logger;
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

        public async Task<LeaderboardResetResult> LeaderboardReset(IExecutionContext context, string leaderboardId, string leaderboardVersionId)
        {
            var lbQueryTask = await _apiClient.Leaderboards.GetLeaderboardVersionScoresAsync(
                context, context.ServiceToken, Guid.Parse(context.ProjectId), leaderboardId, leaderboardVersionId, null, 1);

            if (lbQueryTask.Data.Results.Count <= 0)
            {
                throw new Exception("No leaderboard entries");
            }

            var entry = lbQueryTask.Data.Results[0];

            // TODO: update the XP on the top player

            return new LeaderboardResetResult
            {
                TopScore = entry.Score,
                PlayerId = entry.PlayerId
            };
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
                var lastModified = sessionStartItem.Modified.Date;
                if (DateTime.Now < lastModified?.AddSeconds(sessionLength))
                {
                    throw new Exception("Session already started");
                }

                _logger.LogDebug($"Expired session, {lastModified}");
            }

            _logger.LogDebug($"New session started, {DateTime.Now}");

            var csUpdateTask = await _apiClient.CloudSaveData.SetItemBatchAsync(
                context, context.AccessToken, context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(sessionStartKey, DateTime.Now.ToString()),
                    new(sessionScoreKey, 0),
                }
            ));

            return true;
        }

        public async Task<EndSessionResult> EndSession(IExecutionContext context)
        {
            var csGetTaskResults = new List<Item>();
            var sessionScore = 0;
            var rank = 0;

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

                rank = lbUpdateTask.Data.Rank;
            }

            return new EndSessionResult
            {
                Score = sessionScore,
                Rank = rank
            };
        }

        public async Task<EndSessionResult> EndSessionWithScores(IExecutionContext context, ScoreEventData[] data)
        {
            var csGetTaskResults = new List<Item>();
            float sessionLength = 0;
            var hoops = new List<Hoop>();
            int sessionScore = 0;
            int hoopCount = 0;
            //float progressXP = 0;
            //float currentProgressXP = 0;
            var currentDailyHoopCount = 0;
            var currentSessionScore = 0;
            DateTime? sessionStart = new DateTime();

            async Task GetRemoteConfig()
            {
                var rcResult = await _apiClient.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId,
                                context.EnvironmentId, null, new List<string> { configKeySessionLength, configKeyHoops });
                var settings = rcResult.Data.Configs.Settings;
                sessionLength = Convert.ToSingle(settings[configKeySessionLength]);
                //progressXP = Convert.ToSingle(settings[configKeyProgressXP]);
                hoops = JsonSerializer.Deserialize<List<Hoop>>(settings[configKeyHoops].ToString());
            }

            async Task GetCloudSaveData()
            {
                var csGetTask = await _apiClient.CloudSaveData.GetItemsAsync(context, context.AccessToken, context.ProjectId,
                    context.PlayerId, new List<string> { sessionStartKey, sessionScoreKey, dailyHoopCountKey, progressXPKey });
                csGetTaskResults = csGetTask.Data.Results;
            }

            await AwaitBatch(new List<Func<Task>>() { GetCloudSaveData, GetRemoteConfig });

            if (csGetTaskResults.Count > 0)
            {
                var dailyHoopCountItem = csGetTaskResults.Find((item) => item.Key == dailyHoopCountKey);
                //var progressXPItem = csGetTaskResults.Find((item) => item.Key == progressXPKey);
                var sessionScoreItem = csGetTaskResults.Find((item) => item.Key == sessionScoreKey);
                var sessionStartItem = csGetTaskResults.Find((item) => item.Key == sessionStartKey);

                if (sessionStartItem == null)
                {
                    throw new Exception("A game session was not started");
                }
                else
                {
                    sessionStart = sessionStartItem.Modified.Date;

                    if (DateTime.Now > sessionStart?.AddSeconds(sessionLength).AddSeconds(5))
                    {
                        throw new Exception("End game session submission expired");
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

                // if (progressXPItem != null)
                // {
                //     currentProgressXP = Convert.ToSingle(progressXPItem.Value);
                // }

                if (sessionScoreItem != null)
                {
                    currentSessionScore = Convert.ToInt32(sessionScoreItem.Value);
                }
            }

            var sessionStartEpochTime = new DateTimeOffset((DateTime)sessionStart).ToUnixTimeMilliseconds();
            var sessionEndEpochTime = new DateTimeOffset((DateTime)(sessionStart?.AddSeconds(sessionLength))).ToUnixTimeMilliseconds();

            foreach (var item in data)
            {
                var currentHoop = hoops.Find(h => h.ID == item.HoopId);
                if (currentHoop == null)
                {
                    throw new Exception($"Hoop with ID {item.HoopId} not found");
                }
                if (currentHoop.Score != item.HoopScore)
                {
                    throw new Exception("Hoop scores do not match");
                }
                if (item.EventTime < sessionStartEpochTime || item.EventTime > sessionEndEpochTime)
                {
                    throw new Exception($"Hoop scored outside session window, eventTime: {item.EventTime}, sessionStart: {sessionStartEpochTime}, sessionEnd: {sessionEndEpochTime}");
                }

                sessionScore += currentHoop.Score;
                hoopCount++;
            }

            var score = currentSessionScore + sessionScore;
            var rank = 0;

            if (score <= 0)
            {
                return new EndSessionResult();
            }

            async Task UpdateLeaderboard()
            {
                var lbUpdateTask = await _apiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                    context, context.AccessToken, Guid.Parse(context.ProjectId), leaderboardId, context.PlayerId, new LeaderboardScore(sessionScore));

                rank = lbUpdateTask.Data.Rank;
            }

            async Task UpdateCloudSaveData()
            {
                await _apiClient.CloudSaveData.SetItemBatchAsync(
                    context, context.AccessToken, context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                        new(dailyHoopCountKey, currentDailyHoopCount + hoopCount),
                        // new(progressXPKey, currentProgressXP + progressXP),
                    }
                ));
            }

            await AwaitBatch(new List<Func<Task>>() { UpdateLeaderboard, UpdateCloudSaveData });

            return new EndSessionResult
            {
                Score = score,
                Rank = rank,
            };
        }

        public async Task<int> AddScore(IExecutionContext context, ScoreEventData data)
        {
            var csGetTaskResults = new List<Item>();
            float sessionLength = 0;
            var hoops = new List<Hoop>();
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
                    context.PlayerId, new List<string> { sessionStartKey, sessionScoreKey, dailyHoopCountKey, progressXPKey });
                csGetTaskResults = csGetTask.Data.Results;
            }

            await AwaitBatch(new List<Func<Task>>() { GetCloudSaveData, GetRemoteConfig });

            var currentHoop = hoops.Find(h => h.ID == data.HoopId);
            if (currentHoop == null)
            {
                throw new Exception($"Hoop with ID {data.HoopId} not found");
            }
            hoopScore = currentHoop.Score;
            if (hoopScore != data.HoopScore)
            {
                throw new Exception("Hoop scores do not match");
            }

            if (csGetTaskResults.Count > 0)
            {
                var dailyHoopCountItem = csGetTaskResults.Find((item) => item.Key == dailyHoopCountKey);
                var progressXPItem = csGetTaskResults.Find((item) => item.Key == progressXPKey);
                var sessionScoreItem = csGetTaskResults.Find((item) => item.Key == sessionScoreKey);
                var sessionStartItem = csGetTaskResults.Find((item) => item.Key == sessionStartKey);

                if (sessionStartItem != null)
                {
                    var lastModified = sessionStartItem.Modified.Date;
                    _logger.LogDebug($"session info, current: {DateTime.Now} started: {lastModified}, ends: {lastModified?.AddSeconds(sessionLength)}");

                    if (lastModified != null && DateTime.Now > lastModified?.AddMinutes(sessionLength))
                    {
                        throw new Exception("Not within session");
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
                    currentSessionScore = Convert.ToInt32(sessionScoreItem.Value);
                }
            }

            var score = currentSessionScore + hoopScore;

            var csUpdateTask = await _apiClient.CloudSaveData.SetItemBatchAsync(
                context, context.AccessToken, context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>{
                    new(dailyHoopCountKey, currentDailyHoopCount + 1),
                    new(progressXPKey, currentProgressXP + progressXP),
                    new(sessionScoreKey, score),
                }
            ));

            return score;
        }
    }
}