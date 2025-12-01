using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ProjectMessenger.Data;
using ProjectMessenger.Model;
using ProjectMessenger.Helpers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ProjectMessenger
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
                Console.WriteLine("Password not found!"); 
                throw new FileNotFoundException("Password not found.");
            }
            else
            {
                return pass;
            }
        }
    }

    internal class Program
    {
        private const int _port = 1234;
        private const string _serverIp = "192.168.168.118";

        private static readonly ConcurrentDictionary<string, ClientSession> _activeClients =
            new ConcurrentDictionary<string, ClientSession>();

        static async Task Main(string[] args)
        {
            Console.Title = "Messager";

            var certificate = CertificateHelper.GetOrCreateCertificate();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2. Настройка Dependency Injection
            var services = new ServiceCollection();

            // Добавляем DbContext, указывая ему использовать SQL Server
            // и брать строку подключения из appsettings.json
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Собираем сервис-провайдер
            var serviceProvider = services.BuildServiceProvider();

            // Применяем миграции (создаем/обновляем БД) при старте
            // В реальном приложении это лучше делать отдельно
            //try 
            //{ 
            //    AssureDatabaseCreated(serviceProvider);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"DB connection error: {ex.Message}");
            //    return;
            //}

            TcpListener? listener = null;

            try
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(_serverIp), _port);

                listener = new TcpListener(ep);

                listener.Start();
                Console.WriteLine($"*** SERVER START WORKING on {_serverIp}:{_port} ***");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    //Task.Run(() => ReadMessageClientAsync(client));
                    Task.Run(() => HandleClientAsync(client, certificate, serviceProvider));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server fatal error: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
                Console.WriteLine("*** SERVER SHUT DOWN ***");
            }
        }

        private static async Task HandleClientAsync(TcpClient client, X509Certificate2 certificate, IServiceProvider serviceProvider)
        {
            using var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(certificate, clientCertificateRequired: false, checkCertificateRevocation: false);

            ClientSession? session = null;
            string clientKey = string.Empty;

            try
            {
                session = new ClientSession(client, ssl);

                //Task.Run(() => SendMessageClientAsync(session));
                clientKey = $"{session.IpAddress}:{((IPEndPoint)client.Client.RemoteEndPoint!).Port}";

                _activeClients.TryAdd(clientKey, session);

                Console.WriteLine($"*** CLIENT CONNECTED: {clientKey} ***");

                using StreamReader reader = new StreamReader(ssl, Encoding.UTF8);

                while (client.Connected && !reader.EndOfStream)
                {
                    string? message = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(message)) continue;

                    Console.WriteLine($"Received from {clientKey}: '{message}'");

                    CommandHandler(message, serviceProvider, session);
                    //
                    //if (!string.IsNullOrEmpty(response)) // Отправляем ответ, только если он не пустой (для сообщений)
                    //{
                    //    Console.WriteLine($"Sending to {clientKey}: '{response}'");
                    //    await session.Writer.WriteLineAsync(response);
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Communication error with {clientKey}: {ex.Message}");
            }
            finally
            {
                if (session != null)
                {
                    _activeClients.TryRemove(clientKey, out _);
                    client.Close();
                    Console.WriteLine($"*** CLIENT DISCONNECTED: {clientKey} ***");
                }
            }
        }

        //private static async Task SendMessageClientAsync(ClientSession session)
        //{
        //    while (true)
        //    {
        //        try
        //        {
        //            string response = Console.ReadLine();
        //            Console.WriteLine($"Sending to {session.IpAddress}: '{response}'");
        //            await session.Writer.WriteLineAsync(response);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Error sending message: {ex.Message}");
        //        }
        //    }
        //}

        //private static async Task ReadMessageClientAsync(TcpClient client)
        //{
        //    using (client)
        //    {
        //        string clientAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
        //        int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
        //        DateTime connectTime = DateTime.Now;
        //
        //        Console.WriteLine($"*** CLIENT CONNECTED ***");
        //        Console.WriteLine($"Client Info: {clientAddress}:{clientPort} | Connect Time: {connectTime}");
        //
        //        try
        //        {
        //            using NetworkStream stream = client.GetStream();
        //            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        //
        //            while (!reader.EndOfStream)
        //            {
        //                string? message = await reader.ReadLineAsync();
        //
        //                Console.WriteLine($"Received from {clientAddress}: '{message}'");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Communication error with {clientAddress}: {ex.Message}");
        //        }
        //
        //        DateTime disconnectTime = DateTime.Now;
        //        Console.WriteLine($"*** CLIENT DISCONNECTED: {clientAddress} | Disconnect Time: {disconnectTime} ***");
        //    }
        //}

        private static async Task CommandHandler(string message, IServiceProvider serviceProvider, ClientSession session)
        {
            JsonDocument.Parse(message);

            var packet = JsonSerializer.Deserialize<JsonPackage>(message);

            if (packet == null || string.IsNullOrWhiteSpace(packet.header))
            {
                await session.SendJsonAsync("error", new() { ["error"] = "EMPTY_STRING" });
                return;
            }

            if (packet.body == null) packet.body = new Dictionary<string, string>();

            string command = packet.header.Trim().ToLower();

            //if (packet.Length == 0) return "ERROR;INVALID_FORMAT";

            //string command = packet[0].ToLower();

            try
            {
                switch (command)
                {
                    case "registration_data":
                        await RegisterUserAsync(packet.body, serviceProvider, session);
                        break;
                    case "login_data":
                        await LoginUserAsync(packet.body, serviceProvider, session);
                        break;
                    case "forgotpass_data":
                        await ForgotPasswordAsync(packet.body, serviceProvider, session);
                        break;
                    case "message_data":
                        await HandleMessageAsync(packet.body, session);
                        break;
                    case "verify_data":
                        await VerifyCodeForgotpassAsync(packet.body, serviceProvider, session);
                        break;
                    default:
                        await session.SendJsonAsync("error", new() { ["error"] = "UNKNOWN_COMMAND" });
                        return;
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command '{command}' error: {ex.Message}");
                await session.SendJsonAsync("error", new() { ["error"] = $"COMMAND_FAILED: {ex.Message}" });
                return;
            }
        }

        private static async Task RegisterUserAsync(Dictionary<string, string> data, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: username ; email ; password ; birthday
            //if (args.Length != 4) return "ERROR;REG_FAILED;MISSING_DATA";
            //
            //var name = args[0];
            //var email = args[1];
            //var password = args[2];
            //var birthdayString = args[3]; // Дата рождения как строка


            //if (data.TryGetValue("username", out string name) == string.IsNullOrWhiteSpace(name)) return "ERROR;REG_FAILED;MISSING_DATA";

            data.TryGetValue("username", out string? name);
            data.TryGetValue("email", out string? email);
            data.TryGetValue("password", out string? password);
            data.TryGetValue("birthday", out string? birthdayString);

            //var name = args[0];
            //var email = args[1];
            //var password = args[2];
            //var birthdayString = args[3]; // Дата рождения как строка


            // ⚠️ Попытка парсинга даты рождения

            Console.WriteLine($"username: {name}, email: {email}, password: {password} birthday: '{birthdayString}'");

            if (!DateTime.TryParse(birthdayString, out DateTime birthday))
            {
                await session.SendJsonAsync("error", new() { ["error"] = $"REG_FAILED;INVALID_BIRTHDAY_FORMAT" });
                return;
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (await dbContext.Users.AnyAsync(u => u.Email == email))
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "REG_FAILED;EMAIL_EXISTS" });
                    return;
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

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                session.LoggedInEmail = user.Email;

                await session.SendJsonAsync("success", new() { ["success"] = "REG_OK" });
                return;
            }
        }

        private static async Task LoginUserAsync(Dictionary<string, string> data, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: email ; password
            //if (args.Length != 2) return "ERROR;LOGIN_FAILED;MISSING_DATA";

            data.TryGetValue("email", out string? email);
            data.TryGetValue("password", out string? password);

            //var email = args[0];
            //var password = args[1];

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "LOGIN_FAILED;INVALID_CREDENTIALS" });
                    return;
                }

                bool isPasswordValid = PasswordHelper.VerifyPassword(
                    password,
                    user.PasswordHash,
                    user.PasswordSalt,
                    user.HashIterations
                );

                if (isPasswordValid)
                {
                    session.LoggedInEmail = user.Email;
                    await session.SendJsonAsync("success", new() { ["success"] = $"LOGIN_OK;Welcome {user.Name}" });
                    return;
                }
                else
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "LOGIN_FAILED;INVALID_CREDENTIALS" });
                    return;
                }
            }
        }

        private static async Task ForgotPasswordAsync(Dictionary<string, string> data, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: email
            //if (args.Length != 1) return "ERROR;FORGOTPASS_FAILED;MISSING_DATA";
            data.TryGetValue("email", out string email);
            //var email = args[0];
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "FORGOTPASS_FAILED;EMAIL_NOT_FOUND" });
                    return;
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

                Console.WriteLine($"Generated confirmation code: {codeString} for email: {email}");

                var message = new MailMessage();
                message.To.Add(email);
                message.Subject = "Код подтверждения";
                message.Body = $"Ваш код: {codeString}";
                message.From = new MailAddress("bearodit@gmail.com");

                try
                {
                    var configContent = File.ReadAllText("config.json");
                    var configJson = JsonDocument.Parse(configContent);
                    var gmailHost = configJson.RootElement.GetProperty("Gmail").GetProperty("Host").GetString();
                    Console.WriteLine($"Gmail Host from config: {gmailHost}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading config.json: {ex.Message}");
                    await session.SendJsonAsync("error", new() { ["error"] = "FORGOTPASS_FAILED;CONFIG_ERROR" });
                    return;
                }

                try
                {
                    var appPassword = Environment.GetEnvironmentVariable("APP_PASSWORD", EnvironmentVariableTarget.User);
                    if (string.IsNullOrEmpty(appPassword))
                    {
                        throw new Exception("APP_PASSWORD environment variable is not set.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving APP_PASSWORD: {ex.Message}");
                    await session.SendJsonAsync("error", new() { ["error"] = "FORGOTPASS_FAILED;APP_PASSWORD_NOT_SET" });
                    return;
                }

                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(
                        JsonDocument.Parse(File.ReadAllText("config.json"))
                            .RootElement
                            .GetProperty("Gmail")
                            .GetProperty("Host")
                            .GetString(),
                            Environment.GetEnvironmentVariable("APP_PASSWORD", EnvironmentVariableTarget.User)),
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

                dbContext.PasswordResetTokens.Add(tokenModel);
                await dbContext.SaveChangesAsync();

                var oldTokens = dbContext.PasswordResetTokens.Where(t => t.UserId == user.Id && !t.Used);
                dbContext.PasswordResetTokens.RemoveRange(oldTokens);

                //session.SendJsonAsync("success", new() { ["success"] = "FORGOTPASS_SUCCESS;CONFIRMATION_CODE_SENT" });
                await session.SendJsonAsync("success", new() { ["success"] = "mail_sended" });

                return;
            }
        }

        private static async Task VerifyCodeForgotpassAsync(Dictionary<string, string> data, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: email ; code ; new_password
            //if (args.Length != 3) return "ERROR;VERIFY_FAILED;MISSING_DATA";
            data.TryGetValue("email", out string email);
            data.TryGetValue("code", out string code);
            //var email = args[0];
            //var code = args[1];
            //var newPassword = args[2];
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "VERIFY_FAILED;EMAIL_NOT_FOUND" });
                    return;
                }
                var codeHash = Convert.ToBase64String(
                    SHA256.HashData(Encoding.UTF8.GetBytes(code))
                );
                var token = await dbContext.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == codeHash && !t.Used && t.ExpiresAt > DateTime.UtcNow);
                if (token == null)
                {
                    await session.SendJsonAsync("error", new() { ["error"] = "VERIFY_FAILED;INVALID_OR_EXPIRED_CODE" });
                    return;
                }
                // Обновляем пароль пользователя
                await session.SendJsonAsync("success", new() { ["success"] = "VERIFY_SUCCESS;CODE_CONFIRMED" });
                return;
            }
        }

        private static async Task HandleMessageAsync(Dictionary<string, string> data, ClientSession session)
        {
            //if (args.Length != 1) return "ERROR;MESSAGE_FAILED;MISSING_DATA";

            if (string.IsNullOrEmpty(session.LoggedInEmail))
            {
                await session.SendJsonAsync("error", new() { ["error"] = "MESSAGE_FAILED;NOT_LOGGED_IN" });
                return;
            }

            data.TryGetValue("message", out string message);


            //string message = args[0];

            string sender = session.LoggedInEmail;
            string timeStamp = DateTime.Now.ToShortTimeString();

            Console.WriteLine($"[CHAT] {timeStamp} <{sender}>: {message}");

            string broadcastMessage = $"MESSAGE;{timeStamp};{sender};{message}";

            var sendTasks = _activeClients.Values
                .Where(s => !string.IsNullOrEmpty(s.LoggedInEmail) && s.LoggedInEmail != session.LoggedInEmail)
                .Select(s => s.Writer.WriteLineAsync(broadcastMessage));

            await Task.WhenAll(sendTasks);

            // Отправляем подтверждение отправителю
            await session.SendJsonAsync("success", new() { ["success"] = "MESSAGE_SENT" });
            return;
        }
    }
}
