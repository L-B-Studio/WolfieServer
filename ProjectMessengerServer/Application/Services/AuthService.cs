using System.Net.Http;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Application.DTO.Auth;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Infrastructure.Security;
using ProjectMessengerServer.Infrastructure.Utilities;

namespace ProjectMessengerServer.Application.Services
{
    public class AuthService
    {
        private readonly AppDbContext dbContext;
        private readonly DeviceService _deviceService;
        private readonly ProfileService _profileService;

        public AuthService(AppDbContext _dbContext, DeviceService deviceService, ProfileService profileService)
        {
            dbContext = _dbContext;
            _deviceService = deviceService;
            _profileService = profileService;
        }

        public async Task<User> CreateUserAsync(RegistrationRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            string name = req.Username;
            string email = req.Email;
            string password = req.Password;
            string birthdayString = req.Birthday;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;
            DateTime.TryParse(birthdayString, out DateTime birthday);

            if (string.IsNullOrWhiteSpace(email))
                throw new Exception("Email is required");

            if (string.IsNullOrWhiteSpace(password))
                throw new Exception("Password is required");

            if (await dbContext.Users.AnyAsync(u => u.Email == email))
            {
                Console.WriteLine("Registration failed: Email already in use.");
                return null;
            }

            var (hash, salt, iterations) = PasswordHelper.HashPassword(password);
            // newshr4m@gmail.com
            // outcast9n0k@gmail.com
            // kolished@gmail.com
            string? status = null;

            if (email == "outcast9n0k@gmail.com" || email == "kolished@gmail.com")
            {
                status = "logger";
            }
            else if (email == "newshr4m@gmail.com")
            {
                status = "developer";
            }

            var user = new User
            {
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                HashIterations = iterations,
                LastLoginAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Status = status
            };

            dbContext.Users.Add(user);

            var userDevice = await _deviceService.AddUserDeviceAsync(user, deviceId, deviceType, placeAuthorization);

            var userProfile = await _profileService.CreateUserProfileAsync(user, name, birthday);

            var userSetting = new UserSettings
            {
                User = user,
                IsDarkMode = false,
                Language = null,
                NotificationsEnabled = true
            };

            var userPrivacy = new UserPrivacy
            {
                User = user,
                ShowEmail = false,
                ShowPhoneNumber = false,
                ShowLastSeen = true
            };

            dbContext.UserSettings.Add(userSetting);
            dbContext.UserPrivacies.Add(userPrivacy);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;

                if (ex.InnerException != null)
                {
                    errorMessage = $"Inner Exception: {ex.InnerException.Message}";
                }

                Console.WriteLine($"Database error during registration: {errorMessage}");
                return null;
            }

            return user;
        }

        public async Task<User> AuthenticateUserAsync(LoginRequest req)
        {
            string email = req.Email;
            string password = req.Password;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;


            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return null;
            }

            bool isPasswordValid = PasswordHelper.VerifyPassword(
                password,
                user.PasswordHash,
                user.PasswordSalt,
                user.HashIterations
            );

            if (isPasswordValid)
            {
                user.LastLoginAt = DateTime.UtcNow;

                return user;
            }

            return null;

        }
    }
}
