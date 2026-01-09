using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ProjectMessengerServer.Model
{
    public class WsConnectionManager
    {
        public static ConcurrentDictionary<int, List<WebSocket>> Connections = new();
    }
}
