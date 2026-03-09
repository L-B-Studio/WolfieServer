using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMessengerServer.Application.DTO.Chat;
using ProjectMessengerServer.Application.Services;
using ProjectMessengerServer.Infrastructure.WebSockets;

namespace ProjectMessengerServer.Controllers
{
    [ApiController]
    [Route("chats")]
    public class ChatsController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly WsEventService _wsEventService;

        public ChatsController(ChatService chatService, WsEventService wsEventService)
        {
            _chatService = chatService;
            _wsEventService = wsEventService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateChat(CreateChatRequest req)
        {
            string chatName = req.Chat_name;
            string chatType = req.Chat_type;

            var stringUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            stringUserId = int.TryParse(stringUserId, out int userId) ? userId.ToString() : null;

            if (string.IsNullOrWhiteSpace(stringUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(chatName) ||
                string.IsNullOrWhiteSpace(chatType))
            {
                return BadRequest();
            }

            if (chatType != "private" && chatType != "group")
            {
                return BadRequest();
            }

            var chat = await _chatService.CreateChatAsync(req, userId);

            if (chat == null)
            {
                return BadRequest();
            }

            await _wsEventService.BroadcastChatCreated(chat);

            return Ok(new CreateChatResponse(chat.Uid.ToString()));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetChats(int? limit, int? after)
        {

            var stringUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            stringUserId = int.TryParse(stringUserId, out int userId) ? userId.ToString() : null;

            if (string.IsNullOrWhiteSpace(stringUserId))
            {
                return Unauthorized();
            }


            if (!limit.HasValue || limit <= 0)
            {
                limit = 10;
            }
            var chatsResponses = await _chatService.GetChatsAsync(limit, after, userId);

            if (chatsResponses == null)
            {
                return BadRequest();
            }

            return Ok(chatsResponses);
        }

        [Authorize]
        [HttpPost("{chatUid}/join")]
        public async Task<IActionResult> JoinChat(string chatUid)
        {

            var stringUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            stringUserId = int.TryParse(stringUserId, out int userId) ? userId.ToString() : null;

            if (string.IsNullOrWhiteSpace(stringUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(chatUid))
            {
                return BadRequest();
            }

            var result = await _chatService.JoinChatAsync(chatUid, userId);

            if(!result.IsSuccess)
            {
                return BadRequest();
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("{chatUid}/messages")]
        public async Task<IActionResult> GetMessage(string chatUid, int? limit, int? after)
        {
            var stringUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            stringUserId = int.TryParse(stringUserId, out int userId) ? userId.ToString() : null;

            if (string.IsNullOrWhiteSpace(stringUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(chatUid))
            {
                return BadRequest();
            }

            if (!limit.HasValue || limit <= 0)
            {
                limit = 10;
            }

            var messages = await _chatService.GetMessagesAsync(chatUid, limit, after, userId);

            if (messages == null)
            {
                return BadRequest();
            }

            return Ok(messages); ;
        }
    }
}
