using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMessengerServer.Application.DTO.Chat;
using ProjectMessengerServer.Application.DTO.User;
using ProjectMessengerServer.Application.Services;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.WebSockets;

namespace ProjectMessengerServer.Controllers
{
    [ApiController]
    [Route("users/")]
    public class UsersController : Controller
    {
        private readonly UserService _userService;


        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUser(string? information)
        {
            if (string.IsNullOrWhiteSpace(information))
            {
                return BadRequest();
            }

            var users = await _userService.SearchByUsernameOrPublicIdAsync(information);

            if (users == null || !users.Any())
            {
                return NotFound();
            }

            return Ok(users);
        }
    }
}
