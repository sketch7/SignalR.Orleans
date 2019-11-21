using Microsoft.AspNetCore.SignalR.Protocol;

namespace SignalR.Orleans.Clients
{
    public class ClientMessage
    {
        public string HubName { get; set; }
        public string ConnectionId { get; set; }
        public InvocationMessage Payload { get; set; }

        public static string GetStreamId(string serverId, int replica)
            => $"{serverId}::{replica}";
    }
}