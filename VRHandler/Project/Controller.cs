using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using System;

namespace HelloWorld
{
    public interface IProgressService
    {
        Task<bool> StartSession(IExecutionContext ctx);
        Task<AddScoreResult> AddScore(IExecutionContext ctx, ScoreEventData data);
        Task<bool> EndSession(IExecutionContext ctx);
    }

    public interface INotificationService
    {
        Task<string> SendPlayerMessage(IExecutionContext context, string message, string messageType, string playerId);
        Task<string> SendProjectMessage(IExecutionContext context, string message, string messageType);
    }

    public class ScoreEventData
    {
        [JsonProperty("hoopId")]
        public int HoopId { get; set; }

        [JsonProperty("hoopScore")]
        public int HoopScore { get; set; }

        [JsonProperty("eventTime")]
        public long EventTime { get; set; }
    }

    public class AddScoreResult
    {
        public double Score { get; set; }
        public int Rank { get; set; }
    }

    public class VRHandlerModule
    {
        private readonly ILogger<VRHandlerModule> _logger;
        private readonly IProgressService _progressService;
        private readonly INotificationService _notificationService;

        public VRHandlerModule(ILogger<VRHandlerModule> logger, IProgressService progressService, INotificationService notificationService)
        {
            _logger = logger;
            _progressService = progressService;
            _notificationService = notificationService;
        }

        [CloudCodeFunction("SayHello")]
        public string Hello(string name)
        {
            _logger.LogInformation("Hello {Name}", name);
            return $"Hello, {name}!";
        }

        [CloudCodeFunction("PlayerRegistered")] // TODO: this function is called by the auth trigger
        public async Task<string> PlayerRegistered(IExecutionContext context, string playerId)
        {
            _logger.LogInformation("Player {PlayerId} registered", playerId);
            await _notificationService.SendPlayerMessage(context, "Welcome to the game!", "Welcome", playerId);
            return "Player message sent";
        }

        [CloudCodeFunction("StartGame")]
        public async Task<string> StartGame(IExecutionContext context, string playerId)
        {
            _logger.LogInformation("Player {PlayerId} started the game", playerId);
            // TODO: reset player's sessionScore and sessionTime
            await _progressService.StartSession(context);
            return "ok";
        }

        [CloudCodeFunction("EndGame")]
        public async Task<string> EndGame(IExecutionContext context, string playerId)
        {
            _logger.LogInformation("Player {PlayerId} ended the game", playerId);
            // TODO: validate player's score
            // TODO: submit player's sessionScore to leaderboard
            return "ok";
        }

        [CloudCodeFunction("PlayerLoggedIn")]
        public async Task<string> PlayerLoggedIn(IExecutionContext context, string playerId, string lastLoginAt)
        {
            Thread.Sleep(9000);
            _logger.LogInformation("Player {PlayerId} logged in at {LastLoginAt}", playerId, lastLoginAt);
            await _notificationService.SendPlayerMessage(context, $"Welcome back! You last logged in at {lastLoginAt}", "WelcomeBack", playerId);
            return "Player message sent";
        }

        [CloudCodeFunction("SendAnnouncement")]
        public async Task<string> SendAnnouncement(IExecutionContext context, string message)
        {
            _logger.LogInformation("Sending announcement: {Message}", message);
            await _notificationService.SendProjectMessage(context, message, "");
            return "Project message sent";
        }

        [CloudCodeFunction("ScoreAdded")]
        public async Task<string> ScoreAdded(IExecutionContext context)
        {
            _logger.LogInformation("Leaderboard updated");
            await _notificationService.SendProjectMessage(context, "update-leaderboard", "");
            return "Update leaderboard message sent";
        }

        [CloudCodeFunction("AddScore")]
        public async Task<double> AddScore(IExecutionContext context, int hoopId, long eventTime, int hoopScore)
        {
            _logger.LogInformation("Adding Score of {Score} at id: {HoopId}", hoopScore, hoopId);
            try
            {
                var result = await _progressService.AddScore(context, new ScoreEventData { HoopId = hoopId, EventTime = eventTime, HoopScore = hoopScore });

                if (result.Rank <= 10)
                {
                    await ScoreAdded(context);
                }
                return result.Score;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failure when adding score: {Err}", ex.ToString());
                throw;
            }

        }
    }

    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.Dependencies.AddSingleton(PushClient.Create());
            config.Dependencies.AddSingleton(GameApiClient.Create());
            config.Dependencies.AddSingleton<IProgressService, VRProgressService>();
            config.Dependencies.AddSingleton<INotificationService, VRNotificationService>();
        }
    }
}

