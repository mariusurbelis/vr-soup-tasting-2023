using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;

namespace HelloWorld
{
    public class VRHandlerModule
    {
        
        private readonly ILogger<VRHandlerModule> _logger;
        public VRHandlerModule(ILogger<VRHandlerModule> logger)
        {
            _logger = logger;
        }
        
        
        [CloudCodeFunction("SayHello")]
        public string Hello(string name)
        {
            _logger.LogInformation("Hello {Name}", name);
            return $"Hello, {name}!";
        }
        
        [CloudCodeFunction("PlayerLoggedIn")]
        public async Task<string> PlayerLoggedIn(IExecutionContext context, PushClient pushClient, string playerId, string lastLoginAt)
        {
            Thread.Sleep(9000);
            _logger.LogInformation("Player {PlayerId} logged in at {LastLoginAt}", playerId, lastLoginAt);
            await SendPlayerMessage(context, pushClient, $"Welcome back! You last logged in at {lastLoginAt}", "WelcomeBack", playerId);
            return "Player message sent";
        }
        
        [CloudCodeFunction("SendAnnouncement")]
        public async Task<string> SendAnnouncement(IExecutionContext context, PushClient pushClient, string message)
        {
            _logger.LogInformation("Sending announcement: {Message}", message);
            await SendProjectMessage(context, pushClient, message, "");
            return "Project message sent";
        }
        
        [CloudCodeFunction("SendPlayerMessage")]
        public async Task<string> SendPlayerMessage(IExecutionContext context, PushClient pushClient, string message, string messageType, string playerId)
        {
            var response = await pushClient.SendPlayerMessageAsync(context, message, messageType, playerId);
            
            return "Player message sent";
        }

        [CloudCodeFunction("SendProjectMessage")]
        public async Task<string> SendProjectMessage(IExecutionContext context, PushClient pushClient, string message, string messageType)
        {
            var response = await pushClient.SendProjectMessageAsync(context, message, messageType);
            return "Project message sent";
        }
    }
    
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.Dependencies.AddSingleton(PushClient.Create());
        }
    }
}

