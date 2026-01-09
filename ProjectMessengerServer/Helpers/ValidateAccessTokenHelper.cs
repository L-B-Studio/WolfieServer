using ProjectMessengerServer.Model;
using ProjectMessengerServer.Data;
using Microsoft.EntityFrameworkCore;


namespace ProjectMessengerServer.Helpers
{
    public class ValidateAccessTokenHelper
    {
        public static async Task<User>? ValidateToken(string token, AppDbContext dbContext)
        {
            var tokenHash = TokenHelper.HashToken(token);

            var accessToken = await dbContext.AccessTokens
                .Include(at => at.User)
                .FirstOrDefaultAsync(at => at.AccessTokenHash == tokenHash && at.ExpiresAt > DateTime.UtcNow && at.Revoked == false);

            if (accessToken == null)
            {
                return null;
            }

            return accessToken.User;
        }
    }
}
