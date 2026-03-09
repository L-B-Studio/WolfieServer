using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Infrastructure.WebSockets;

namespace ProjectMessengerServer.Infrastructure.Logging
{
    public class LogManager
    {
        private readonly WsEventService _eventService;
        private readonly AppDbContext _dbContext;

        public LogManager(WsEventService eventService, AppDbContext appContext)
        {
            _eventService = eventService;
            _dbContext = appContext;
        }

        public async Task AddLog(string level, string message)
        {
            var log = new Log
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message
            };
            _dbContext.Logs.Add(log);

            var chatsId = await _dbContext.Chats
                .Where(c => c.Name == "Logs")
                .Select(c => c.Id)
                .ToListAsync();

            var userId = await _dbContext.Users
                .Where(u => u.Email == "wolfie@gmail.com")
                .OrderByDescending(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            foreach (var chatId in chatsId)
            {
                var lastMessageInChatId = await _dbContext.Messages
                    .Where(m => m.ChatId == chatId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Id)
                    .FirstOrDefaultAsync();

                var messageChat = new Message
                {
                    ChatId = chatId,
                    SenderId = userId,
                    Text = message,
                    MessageInChatId = lastMessageInChatId + 1,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Messages.Add(messageChat);

                var chatUid = await _dbContext.Chats
                    .Where(c => c.Id == chatId)
                    .Select(c => c.Uid)
                    .FirstOrDefaultAsync();

                await _eventService.BroadcastMessage(userId, chatUid!, messageChat);
            }

            await _dbContext.SaveChangesAsync();

        }

        public static IEnumerable<Log> GetLogs(AppDbContext dbContext, int limit, int? offset = 0)
        {
            var query = dbContext.Logs
                .OrderByDescending(l => l.Id)
                .Skip(limit);
            if (offset.HasValue)
            {
                query = query.Take(offset.Value);
            }
            return query.Reverse().ToList();
        }
    }
}