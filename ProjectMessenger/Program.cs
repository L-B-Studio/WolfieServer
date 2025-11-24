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

namespace ProjectMessenger
{
    class CertPasswordManager
    {
        private static readonly string PasswordFile = "pfx_pwd.bin";

        // Сохраняет пароль в зашифрованном виде (один раз)
        public static void SavePassword(string plainPassword)
        {
            byte[] data = Encoding.UTF8.GetBytes(plainPassword);
            byte[] encrypted = ProtectedData.Protect(
                data,
                null, // можно добавить "salt" байты, если хочешь
                DataProtectionScope.CurrentUser);

            File.WriteAllBytes(PasswordFile, encrypted);
            Console.WriteLine("Password saved (encrypted).");
        }

        // Читает и расшифровывает пароль
        public static string LoadPassword()
        {
            if (!File.Exists(PasswordFile))
                throw new FileNotFoundException("Password file not found.");

            byte[] encrypted = File.ReadAllBytes(PasswordFile);
            byte[] decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
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
                .SetBasePath(Directory.GetCurrentDirectory())
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

            if (packet == null || string.IsNullOrWhiteSpace(packet.header)) session.SendJsonAsync("error", new() { ["error"] = "EMPTY_STRING" });

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
                    default:
                        session.SendJsonAsync("error", new() { ["error"] = "UNKNOWN_COMMAND" });
                        return;
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command '{command}' error: {ex.Message}");
                session.SendJsonAsync("error", new() { ["error"] = $"COMMAND_FAILED: {ex.Message}" });
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

            data.TryGetValue("username", out string name);
            data.TryGetValue("email", out string email);
            data.TryGetValue("password", out string password);
            data.TryGetValue("birthday", out string birthdayString);

            //var name = args[0];
            //var email = args[1];
            //var password = args[2];
            //var birthdayString = args[3]; // Дата рождения как строка


            // ⚠️ Попытка парсинга даты рождения

            Console.WriteLine($"username: {name}, email: {email}, password: {password} birthday: '{birthdayString}'");

            if (!DateTime.TryParse(birthdayString, out DateTime birthday))
            {
                session.SendJsonAsync("error", new() { ["error"] = $"REG_FAILED;INVALID_BIRTHDAY_FORMAT" });
                return;
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (await dbContext.Users.AnyAsync(u => u.Email == email))
                {
                    session.SendJsonAsync("error", new() { ["error"] = "REG_FAILED;EMAIL_EXISTS" });
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

                session.SendJsonAsync("success", new() { ["success"] = "REG_OK" });
                return;
            }
        }

        private static async Task LoginUserAsync(Dictionary<string, string> data, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: email ; password
            //if (args.Length != 2) return "ERROR;LOGIN_FAILED;MISSING_DATA";

            data.TryGetValue("email", out string email);
            data.TryGetValue("password", out string password);

            //var email = args[0];
            //var password = args[1];

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    session.SendJsonAsync("error", new() { ["error"] = "LOGIN_FAILED;INVALID_CREDENTIALS" });
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
                    session.SendJsonAsync("success", new() { ["success"] = $"LOGIN_OK;Welcome {user.Name}" });
                    return;
                }
                else
                {
                    session.SendJsonAsync("error", new() { ["error"] = "LOGIN_FAILED;INVALID_CREDENTIALS" });
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
                    session.SendJsonAsync("error", new() { ["error"] = "FORGOTPASS_FAILED;EMAIL_NOT_FOUND" });
                    return;
                }
                // Здесь должна быть логика отправки письма с восстановлением пароля
                // Для упрощения примера, мы просто отправим успешный ответ


                var rng = RandomNumberGenerator.Create();
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;

                string code = value.ToString("D6"); // например: 047912
                Console.WriteLine($"Generated confirmation code: {code} for email: {email}");

                var message = new MailMessage();
                message.To.Add(email);
                message.Subject = "Код подтверждения";
                message.Body = $"Ваш код: {code}";
                message.From = new MailAddress("bearodit@gmail.com");

                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential("bearodit@gmail.com", "vsnl kzwi wxya dejw"),
                    EnableSsl = true
                };

                await smtp.SendMailAsync(message);

                var codeHash = Convert.ToBase64String(
                    SHA256.HashData(Encoding.UTF8.GetBytes(code))
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

                //vsnl kzwi wxya dejw

                var oldTokens = dbContext.PasswordResetTokens.Where(t => t.UserId == user.Id && !t.Used);
                dbContext.PasswordResetTokens.RemoveRange(oldTokens);

                session.SendJsonAsync("success", new() { ["success"] = "CONFIRMATION_CODE_SENT" });

                return;
            }
        }


        private static async Task HandleMessageAsync(Dictionary<string, string> data, ClientSession session)
        {
            //if (args.Length != 1) return "ERROR;MESSAGE_FAILED;MISSING_DATA";

            if (string.IsNullOrEmpty(session.LoggedInEmail))
            {
                session.SendJsonAsync("error", new() { ["error"] = "MESSAGE_FAILED;NOT_LOGGED_IN" });
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
            session.SendJsonAsync("success", new() { ["success"] = "MESSAGE_SENT" });
            return;
        }
    }
}
