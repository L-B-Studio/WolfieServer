using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Data;
using ProjectMessengerServer.Model;
using ProjectMessengerServer.Helpers;
using System.Security.Cryptography;
using System.Net.Mail;
using System.Net.WebSockets;
using static System.Formats.Asn1.AsnWriter;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Data;
using static ProjectMessengerServer.Model.ChatMember;

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


            app.UseWebSockets();

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
                string? deviceId = req.Device_id;
                string? deviceType = req.Device_type;
                string? placeAuthorization = req.Place_authorization;

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
                        Email = email,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        HashIterations = iterations,
                        CreatedAt = DateTime.UtcNow
                    };


                    var userDevice = new UserDevice
                    {
                        User = user,
                        DeviceId = deviceId ?? null,
                        DeviceType = deviceType ?? null,
                        PlaceAuthorization = placeAuthorization ?? null,
                        LastActive = DateTime.UtcNow
                    };

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

                    var userProfile = new UserProfile
                    {
                        User = user,
                        Name = name,
                        PublicId = RandomStringGeneratorHelper.GenerateRandomString(6),
                        PhoneNumber = null,
                        AvatarUrl = null,
                        Birthday = birthday,
                        Bio = null
                    };

                    try
                    {
                        dbContext.Users.Add(user);
                        dbContext.UserDevices.Add(userDevice);
                        dbContext.UserSettings.Add(userSetting);
                        dbContext.UserPrivacies.Add(userPrivacy);
                        dbContext.UserProfiles.Add(userProfile);


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
                string? deviceId = req.Device_id;
                string? deviceType = req.Device_type;
                string? placeAuthorization = req.Place_authorization;

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
                        var checkDeviceId = await dbContext.UserDevices
                            .FirstOrDefaultAsync(ud => ud.UserId == user.Id && ud.DeviceId == deviceId);

                        if (checkDeviceId == null)
                        {
                            var newUserDevice = new UserDevice
                            {
                                User = user,
                                DeviceId = deviceId ?? null,
                                DeviceType = deviceType ?? null,
                                PlaceAuthorization = placeAuthorization ?? null,
                                LastActive = DateTime.UtcNow
                            };
                            dbContext.UserDevices.Add(newUserDevice);
                        }

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
                string? deviceId = req.Device_id;
                string? deviceType = req.Device_type;
                string? placeAuthorization = req.Place_authorization;

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

            app.MapPost("/auth/password/verify", async (ForgotPassVerifyRequest req, HttpContext httpContext) =>
            {


                //data.TryGetValue("email", out string email);
                //data.TryGetValue("code", out string code);
                //data.TryGetValue("device_info", out string deviceInfo);

                string email = req.Email;
                string code = req.Code;

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


                return Results.Ok(new ForgotPassVerifyResponse(
                    Token_reset: hashTokenReset
                ));
            });

            app.MapPost("/auth/password/change", async (ChangedPassRequest req, HttpContext httpContext) =>
            {
                //data.TryGetValue("token_reset", out string token);
                //data.TryGetValue("password", out string newPassword);

                string token = req.Token_reset;
                string newPassword = req.Password;
                string? deviceId = req.Device_id;
                string? deviceType = req.Device_type;
                string? placeAuthorization = req.Place_authorization;

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

                    var checkDeviceId = await dbContext.ForgotPassDevices
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
                        dbContext.UserDevices.Add(newUserDevice);
                    }

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

            app.MapPost("/chats", async (CreateChatRequest req, HttpContext httpContext) =>
            {
                var auth = httpContext.Request.Headers["Authorization"].ToString();
                if (!auth.StartsWith("Bearer "))
                {
                    return Results.Unauthorized();
                }

                var token = auth["Bearer ".Length..];

                using var scope = httpContext.RequestServices.CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var user = await ValidateAccessTokenHelper.ValidateToken(token, dbContext);

                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }


                    string chatName = req.Chat_name;
                    string chatType = req.Chat_type;


                    if (string.IsNullOrWhiteSpace(chatName) ||
                        string.IsNullOrWhiteSpace(chatType))
                    {
                        return Results.BadRequest();
                    }

                    if (chatType != "private" && chatType != "group")
                    {
                        return Results.BadRequest();
                    }

                    var memberUids = req.Member_uids ?? new List<string>();

                    var validMemberIds = memberUids
                        .Where(uid => !string.IsNullOrWhiteSpace(uid))
                        .Distinct()
                        .Select(uid =>
                        {
                            var memberProfile = dbContext.UserProfiles
                                .FirstOrDefault(up => up.PublicId == uid);
                            if (memberProfile == null || memberProfile.UserId == user.Id)
                                return -1;
                            return memberProfile.UserId;
                        })
                        .ToList();

                    validMemberIds = validMemberIds
                        .Where(id => id != -1)
                        .ToList();

                    validMemberIds.Add(user.Id);


                    var chat = new Chat
                    {
                        Uid = RandomStringGeneratorHelper.GenerateRandomString(6),
                        Type = chatType,
                        Name = chatName,
                        CreatedAt = DateTime.UtcNow,
                        Members = new List<ChatMember>()
                    };

                    ChatMember chatMember;

                    foreach (var memberId in validMemberIds)
                    {
                        var member = await dbContext.Users.FindAsync(memberId);
                        if (member == null)
                        {
                            continue;
                        }
                        ChatRole role;
                        if (user.Id == member.Id)
                        {
                            continue;
                        }

                        chatMember = new ChatMember
                        {
                            ChatId = chat.Id,
                            UserId = member.Id,
                            JoinedAt = DateTime.UtcNow,
                            Role = ChatRole.Member
                        };
                        chat.Members.Add(chatMember);
                        dbContext.ChatMembers.Add(chatMember);
                    }

                    chatMember = new ChatMember
                    {
                        ChatId = chat.Id,
                        UserId = user.Id,
                        JoinedAt = DateTime.UtcNow,
                        Role = ChatRole.Owner
                    };
                    chat.Members.Add(chatMember);
                    dbContext.ChatMembers.Add(chatMember);

                    dbContext.Chats.Add(chat);

                    var request = new Dictionary<string, string>
                    {
                        { "chat_uid", chat.Uid.ToString() },
                        { "chat_name", chat.Name },
                        { "chat_type", chat.Type }
                    };

                    await EventManager(user, dbContext, request, "create_chat", validMemberIds);

                    await dbContext.SaveChangesAsync();


                    return Results.Ok(new CreateChatResponse(chat.Uid.ToString()));
                }
            });

            app.MapPost("/chats/{chatUid}/join", async (HttpContext httpContext) =>
            {
                var auth = httpContext.Request.Headers["Authorization"].ToString();
                if (!auth.StartsWith("Bearer "))
                {
                    return Results.Unauthorized();
                }
                var token = auth["Bearer ".Length..];
                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await ValidateAccessTokenHelper.ValidateToken(token, dbContext);
                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }
                    var chatUid = httpContext.Request.RouteValues["chatUid"]?.ToString();

                    var checkMember = await dbContext.ChatMembers
                        .FirstOrDefaultAsync(cm => cm.UserId == user.Id && cm.Chat.Uid == chatUid);

                    if (checkMember != null)
                    {
                        return Results.Conflict();
                    }

                    if (string.IsNullOrWhiteSpace(chatUid))
                    {
                        return Results.BadRequest();
                    }
                    var chat = await dbContext.Chats
                        .Include(c => c.Members)
                        .FirstOrDefaultAsync(c => c.Uid == chatUid);

                    if (chat == null)
                    {
                        return Results.NotFound();
                    };

                    if (chat.Type == "private")
                    {
                        return Results.StatusCode(403);
                    }

                    var chatMember = new ChatMember
                    {
                        ChatId = chat.Id,
                        UserId = user.Id,
                        JoinedAt = DateTime.UtcNow,
                        Role = ChatRole.Member
                    };

                    dbContext.ChatMembers.Add(chatMember);
                    chat.Members.Add(chatMember);
                    await dbContext.SaveChangesAsync();
                    return Results.NoContent();
                }
            });

            app.MapGet("/logs", async (HttpContext httpContext) =>
            {
                // 1. Берём токен из заголовка
                var auth = httpContext.Request.Headers["Authorization"].ToString();
                if (!auth.StartsWith("Bearer "))
                {
                    return Results.Unauthorized();
                }

                var token = auth["Bearer ".Length..];

                using var scope = builder.Services.BuildServiceProvider().CreateScope();
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // 3. Ищем токен в БД
                    var user = await ValidateAccessTokenHelper.ValidateToken(token, dbContext);

                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }

                    // 4. Проверка прав
                    if (user.Status != "logger" && user.Status != "developer")
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

            app.Map("/ws", async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }

                var auth = ctx.Request.Headers["Authorization"].ToString();
                if (!auth.StartsWith("Bearer "))
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var token = auth["Bearer ".Length..];
                using var scope = ctx.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await ValidateAccessTokenHelper.ValidateToken(token, dbContext);

                if (user == null)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                Console.WriteLine($"WS connected: userId={user.Id}");


                var userSequence = await dbContext.UserSequences
                    .FirstOrDefaultAsync(us => us.UserId == user.Id);

                if (userSequence == null)
                {
                    userSequence = new UserSequence
                    {
                        UserId = user.Id,
                        LastSeq = 1
                    };

                    dbContext.UserSequences.Add(userSequence);
                    await dbContext.SaveChangesAsync();
                }

                int currentSeq = userSequence.LastSeq;

                if (!WsConnectionManager.Connections.ContainsKey(user.Id))
                {
                    WsConnectionManager.Connections[user.Id] = new List<WebSocket>();
                }

                WsConnectionManager.Connections[user.Id].Add(ws);
                try
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        var buffer = new byte[4096];

                        WebSocketReceiveResult result;

                        try
                        {
                            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                        }
                        catch (WebSocketException ex)
                        {
                            Console.WriteLine($"WS receive error [{user.Id}]: {ex.Message}");
                            break;
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine($"WS close frame [{user.Id}]");
                            break;
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        WsEnvelope? message;
                        try
                        {
                            message = JsonSerializer.Deserialize<WsEnvelope>(json);
                        }
                        catch (JsonException)
                        {
                            Console.WriteLine($"[{user.Id}] JSON deserialization error.");
                            Console.WriteLine(json);
                            continue;
                        }

                        if (message == null)
                        {
                            Console.WriteLine($"[{user.Id}] Invalid message format.");
                            continue;
                        }

                        await ReceiveLoop(user, dbContext, message.Data, message.Op, ws);
                    }
                }
                finally
                {
                    WsConnectionManager.Connections[user.Id].Remove(ws);

                    try
                    {
                        await ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None
                        );
                    }
                    catch { }
                }

                Console.WriteLine($"WS disconnected: userId={user.Id}");
            });

            static async Task EventManager(User user, AppDbContext dbContext, Dictionary<string, string> req, string operation, List<int> memberIds)
            {
                try
                {
                    WsEnvelope envelope;


                    switch (operation)
                    {
                        case "create_chat":
                            req.TryGetValue("chat_uid", out string? chatUid);
                            req.TryGetValue("chat_name", out string? messageText);
                            req.TryGetValue("chat_type", out string? chatType);

                            envelope = new WsEnvelope(
                                "new_chat",
                                new()
                                {
                                    ["chat_uid"] = chatUid,
                                    ["chat_name"] = messageText,
                                    ["chat_type"] = chatType
                                },
                                Seq: null
                            );


                            foreach (var chatUserId in memberIds)
                            {
                                if (WsConnectionManager.Connections.ContainsKey(chatUserId))
                                {
                                    var connections = WsConnectionManager.Connections[chatUserId];
                                    foreach (var connection in connections)
                                    {
                                        await SendLoop(connection, user, dbContext, envelope);
                                    }
                                }
                            }

                            break;

                        default:
                            Console.WriteLine($"Unknown operation: {operation}");

                            return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WS receive error [{user.Id}]: {ex.Message}");
                }
            }

            static async Task ReceiveLoop(User user, AppDbContext dbContext, Dictionary<string, string> req, string operation, WebSocket? ws = null)
            {
                try
                {
                    WsEnvelope envelope;

                    switch (operation)
                    {
                        case "send_message":
                            // Обработка операции отправки сообщения

                            req.TryGetValue("chat_uid", out string? chatUid);
                            req.TryGetValue("message_text", out string? messageText);

                            if (string.IsNullOrWhiteSpace(chatUid) || string.IsNullOrWhiteSpace(messageText))
                            {
                                Console.WriteLine($"[{user.Id}] send_message failed: Missing required fields.");
                                envelope = new WsEnvelope(
                                    "error",
                                    new()
                                    {
                                        ["code"] = "MISSING_REQUIRED_FIELDS",
                                        ["message"] = ""
                                    },
                                    Seq: null
                                );
                                Console.WriteLine($"[{user.Id}] Sending response: {JsonSerializer.Serialize(envelope)}");
                                SendLoop(ws, user, dbContext, envelope);
                                return;
                            }

                            if (!await dbContext.ChatMembers.AnyAsync(cm => cm.Chat.Uid == chatUid && cm.UserId == user.Id))
                            {
                                Console.WriteLine($"[{user.Id}] send_message failed: User not in chat.");
                                envelope = new WsEnvelope(
                                    "error",
                                    new()
                                    {
                                        ["code"] = "USER_NOT_IN_CHAT",
                                        ["message"] = ""
                                    },
                                    Seq: null
                                );
                                Console.Write($"[{user.Id}] Sending response: {JsonSerializer.Serialize(envelope)}");
                                SendLoop(ws, user, dbContext, envelope);
                                return;
                            }

                            var chatId = await dbContext.Chats
                                .Where(c => c.Uid == chatUid)
                                .Select(c => c.Id)
                                .FirstAsync();

                            var Message = new Message
                            {
                                ChatId = chatId,
                                SenderId = user.Id,
                                Text = messageText,
                                CreatedAt = DateTime.UtcNow
                            };

                            dbContext.Messages.Add(Message);
                            var chatUsers = await dbContext.Chats
                                .Where(c => c.Uid == chatUid)
                                .SelectMany(c => c.Members)
                                .Select(cm => cm.UserId)
                                .ToListAsync();

                            envelope = new WsEnvelope(
                                "new_message",
                                new()
                                {
                                    ["chat_uid"] = chatUid,
                                    ["message_id"] = Message.Id.ToString(),
                                    ["sender_id"] = Message.SenderId.ToString(),
                                    ["message_text"] = Message.Text,
                                    ["created_at"] = Message.CreatedAt.ToString("o")
                                },
                                Seq: null
                            );

                            foreach (var chatUserId in chatUsers)
                            {
                                if (chatUserId == user.Id)
                                    continue;
                                if (WsConnectionManager.Connections.ContainsKey(chatUserId))
                                {
                                    var connections = WsConnectionManager.Connections[chatUserId];
                                    foreach (var connection in connections)
                                    {
                                        await SendLoop(connection, user, dbContext, envelope);
                                    }
                                }
                            }

                            await dbContext.SaveChangesAsync();

                            break;

                        default:
                            Console.WriteLine($"[{user.Id}] Unknown operation: {operation}");

                            envelope = new WsEnvelope(
                                "error",
                                new ()
                                {
                                    ["code"] = "OPERATION_IS_NOT_CORRECT",
                                    ["message"] = ""
                                },
                                Seq: null
                            );

                            Console.Write($"[{user.Id}] Sending response: {JsonSerializer.Serialize(envelope)}");

                            SendLoop(ws, user, dbContext, envelope);

                            return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WS receive error [{user.Id}]: {ex.Message}");
                }
            }

            static async Task SendLoop(WebSocket ws, User user, AppDbContext dbContext, WsEnvelope envelope)
            {
                if (ws.State != WebSocketState.Open)
                    return;

                var userSequence = await dbContext.UserSequences.FirstAsync(x => x.UserId == user.Id);

                envelope = envelope with { Seq = userSequence.LastSeq };

                var json = JsonSerializer.Serialize(envelope);
                var bytes = Encoding.UTF8.GetBytes(json);

                try
                {
                    await ws.SendAsync(
                        bytes,
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );

                    userSequence.LastSeq++;
                    await dbContext.SaveChangesAsync();
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"Send loop error: {ex.Message}");
                    return;
                }

            }


            app.MapGet("/ping", () => "pong");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} *** SERVER START WORKING on {_serverIp}:{_port} ***");
            Console.ResetColor();

            app.Run();

        }


    }

    public record RegistrationRequest(string Username, string Email, string Password, string Birthday, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
    public record RegistrationResponse(string Token_refresh, string Token_access);
    public record LoginRequest(string Email, string Password, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
    public record LoginResponse(string Token_refresh, string Token_access);
    public record CreateChatRequest(string Chat_name, string Chat_type, List<string> Member_uids); 
    public record CreateChatResponse(string Chat_uid);
    public record GetAccessTokenRequest(string Token_refresh, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
    public record GetAccessTokenResponse(string Token_refresh, string Token_access);
    public record ForgotPassRequest(string Email);
    public record ForgotPassVerifyRequest(string Email, string Code);
    public record ForgotPassVerifyResponse(string Token_reset);
    public record ChangedPassRequest(string Token_reset, string Password, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
    public record WsEnvelope(string Op, Dictionary<string, string> Data, int? Seq);
}
