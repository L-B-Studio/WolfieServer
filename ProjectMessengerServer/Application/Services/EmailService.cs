using System.Net.Mail;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Application.Services
{
    public class EmailService
    {
        private readonly AppDbContext _dbContext;

        public EmailService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user!;
        }

        public async Task<Result> SendEmailAsync(MailMessage message)
        {
            try
            {
                var configContent = File.ReadAllText("appsettings.json");
                var configJson = JsonDocument.Parse(configContent);
                configJson.RootElement.GetProperty("Gmail").GetProperty("Host").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading appsettings.json: {ex.Message}");
                return Result.Failure();
            }

            string? appPassword;
            try
            {
                appPassword = Environment.GetEnvironmentVariable("APP_PASSWORD", EnvironmentVariableTarget.User);
                if (string.IsNullOrEmpty(appPassword))
                {
                    appPassword = Environment.GetEnvironmentVariable("APP_PASSWORD");

                    if (string.IsNullOrEmpty(appPassword))
                    {
                        throw new Exception("APP_PASSWORD environment variable is not set.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving APP_PASSWORD: {ex.Message}");
                return Result.Failure();
            }

            var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(
                    JsonDocument.Parse(File.ReadAllText("appsettings.json"))
                            .RootElement
                            .GetProperty("Gmail")
                            .GetProperty("Host")
                            .GetString(),
                        appPassword),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);

            return Result.Success();
        }
    }
}
