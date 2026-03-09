using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using ProjectMessengerServer.Application.DTO.Ws;

namespace ProjectMessengerServer.Infrastructure.WebSockets
{
    public class WsSender
    {
        public static async Task SendAsync(WebSocket socket, WsEnvelope envelope)
        {
            if (socket.State != WebSocketState.Open)
                return;

            var json = JsonSerializer.Serialize(envelope);
            var bytes = Encoding.UTF8.GetBytes(json);

            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
