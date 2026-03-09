using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Application.DTO.Chat;
using ProjectMessengerServer.Application.DTO.Message;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Infrastructure.Security;
using ProjectMessengerServer.Infrastructure.Utilities;
using static ProjectMessengerServer.Domain.Entities.ChatMember;

namespace ProjectMessengerServer.Application.Services
{
    public class ChatService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Chat> CreateChatAsync(CreateChatRequest req, int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);

            string chatName = req.Chat_name;
            string chatType = req.Chat_type;

            var memberUids = req.Member_uids ?? new List<string>();

            var validMemberIds = memberUids
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct()
                .Select(uid =>
                {
                    var memberProfile = _dbContext.UserProfiles
                        .FirstOrDefault(up => up.PublicId == uid);
                    if (memberProfile == null || memberProfile.UserId == user.Id)
                        return -1;
                    return memberProfile.UserId;
                })
                .ToList();

            validMemberIds = validMemberIds
                .Where(uid => uid != -1)
                .ToList();

            validMemberIds.Add(user.Id);

            if (chatType == "private" && validMemberIds.Count != 2)
            {
                return null!;
            }

            string uid = RandomStringGenerator.GenerateRandomString(6);

            if (await _dbContext.Chats.AnyAsync(c => c.Uid == uid))
            {
                uid = RandomStringGenerator.GenerateRandomString(6);
            }

            var chat = new Chat
            {
                Uid = uid,
                Type = chatType,
                Name = chatName,
                CreatedAt = DateTime.UtcNow,
                Members = new List<ChatMember>()
            };

            ChatMember chatMember;

            foreach (var memberId in validMemberIds)
            {
                var member = await _dbContext.Users.FindAsync(memberId);
                if (member == null)
                {
                    continue;
                }
                ChatRole role;
                if (user.Id == member.Id)
                {
                    continue;
                }

                chatMember = new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = member.Id,
                    JoinedAt = DateTime.UtcNow,
                    Role = ChatRole.Member
                };
                chat.Members.Add(chatMember);
                _dbContext.ChatMembers.Add(chatMember);
            }

            chatMember = new ChatMember
            {
                ChatId = chat.Id,
                UserId = user.Id,
                JoinedAt = DateTime.UtcNow,
                Role = ChatRole.Owner
            };
            chat.Members.Add(chatMember);
            _dbContext.ChatMembers.Add(chatMember);

            _dbContext.Chats.Add(chat);

            var request = new Dictionary<string, string>
                    {
                        { "chat_uid", chat.Uid.ToString() },
                        { "chat_name", chat.Name },
                        { "chat_type", chat.Type }
                    };

            await _dbContext.SaveChangesAsync();

            return chat;
        }

        public async Task<List<GetChatsResponse>> GetChatsAsync(int? limit, int? after, int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);

            var take = Math.Clamp(limit ?? 10, 1, 20);

            var chatsQuery =
                from cm in _dbContext.ChatMembers
                join c in _dbContext.Chats on cm.ChatId equals c.Id
                join lm in _dbContext.Messages on c.LastMessageId equals lm.Id into lmj
                from lastMessage in lmj.DefaultIfEmpty()
                where cm.UserId == user.Id
                select new
                {
                    Chat = c,
                    LastMessage = lastMessage,
                    UnreadCount = _dbContext.Messages.Count(m =>
                        m.ChatId == c.Id &&
                        (cm.LastReadMessageId == null || m.Id > cm.LastReadMessageId)
                    )
                };

            chatsQuery = chatsQuery
                .OrderByDescending(x => x.UnreadCount)
                .ThenByDescending(x => x.LastMessage!.Id);

            if (after.HasValue)
            {
                chatsQuery = chatsQuery.Where(x =>
                    x.LastMessage != null &&
                    x.LastMessage.Id < after.Value
                );
            }


            var result = await chatsQuery
                .Take(take)
                .Select(x => new GetChatsResponse(
                    x.Chat.Uid,
                    x.Chat.Name,
                    x.Chat.Type,
                    x.LastMessage != null! ? x.LastMessage.Text : null!,
                    x.LastMessage != null! ? _dbContext.UserProfiles.Where(p => p.UserId == x.LastMessage.SenderId).Select(p => p.Name).FirstOrDefault() ?? "unknown" : null!,
                    x.LastMessage != null! ? x.LastMessage.CreatedAt.ToString() : null!,
                    x.UnreadCount,
                    x.LastMessage != null ? x.LastMessage.Id : -1
                ))
                .ToListAsync();

            return result;
        }

        public async Task<Result> JoinChatAsync(string chatUid, int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);

            if (string.IsNullOrWhiteSpace(chatUid))
            {
                return Result.Failure();
            }

            var checkMember = await _dbContext.ChatMembers
                .FirstOrDefaultAsync(cm => cm.UserId == user.Id && cm.Chat.Uid == chatUid);

            if (checkMember != null)
            {
                return Result.Failure();
            }

            var chat = await _dbContext.Chats
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Uid == chatUid);

            if (chat == null)
            {
                return Result.Failure();
            };

            if (chat.Type == "private")
            {
                return Result.Failure();
            }

            var chatMember = new ChatMember
            {
                ChatId = chat.Id,
                UserId = user.Id,
                JoinedAt = DateTime.UtcNow,
                Role = ChatRole.Member
            };

            _dbContext.ChatMembers.Add(chatMember);
            chat.Members.Add(chatMember);
            await _dbContext.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<List<GetMessageResponse>> GetMessagesAsync(string chatUid, int? limit, int? after, int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);

            if (string.IsNullOrWhiteSpace(chatUid))
            {
                return null!;
            }

            var chat = await _dbContext.Chats
                .FirstOrDefaultAsync(c => c.Uid == chatUid);

            if (chat == null)
            {
                return null!;
            }

            var member = await _dbContext.ChatMembers
                .FirstOrDefaultAsync(cm => cm.ChatId == chat.Id && cm.UserId == user.Id);

            if (member == null)
            {
                return null!;
            }

            var take = Math.Clamp(limit ?? 50, 1, 100);

            var messagesQuery = _dbContext.Messages
                .Where(m => m.ChatId == chat.Id);

            if (after.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.MessageInChatId > after.Value).OrderBy(m => m.MessageInChatId);
            }
            else
            {
                messagesQuery = messagesQuery.OrderByDescending(m => m.MessageInChatId)
                             .Take(take)
                             .OrderBy(m => m.MessageInChatId);
            }


            var messages = await messagesQuery
                .Take(take)
                .Select(m => new GetMessageResponse(
                    chatUid.ToString(),
                    m.MessageInChatId.ToString(),
                    _dbContext.UserProfiles.Where(p => p.UserId == m.SenderId).Select(p => p.PublicId).FirstOrDefault() ?? "unknown",
                    _dbContext.UserProfiles.Where(p => p.UserId == m.SenderId).Select(p => p.Name).FirstOrDefault() ?? "unknown",
                    m.Text,
                    m.CreatedAt.ToString()
                ))
                .ToListAsync();

            var lastMessage = messagesQuery
                .OrderByDescending(m => m.Id)
                .FirstOrDefault();

            if (member != null && lastMessage != null && member.LastReadMessageId < lastMessage.Id)
            {
                member.LastReadMessageId = lastMessage.Id;
            }

            await _dbContext.SaveChangesAsync();

            return messages;
        }
        public async Task<List<int>> GetChatMembers(string chatUid)
        {
            return await _dbContext.ChatMembers
                .Where(cm => cm.Chat.Uid == chatUid)
                .Select(cm => cm.UserId)
                .ToListAsync();
        }
    }
}
