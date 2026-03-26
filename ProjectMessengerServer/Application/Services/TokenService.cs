using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectMessengerServer.Application.DTO.Auth;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Infrastructure.Networking;
using ProjectMessengerServer.Infrastructure.Security;

namespace ProjectMessengerServer.Application.Services
{
    public class TokenService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JwtOptions _config;
        private readonly ProfileService _profileService;

        public TokenService(
            AppDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IOptions<JwtOptions> config,
            ProfileService profileService)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
            _config = config.Value;
            _profileService = profileService;

            if (string.IsNullOrWhiteSpace(_config.Key))
                throw new InvalidOperationException("JWT Key is not configured!");
        }

        //public async Task<string> CreateAccessTokenAsync(User user)
        //{
        //    string hashAccessToken;
        //
        //    hashAccessToken = TokenHelper.GenerateSecureToken();
        //    var accessToken = new AccessToken
        //    {
        //        UserId = user.Id,
        //        AccessTokenHash = TokenHelper.HashToken(hashAccessToken),
        //        CreatedAt = DateTime.UtcNow,
        //        ExpiresAt = DateTime.UtcNow.AddMinutes(3),
        //        Revoked = false
        //    };
        //
        //    _dbContext.AccessTokens.Add(accessToken);
        //    await _dbContext.SaveChangesAsync();
        //
        //    //Логику токена надо менять
        //    //var key = new SymmetricSecurityKey(
        //    //    Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])
        //    //);
        //    //
        //    //var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        //    //
        //    //var token = new JwtSecurityToken(
        //    //    claims: TokenHelper.GetClaims(user),
        //    //    expires: accessToken.ExpiresAt,
        //    //    signingCredentials: creds
        //    //);
        //    //
        //    //var lol1 = new JwtSecurityTokenHandler().WriteToken(token);
        //
        //    return hashAccessToken;
        //}


        public async Task<string> CreateAccessTokenAsync(User user)
        {
            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile == null)
            {
                DateTime.TryParse("2000-08-14", out DateTime birthday);
                profile = await _profileService.CreateUserProfileAsync(user, user.Email.Split('@')[0], birthday);

                try
                {
                    _dbContext.UserProfiles.Add(profile);
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    profile = await _dbContext.UserProfiles.FirstAsync(p => p.UserId == user.Id);
                }
            }

            var claims = new List<Claim>
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("uid", profile.PublicId),
            new Claim("name", profile.Name),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config.Key));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config.Issuer,
                audience: _config.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_config.ExpiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<string> CreateRefreshTokenAsync(User user)
        {
            string hashRefreshToken;

            hashRefreshToken = TokenHelper.GenerateSecureToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                RefreshTokenHash = TokenHelper.HashToken(hashRefreshToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Used = false,
                IpAddress = GetClientIp.GetClientIpAddress(_httpContextAccessor.HttpContext!),
                Revoked = false
            };

            _dbContext.RefreshTokens.Add(refreshToken);
            await _dbContext.SaveChangesAsync();

            return hashRefreshToken;
        }

        //public async Task<string> RecreateAccessTokenAsync(User user)
        //{
        //    string hashAccessToken;
        //    var activeAccessTokens = await _dbContext.AccessTokens
        //        .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
        //        .OrderBy(x => x.CreatedAt)
        //        .ToListAsync();
        //
        //    foreach (var oldAccessToken in activeAccessTokens)
        //    {
        //        oldAccessToken.Revoked = true;
        //    }
        //
        //    hashAccessToken = TokenHelper.GenerateSecureToken();
        //    var accessToken = new AccessToken
        //    {
        //        UserId = user.Id,
        //        AccessTokenHash = TokenHelper.HashToken(hashAccessToken),
        //        CreatedAt = DateTime.UtcNow,
        //        ExpiresAt = DateTime.UtcNow.AddMinutes(3),
        //        Revoked = false
        //    };
        //
        //    _dbContext.AccessTokens.Add(accessToken);
        //    await _dbContext.SaveChangesAsync();
        //
        //    return hashAccessToken;
        //}

        public async Task<string> RecreateRefreshTokenAsync(User user)
        {
            string hashRefreshToken;

            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            foreach (var oldRefreshToken in activeRefreshTokens)
            {
                oldRefreshToken.Revoked = true;
            }


            hashRefreshToken = TokenHelper.GenerateSecureToken();
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                RefreshTokenHash = TokenHelper.HashToken(hashRefreshToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Used = false,
                IpAddress = GetClientIp.GetClientIpAddress(_httpContextAccessor.HttpContext),
                Revoked = false
            };

            _dbContext.RefreshTokens.Add(refreshToken);
            await _dbContext.SaveChangesAsync();

            return hashRefreshToken;

        }   

        public async Task<RotationResult> RotationTokenAsync(string refreshTokenSession)
        {
            //string hashAccessToken;
            string hashRefreshToken;

            //
            var refreshTokenHash = TokenHelper.HashToken(refreshTokenSession);
            var refreshToken = await _dbContext.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.RefreshTokenHash == refreshTokenHash && !t.Used && t.ExpiresAt > DateTime.UtcNow && !t.Revoked);

            if (refreshToken == null)
            {
                return null!;
            }

            var user = refreshToken.User;

            //var activeAccessTokens = await _dbContext.AccessTokens
            //    .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
            //    .OrderBy(x => x.CreatedAt)
            //    .ToListAsync();

            //foreach (var oldAccessToken in activeAccessTokens)
            //{
            //    oldAccessToken.Revoked = true;
            //}

            refreshToken.Used = true;

            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            foreach (var oldRefreshToken in activeRefreshTokens)
            {
                oldRefreshToken.Revoked = true;
            }

            var accessToken = await CreateAccessTokenAsync(user);
            var newRefreshToken = await CreateRefreshTokenAsync(user);

            //
            return new RotationResult
            {
                HashAccessToken = accessToken,
                HashRefreshToken = newRefreshToken
            };
        }

        public async Task<Result> RevokeRefreshTokenAsync(string stringRefreshToken)
        {
            var refreshToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.RefreshTokenHash == TokenHelper.HashToken(stringRefreshToken) && !t.Revoked);

            if (refreshToken == null)
            {
                return Result.Failure("Refresh token not found or already revoked");
            }

            refreshToken.Revoked = true;

            try
            {
                await _dbContext.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logout: {ex.Message}");
                return Result.Failure("An error occurred during logout");
            }
        }
    }
}
