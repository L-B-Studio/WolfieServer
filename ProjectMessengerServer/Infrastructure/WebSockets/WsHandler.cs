using System.Net.WebSockets;
using System.Text;

namespace ProjectMessengerServer.Infrastructure.WebSockets
{
    public class WsHandler
    {
        private readonly WsConnectionManager _manager;
        private readonly WsMessageService _messageService;

        public WsHandler(
            WsConnectionManager manager,
            WsMessageService messageService,
            WsEventService eventService)
        {
            _manager = manager;
            _messageService = messageService;
        }

        public async Task HandleAsync(WebSocket socket, int userId)
        {
            _manager.Add(userId, socket);

            var buffer = new byte[4096];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _manager.Remove(userId, socket);
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                await _messageService.ProcessMessageAsync(userId, socket, json);
            }
        }
    }
}
