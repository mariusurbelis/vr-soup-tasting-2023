using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;

namespace HelloWorld
{
    public interface IProgressService
    {
        Task<int> AddScore(IExecutionContext ctx, ScoreEventData data);
    }

    public interface INotificationService
    {
        Task<string> SendPlayerMessage(IExecutionContext context, string message, string messageType, string playerId)
        Task<string> SendProjectMessage(IExecutionContext context, string message, string messageType)
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

        [CloudCodeFunction("PlayerLoggedIn")]
        public async Task<string> PlayerLoggedIn(IExecutionContext context, string playerId, string lastLoginAt)
        {
            Thread.Sleep(9000);
            _logger.LogInformation("Player {PlayerId} logged in at {LastLoginAt}", playerId, lastLoginAt);
            await _notificationService.SendPlayerMessage(context, pushClient, $"Welcome back! You last logged in at {lastLoginAt}", "WelcomeBack", playerId);
            return "Player message sent";
        }

        [CloudCodeFunction("SendAnnouncement")]
        public async Task<string> SendAnnouncement(IExecutionContext context, string message)
        {
            _logger.LogInformation("Sending announcement: {Message}", message);
            await _notificationService.SendProjectMessage(context, pushClient, message, "");
            return "Project message sent";
        }

        [CloudCodeFunction("AddScore")]
        public async Task<int> AddScore(IExecutionContext context, ScoreEventData data)
        {
            _logger.LogInformation("Adding Score", data.HoopId);
            return await _progressService.AddScore(context, data);
        }
    }

    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.Dependencies.AddSingleton(PushClient.Create());
            config.Dependencies.AddSingleton(GameApiClient.Create());
            config.Dependencies.AddSingleton<IProgressService, VRProgressService>();
            config.Dependencies.AddSingleton<INotificationService, VRProgressService>();
        }
    }
}

