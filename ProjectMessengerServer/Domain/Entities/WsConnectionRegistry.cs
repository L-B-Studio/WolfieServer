using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ProjectMessengerServer.Domain.Entities
{
    public class WsConnectionRegistry
    {
        public static ConcurrentDictionary<int, List<WebSocket>> Connections = new();
    }
}