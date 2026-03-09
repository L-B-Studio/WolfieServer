using ProjectMessengerServer.Application.DTO.Ws;
using ProjectMessengerServer.Application.Services;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Infrastructure.WebSockets
{
    public class WsEventService
    {
        private readonly WsConnectionManager _connections;
        private readonly ChatService _chatService;
        private readonly AppDbContext _dbContext;

        public WsEventService(WsConnectionManager connections, ChatService chatService, WsSender wsSender, AppDbContext dbContext)
        {
            _connections = connections;
            _chatService = chatService;
            _dbContext = dbContext;
        }

        public async Task BroadcastChatCreated(Chat chat)
        {
            var envelope = new WsEnvelope(
                "new_chat",
                new()
                {
                    ["chat_uid"] = chat.Uid,
                    ["chat_name"] = chat.Name
                },
                null
            );

            var users = await _chatService.GetChatMembers(chat.Uid);

            foreach (var userId in users)
            {
                var sockets = _connections.GetConnections(userId);

                foreach (var socket in sockets)
                {
                    await WsSender.SendAsync(socket, envelope);
                }
            }
        }

        public async Task BroadcastMessage(int userId, string chatUid, Message message)
        {
            var envelope = new WsEnvelope(
                "new_message",
                new()
                {
                    ["chat_uid"] = chatUid,
                    ["message_id"] = message.Id.ToString(),
                    ["message_text"] = message.Text,
                    ["sender_id"] = _dbContext.UserProfiles.Where(p => p.UserId == message.SenderId).Select(p => p.Name).FirstOrDefault()!,
                    ["created_at"] = message.CreatedAt.ToString("o")
                },
                null
            );

            //x.LastMessage != null! ? _dbContext.UserProfiles.Where(p => p.UserId == x.LastMessage.SenderId).Select(p => p.Name).FirstOrDefault() ?? "unknown" : null!,
            var users = await _chatService.GetChatMembers(chatUid);

            foreach (var uid in users)
            {
                var sockets = _connections.GetConnections(uid);
                foreach (var socket in sockets)
                {
                    await WsSender.SendAsync(socket, envelope);
                }
            }
        }
    }
}
