using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;

namespace HelloWorld
{
    public class VRNotificationService : INotificationService
    {
        private readonly PushClient _pushClient;

        public VRProgressService(PushClient pushClient)
        {
            _pushClient = pushClient;
        }

        public async Task<string> SendPlayerMessage(IExecutionContext context, string message, string messageType, string playerId)
        {
            var response = await _pushClient.SendPlayerMessageAsync(context, message, messageType, playerId);

            return "Player message sent";
        }

        public async Task<string> SendProjectMessage(IExecutionContext context, string message, string messageType)
        {
            var response = await _pushClient.SendProjectMessageAsync(context, message, messageType);
            return "Project message sent";
        }
    }
}