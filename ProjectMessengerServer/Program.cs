using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Data;
using ProjectMessengerServer.Model;
using ProjectMessengerServer.Helpers;
using System.Security.Cryptography;
using System.Net.Mail;

namespace ProjectMessengerServer
{
    class CertPasswordManager
    {
        public static void SavePassword(string password)
        {
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable("CERT_PASSWORD", password, EnvironmentVariableTarget.User);

                Environment.SetEnvironmentVariable("CERT_PASSWORD", password);

                Console.WriteLine("Saved in Windows User Environment Variables.");
            }
            else if (OperatingSystem.IsLinux())
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string bashrc = Path.Combine(home, ".bashrc");

                string line = $"export CERT_PASSWORD=\"{password}\"";

                if (File.Exists(bashrc))
                {
                    var lines = File.ReadAllLines(bashrc)
                        .Where(l => !l.StartsWith($"export CERT_PASSWORD="))
                        .ToList();

                    lines.Add(line);
                    File.WriteAllLines(bashrc, lines);
                }
                else
                {
                    File.WriteAllText(bashrc, line + "\n");
                }

                Environment.SetEnvironmentVariable("CERT_PASSWORD", password);
            }
            else
            {
                throw new NotSupportedException("OS not supported.");
            }
        }

        public static string LoadPassword()
        {
            string? pass = Environment.GetEnvironmentVariable("CERT_PASSWORD", EnvironmentVariableTarget.User);
            if (pass == null)
            {
                pass = Environment.GetEnvironmentVariable("CERT_PASSWORD");
                if (pass == null)
                {
                    Console.WriteLine("Password not found!");
                    throw new FileNotFoundException("Password not found.");
                }
                else
                {
                    return pass;
                }
            }
            else
            {
                return pass;
            }
        }
    }

    public class Program
    {
        private const int _port = 1234;
        //private const string _serverIp = "192.168.168.118";
        private const string _serverIp = "141.105.132.149";

        public static void Main(string[] args)
        {
            Console.Title = "Messager";

            var certificate = CertificateHelper.GetOrCreateCertificate();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();




            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            //services.AddDbContext<AppDbContext>(options =>
            //    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            var builder = WebApplication.CreateBuilder(args);

            // добавляем DbContext сразу в DI приложения
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            // Собираем сервис-провайдер
            //var serviceProvider = new ServiceCollection().BuildServiceProvider();

            try
            {
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB connection or migration error: {ex.Message}");
                return;
            }

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(
                    IPAddress.Parse(_serverIp),
                    _port,
                    listenOptions =>
                    {
                        listenOptions.UseHttps(certificate);
                    });
            });

            var app = builder.Build();

            app.UseMiddleware<RequestLoggingMiddlewareHelper>();
            app.UseMiddleware<ResponseLoggingMiddlewareHelper>();

            app.MapPost("/auth/registration", async (RegistrationRequest req, HttpContext httpContext) =>
            {

                //data.TryGetValue("username", out string? name);
                //data.TryGetValue("email", out string? email);
                //data.TryGetValue("password", out string? password);
                //data.TryGetValue("birthday", out string? birthdayString);
                //data.TryGetValue("device_info", out string? deviceInfo);

                string name = req.Username;
                string email = req.Email;
                string password = req.Password;
                string birthdayString = req.Birthday;
                string? deviceInfo = req.Device_info;

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(birthdayString))
                {
                    Console.WriteLine("Registration failed: Missing required fields.");
                    return Results.BadRequest();
                }

                if (!DateTime.TryParse(birthdayString, out DateTime birthday))
                {
                    Console.WriteLine("Registration failed: Invalid birthday format.");
                    return Results.BadRequest();
                }

                string hashAccessToken;
                string hashRefreshToken;

                //
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    if (await dbContext.Users.AnyAsync(u => u.Email == email))
                    {
                        Console.WriteLine("Registration failed: Email already in use.");
                        return Results.Unauthorized();
                    }

                    // 🔐 Хеширование пароля (PBKDF2)
                    var (hash, salt, iterations) = PasswordHelper.HashPassword(password);

                    var user = new User
                    {
                        Name = name,
                        Email = email,
                        Birthday = birthday, // <-- Теперь сохраняем Birthday
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        HashIterations = iterations,
                        CreatedAt = DateTime.UtcNow
                    };

                    try
                    {
                        dbContext.Users.Add(user);
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
                        return Results.Problem();
                    }

                    hashAccessToken = TokenHelper.GenerateSecureToken();
                    var accessToken = new AccessToken
                    {
                        UserId = user.Id,
                        AccessTokenHash = TokenHelper.HashToken(hashAccessToken),
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(3),
                        Revoked = false
                    };

                    dbContext.AccessTokens.Add(accessToken);

                    hashRefreshToken = TokenHelper.GenerateSecureToken();

                    var refreshToken = new RefreshToken
                    {
                        UserId = user.Id,
                        RefreshTokenHash = TokenHelper.HashToken(hashRefreshToken),
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(30),
                        Used = false,
                        IpAddress = GetIpHelper.GetClientIp(httpContext),
                        Revoked = false
                    };

                    dbContext.RefreshTokens.Add(refreshToken);
                    await dbContext.SaveChangesAsync();
                }
                //
                Console.WriteLine($"User registered: {email}");
                return Results.Ok(new RegistrationResponse(
                    Token_refresh: hashRefreshToken,
                    Token_access: hashAccessToken
                ));
            });

            app.MapPost("/auth/login", async (LoginRequest req, HttpContext httpContext) =>
            {

                //data.TryGetValue("email", out string? email);
                //data.TryGetValue("password", out string? password);
                //data.TryGetValue("device_info", out string? deviceInfo);

                string email = req.Email;
                string password = req.Password;
                string? deviceInfo = req.Device_info;

                if (email == null || password == null)
                {
                    return Results.BadRequest();
                }

                string hashAccessToken;
                string hashRefreshToken;

                //
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }

                    bool isPasswordValid = PasswordHelper.VerifyPassword(
                        password,
                        user.PasswordHash,
                        user.PasswordSalt,
                        user.HashIterations
                    );

                    if (isPasswordValid)
                    {
                        var activeAccessTokens = await dbContext.AccessTokens
                            .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                            .OrderBy(x => x.CreatedAt)
                            .ToListAsync();

                        foreach (var oldAccessToken in activeAccessTokens)
                        {
                            oldAccessToken.Revoked = true;
                        }

                        var activeRefreshTokens = await dbContext.RefreshTokens
                            .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                            .OrderBy(x => x.CreatedAt)
                            .ToListAsync();

                        foreach (var oldRefreshToken in activeRefreshTokens)
                        {
                            oldRefreshToken.Revoked = true;
                        }

                        hashAccessToken = TokenHelper.GenerateSecureToken();
                        var accessToken = new AccessToken
                        {
                            UserId = user.Id,
                            AccessTokenHash = TokenHelper.HashToken(hashAccessToken),
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(3),
                            Revoked = false
                        };

                        dbContext.AccessTokens.Add(accessToken);

                        hashRefreshToken = TokenHelper.GenerateSecureToken();
                        var refreshToken = new RefreshToken
                        {
                            UserId = user.Id,
                            RefreshTokenHash = TokenHelper.HashToken(hashRefreshToken),
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(30),
                            Used = false,
                            DeviceInfo = deviceInfo ?? "unknown",
                            IpAddress = GetIpHelper.GetClientIp(httpContext),
                            Revoked = false
                        };

                        dbContext.RefreshTokens.Add(refreshToken);
                        await dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        return Results.Unauthorized();
                    }
                }
                //

                return Results.Ok(new LoginResponse(
                    Token_access: hashAccessToken,
                    Token_refresh: hashRefreshToken
                ));
            });

            app.MapPost("/auth/get_access_token", async (GetAccessTokenRequest req, HttpContext httpContext) =>
            {

                //data.TryGetValue("email", out string? email);
                //data.TryGetValue("password", out string? password);
                //data.TryGetValue("device_info", out string? deviceInfo);

                //data.TryGetValue("token_refresh", out string refreshTokenSession);
                //data.TryGetValue("device_info", out string? deviceInfo);

                string refreshTokenSession = req.Token_refresh;
                string? deviceInfo = req.Device_info;

                if (string.IsNullOrWhiteSpace(refreshTokenSession))
                {
                    return Results.BadRequest();
                }

                string hashAccessToken;
                string hashRefreshToken;

                //
                var refreshTokenHash = TokenHelper.HashToken(refreshTokenSession);
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var refreshToken = await dbContext.RefreshTokens
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t =>
                            t.RefreshTokenHash == refreshTokenHash && !t.Used && t.ExpiresAt > DateTime.UtcNow && !t.Revoked);
                    if (refreshToken == null)
                    {
                        return Results.Unauthorized();
                    }

                    var user = refreshToken.User;

                    var activeAccessTokens = await dbContext.AccessTokens
                        .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                        .OrderBy(x => x.CreatedAt)
                        .ToListAsync();

                    foreach (var oldAccessToken in activeAccessTokens)
                    {
                        oldAccessToken.Revoked = true;
                    }

                    refreshToken.Used = true;

                    var activeRefreshTokens = await dbContext.RefreshTokens
                        .Where(x => x.UserId == user.Id && !x.Revoked && x.ExpiresAt > DateTime.UtcNow)
                        .OrderBy(x => x.CreatedAt)
                        .ToListAsync();

                    foreach (var oldRefreshToken in activeRefreshTokens)
                    {
                        oldRefreshToken.Revoked = true;
                    }


                    hashAccessToken = TokenHelper.GenerateSecureToken();
                    var accessToken = new AccessToken
                    {
                        UserId = user.Id,
                        AccessTokenHash = TokenHelper.HashToken(hashAccessToken),
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(3),
                        Revoked = false
                    };

                    dbContext.AccessTokens.Add(accessToken);

                    hashRefreshToken = TokenHelper.GenerateSecureToken();
                    var newRefreshToken = new RefreshToken
                    {
                        UserId = user.Id,
                        RefreshTokenHash = TokenHelper.HashToken(hashRefreshToken),
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(30),
                        Used = false,
                        DeviceInfo = deviceInfo ?? "unknown",
                        IpAddress = GetIpHelper.GetClientIp(httpContext),
                        Revoked = false
                    };

                    dbContext.RefreshTokens.Add(newRefreshToken);
                    await dbContext.SaveChangesAsync();
                }
                //

                return Results.Ok(new GetAccessTokenResponse(
                    Token_access: hashAccessToken,
                    Token_refresh: hashRefreshToken
                ));
            });

            app.MapPost("/auth/password/forgot", async (ForgotPassRequest req, HttpContext httpContext) =>
            {

                //data.TryGetValue("email", out string? email);
                //data.TryGetValue("password", out string? password);
                //data.TryGetValue("device_info", out string? deviceInfo);

                //data.TryGetValue("token_refresh", out string refreshTokenSession);
                //data.TryGetValue("device_info", out string? deviceInfo);

                //data.TryGetValue("email", out string email);

                string email = req.Email;

                if (string.IsNullOrWhiteSpace(email))
                {
                    return Results.BadRequest();
                }

                string hashAccessToken;
                string hashRefreshToken;

                //
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }
                    // Здесь должна быть логика отправки письма с восстановлением пароля
                    // Для упрощения примера, мы просто отправим успешный ответ


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

                    try
                    {
                        var configContent = File.ReadAllText("appsettings.json");
                        var configJson = JsonDocument.Parse(configContent);
                        var gmailHost = configJson.RootElement.GetProperty("Gmail").GetProperty("Host").GetString();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading appsettings.json: {ex.Message}");
                        return Results.Problem();
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
                        return Results.Problem();
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

                    dbContext.PasswordResetTokens.Add(tokenModel);
                    await dbContext.SaveChangesAsync();
                }
                //

                return Results.NoContent();
            });

            app.MapPost("/auth/password/verify", async (ForgotpassVerifyRequest req, HttpContext httpContext) =>
            {


                //data.TryGetValue("email", out string email);
                //data.TryGetValue("code", out string code);
                //data.TryGetValue("device_info", out string deviceInfo);

                string email = req.Email;
                string code = req.Code;
                string? deviceInfo = req.Device_info;

                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(code))
                {
                    return Results.BadRequest();
                }

                string hashTokenReset;

                //
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }
                    var codeHash = Convert.ToBase64String(
                        SHA256.HashData(Encoding.UTF8.GetBytes(code))
                    );
                    var token = await dbContext.PasswordResetTokens
                        .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == codeHash && !t.Used && t.ExpiresAt > DateTime.UtcNow);
                    if (token == null)
                    {
                        return Results.Unauthorized();
                    }

                    token.Used = true;
                    await dbContext.SaveChangesAsync();

                    try
                    {
                        var activeTokens = await dbContext.PasswordResetTokenResets
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
                            DeviceInfo = deviceInfo ?? "unknown",
                            IpAddress = GetIpHelper.GetClientIp(httpContext),
                            Revoked = false
                        };

                        dbContext.PasswordResetTokenResets.Add(tokenReset);
                        await dbContext.SaveChangesAsync();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        if (ex.InnerException != null)
                            Console.WriteLine("INNER: " + ex.InnerException.Message);

                        throw;
                    }
                }
                //


                return Results.Ok(new ForgotpassVerifyResponse(
                    Token_reset: hashTokenReset
                ));
            });

            app.MapPost("/auth/password/change", async (ChangedpassRequest req, HttpContext httpContext) =>
            {

                //data.TryGetValue("token_reset", out string token);
                //data.TryGetValue("password", out string newPassword);

                string token = req.Token_reset;
                string newPassword = req.Password;

                if (string.IsNullOrWhiteSpace(token) ||
                    string.IsNullOrWhiteSpace(newPassword))
                {
                    return Results.BadRequest();
                }

                //
                var tokenResetHash = TokenHelper.HashToken(token);

                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var tokenReset = await dbContext.PasswordResetTokenResets
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t =>
                            t.TokenResetHash == tokenResetHash && !t.Used && t.ExpiresAt > DateTime.UtcNow && !t.Revoked);

                    if (tokenReset == null)
                    {
                        return Results.Unauthorized();
                    }

                    var user = tokenReset.User;

                    tokenReset.Used = true;
                    var (hash, salt, iterations) = PasswordHelper.HashPassword(newPassword);
                    user.PasswordHash = hash;
                    user.PasswordSalt = salt;
                    user.HashIterations = iterations;
                    await dbContext.SaveChangesAsync();
                }
                //

                return Results.NoContent();
            });

            app.MapGet("/logs", async (HttpContext httpContext) =>
            {
                // 1. Берём токен из заголовка
                var auth = httpContext.Request.Headers["Authorization"].ToString();
                if (!auth.StartsWith("Bearer "))
                    return Results.Unauthorized();

                var token = auth["Bearer ".Length..];

                // 2. Хешируем токен (никогда не храним чистый)
                var tokenHash = TokenHelper.HashToken(token);

                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // 3. Ищем токен в БД
                    var accessToken = await dbContext.AccessTokens
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t =>
                            t.AccessTokenHash == tokenHash &&
                            !t.Revoked &&
                            t.ExpiresAt > DateTime.UtcNow);

                    if (accessToken == null)
                        return Results.Unauthorized();

                    // 4. Проверка прав
                    if (accessToken.User.Status != "logger" && accessToken.User.Status != "developer")
                    {
                        return Results.StatusCode(403);
                    }

                    var query = httpContext.Request.Query;
                    int limit = 20;
                    int offset = 0;

                    if (query.ContainsKey("limit") && int.TryParse(query["limit"], out int parsedLimit))
                        limit = Math.Min(parsedLimit, 100); // ограничение на максимум

                    if (query.ContainsKey("offset") && int.TryParse(query["offset"], out int parsedOffset))
                        offset = Math.Max(parsedOffset, 0);

                    //if (limit < offset)
                    //    return Results.BadRequest();

                    var logs = LogsHelper.GetLogs(dbContext, offset, limit);

                    return Results.Ok(logs);
                }
            });

            app.MapGet("/ping", () => "pong");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} *** SERVER START WORKING on {_serverIp}:{_port} ***");
            Console.ResetColor();

            app.Run();

        }
        
        public record RegistrationRequest(string Username, string Email, string Password, string Birthday, string? Device_info = null);
        public record RegistrationResponse(string Token_refresh, string Token_access);
        public record LoginRequest(string Email, string Password, string? Device_info = null);
        public record LoginResponse(string Token_refresh, string Token_access);
        public record GetAccessTokenRequest(string Token_refresh, string? Device_info = null);
        public record GetAccessTokenResponse(string Token_refresh, string Token_access);
        public record ForgotPassRequest(string Email);
        public record ForgotpassVerifyRequest(string Email, string Code, string? Device_info = null);
        public record ForgotpassVerifyResponse(string Token_reset);
        public record ChangedpassRequest(string Token_reset, string Password);
    }
}
