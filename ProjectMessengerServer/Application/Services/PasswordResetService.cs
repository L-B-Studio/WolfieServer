using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Security;
using ProjectMessengerServer.Infrastructure.Networking;
using ProjectMessengerServer.Application.DTO.Password;

namespace ProjectMessengerServer.Application.Services
{
    public class PasswordResetService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EmailService _emailService;

        public PasswordResetService(AppDbContext dbContext, IHttpContextAccessor httpContext, EmailService emailService)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContext;
            _emailService = emailService;
        }

        public async Task<Result> ResetPasswordAsync(ForgotPassRequest req)
        {
            string email = req.Email;

            var user = await _emailService.GetUserByEmailAsync(email);
            if (user == null)
            {
                return Result.Failure();
            }

            //var rng = RandomNumberGenerator.Create();
            //byte[] bytes = new byte[4];
            //rng.GetBytes(bytes);
            //int value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;

            //string code = value.ToString("D9"); // например: 047912

            //const string chars = "abcdefghijklmnopqrstuvwxyz0123456789"; // буквы нижнего регистра + цифры
            const string chars = "0123456789"; // буквы нижнего регистра + цифры
            Random random = new Random();
            char[] code = new char[9];

            for (int i = 0; i < 9; i++)
            {
                code[i] = chars[random.Next(chars.Length)];
            }

            string codeString = new string(code);
            var message = new MailMessage();
            message.To.Add(email);
            message.Subject = "Код подтверждения";
            message.Body = $"Ваш код: {codeString}";
            message.From = new MailAddress("bearodit@gmail.com");

            var sendResult = await _emailService.SendEmailAsync(message);

            if(!sendResult.IsSuccess)
            {
                return Result.Failure();
            }

            var codeHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(codeString))
            );

            var tokenModel = new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = codeHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),  // код действует 10 минут
                Used = false
            };

            //var oldTokens = dbContext.PasswordResetTokens.Where(t => t.UserId == user.Id && t.ExpiresAt < DateTime.UtcNow && !t.Used);
            //dbContext.PasswordResetTokens.RemoveRange(oldTokens);

            _dbContext.PasswordResetTokens.Add(tokenModel);
            await _dbContext.SaveChangesAsync();
            //
            return Result.Success();
        }

        public async Task<string> VerifyResetPasswordAsync(ForgotPassVerifyRequest req)
        {
            string email = req.Email;
            string code = req.Code;

            string hashTokenReset;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return null!;
            }
            var codeHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(code))
            );
            var token = await _dbContext.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == codeHash && !t.Used && t.ExpiresAt > DateTime.UtcNow);
            if (token == null)
            {
                return null!;
            }

            var oldTokens = await _dbContext.PasswordResetTokens.Where(t => t.UserId == user.Id && t.ExpiresAt < DateTime.UtcNow && !t.Used).ToListAsync();

            foreach (var oldToken in oldTokens)
            {
                oldToken.Used = true;
            }

            await _dbContext.SaveChangesAsync();

            try
            {
                var activeTokens = await _dbContext.PasswordResetTokenResets
                    .Where(x => x.UserId == user.Id && !x.Revoked && !x.Used && x.ExpiresAt > DateTime.UtcNow)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync();

                if (activeTokens.Count > 1)
                {
                    var oldestToken = activeTokens.First();
                    oldestToken.Revoked = true;
                }

                hashTokenReset = TokenHelper.GenerateSecureToken();
                var tokenReset = new PasswordResetTokenReset
                {
                    UserId = user.Id,
                    TokenResetHash = TokenHelper.HashToken(hashTokenReset),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    Used = false,
                    IpAddress = GetClientIp.GetClientIpAddress(_httpContextAccessor.HttpContext),
                    Revoked = false
                };

                _dbContext.PasswordResetTokenResets.Add(tokenReset);
                await _dbContext.SaveChangesAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex.InnerException != null)
                    Console.WriteLine("INNER: " + ex.InnerException.Message);

                throw;
            }

            return hashTokenReset;
            //
        }

        public async Task<Result> ChangePasswordAsync(ChangedPassRequest req)
        {
            string token = req.Token_reset;
            string newPassword = req.Password;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;
            //
            var tokenResetHash = TokenHelper.HashToken(token);

            var tokenReset = await _dbContext.PasswordResetTokenResets
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.TokenResetHash == tokenResetHash && !t.Used && t.ExpiresAt > DateTime.UtcNow && !t.Revoked);

            if (tokenReset == null)
            {
                throw new ValidationException();
            }

            var user = tokenReset.User;

            var checkDeviceId = await _dbContext.ForgotPassDevices
                    .FirstOrDefaultAsync(ud => ud.UserId == user.Id && ud.DeviceId == deviceId);

            if (checkDeviceId == null)
            {
                var newUserDevice = new UserDevice
                {
                    UserId = tokenReset.UserId,
                    DeviceId = deviceId ?? null,
                    DeviceType = deviceType ?? null,
                    PlaceAuthorization = placeAuthorization ?? null,
                    LastActive = DateTime.UtcNow
                };
                _dbContext.UserDevices.Add(newUserDevice);
            }

            tokenReset.Used = true;
            var (hash, salt, iterations) = PasswordHelper.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.HashIterations = iterations;
            await _dbContext.SaveChangesAsync();
            //

            return Result.Success();
        }
    }
}
