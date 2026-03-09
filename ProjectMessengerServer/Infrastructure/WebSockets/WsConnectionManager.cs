using System.Net.Sockets;
using System.Net.WebSockets;

namespace ProjectMessengerServer.Infrastructure.WebSockets
{
    public class WsConnectionManager
    {
        private readonly Dictionary<int, List<WebSocket>> _connections = new();

        public void Add(int userId, WebSocket socket)
        {
            if (!_connections.ContainsKey(userId))
            {
                _connections[userId] = new List<WebSocket>();
            }

            _connections[userId].Add(socket);
        }

        public void Remove(int userId, WebSocket socket)
        {
            if (_connections.ContainsKey(userId))
            {
                _connections[userId].Remove(socket);

                if (_connections[userId].Count == 0)
                    _connections.Remove(userId);
            }
        }

        public List<WebSocket> GetConnections(int userId)
        {
            if (_connections.TryGetValue(userId, out var sockets))
                return sockets;

            return new List<WebSocket>();
        }
    }
}
