using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Application.DTO.Auth;
using ProjectMessengerServer.Application.DTO.User;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Application.Services
{
    public class UserService
    {

        private readonly AppDbContext _dbContext;

        public UserService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<SearchUserResponse>> SearchByUsernameOrPublicIdAsync(string information)
        {
            var usersProfiles = await _dbContext.UserProfiles
                .Where(up => up.Name.Contains(information) || up.PublicId.Contains(information))
                .Include(up => up.User)
                .ToListAsync();

            var users = usersProfiles.Select(up => up.User).ToList();

            foreach (var user in users)
            {
                if (user.IsBlocked == true)
                {
                    users.Remove(user);
                }
            }

            var responses = usersProfiles.Select(up => new SearchUserResponse(up.PublicId, up.Name, up.Bio)).ToList();

            return responses;
        }
    }
}
