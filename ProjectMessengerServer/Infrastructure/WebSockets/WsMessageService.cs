using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Application.DTO.Ws;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Infrastructure.WebSockets
{
    public class WsMessageService
    {
        private readonly AppDbContext _db;
        private readonly WsEventService _eventService;

        public WsMessageService(
            AppDbContext db,
            WsEventService eventService)
        {
            _db = db;
            _eventService = eventService;
        }

        public async Task ProcessMessageAsync(int userId, WebSocket ws, string json)
        {
            var message = JsonSerializer.Deserialize<WsEnvelope>(json);

            if (message == null)
            {
                Console.WriteLine($"[{userId}]Failed to deserialize message");
                return;
            }

            var operation = message.Op;

            if (string.IsNullOrWhiteSpace(operation))
            {
                Console.WriteLine($"[{userId}]Operation is missing or empty");
                return;
            }

            var messageDataType = message.Data;

            if (messageDataType == null) {
                Console.WriteLine($"[{userId}]Message data is missing");
                return;
            }

            switch (operation)
            {
                case "send_message":
                    await HandleSendMessage(userId, ws, messageDataType);
                    break;
            }
        }

        private async Task HandleSendMessage(int userId, WebSocket ws, Dictionary<string, string> req)
        {
            req.TryGetValue("chat_uid", out string? chatUid);
            req.TryGetValue("message_text", out string? messageText);

            if (string.IsNullOrWhiteSpace(chatUid) ||
                string.IsNullOrWhiteSpace(messageText))
            {
                Console.WriteLine($"Invalid message data: chat_uid='{chatUid}', message_text='{messageText}'");
                return;
            }

            if ((!await _db.Chats.AnyAsync(c => c.Uid == chatUid))
                || !await _db.ChatMembers.AnyAsync(cm => cm.Chat.Uid == chatUid && cm.UserId == userId))
            {
                Console.WriteLine($"User {userId} is not a member of chat '{chatUid}' or chat does not exist");
                return;
            }

            var chatId = await _db.Chats
                .Where(c => c.Uid == chatUid)
                .Select(c => c.Id)
                .FirstAsync();

            var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) {
                Console.WriteLine($"Chat with UID '{chatUid}' not found");
                return;
            }

            var lastMessageInChatId = await _db.Messages
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Id)
                .FirstOrDefaultAsync();

            var message = new Message
            {
                ChatId = chatId,
                SenderId = userId,
                Text = messageText,
                MessageInChatId = lastMessageInChatId + 1,
                CreatedAt = DateTime.UtcNow
            };

            _db.Messages.Add(message);

            await _db.SaveChangesAsync();

            chat.LastMessageId = message.Id;

            await _db.SaveChangesAsync();

            await _eventService.BroadcastMessage(userId, chatUid, message);
        }
    }
}
