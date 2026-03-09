using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Infrastructure.Utilities;

namespace ProjectMessengerServer.Application.Services
{
    public class ProfileService
    {
        private readonly AppDbContext dbContext;

        public ProfileService(AppDbContext _dbContext)
        {
            dbContext = _dbContext;
        }

        public async Task<UserProfile> CreateUserProfileAsync(User user, string? name, DateTime birthday, string phoneNumber = null!, string avatarUrl = null!, string bio = null!)
        {
            
            string publicId = RandomStringGenerator.GenerateRandomString(6);

            while (await dbContext.UserProfiles.AnyAsync(up => up.PublicId == publicId))
            {
                publicId = RandomStringGenerator.GenerateRandomString(6);
            }

            var userProfile = new UserProfile
            {
                User = user,
                Name = name,
                PublicId = publicId,
                PhoneNumber = phoneNumber,
                AvatarUrl = avatarUrl,
                Birthday = birthday,
                Bio = bio
            };

            dbContext.UserProfiles.Add(userProfile);
            await dbContext.SaveChangesAsync();
            return userProfile;
        }
    }
}
