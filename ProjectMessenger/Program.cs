using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ProjectMessenger.Data;
using ProjectMessenger.Model;
using ProjectMessenger.Helpers;
using System.Collections.Concurrent;


namespace ProjectMessenger
{
    public class ClientSession
    {
        public TcpClient Client { get; set; }
        public string IpAddress { get; set; }
        public string? LoggedInEmail { get; set; } // Email, если пользователь залогинен
        public NetworkStream Stream { get; set; }
        public StreamWriter Writer { get; set; }

        public ClientSession(TcpClient client)
        {
            Client = client;
            IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            Stream = client.GetStream();
            Writer = new StreamWriter(Stream, Encoding.UTF8) { AutoFlush = true };
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

            TcpListener ?listener = null;
            
            try
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(_serverIp), _port);
            
                listener = new TcpListener(ep);
            
                listener.Start();
                Console.WriteLine($"*** SERVER START WORKING on {_serverIp}:{_port} ***");
            
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    //Task.Run(() => SendMessageClientAsync(client));
                    //Task.Run(() => ReadMessageClientAsync(client));
                    Task.Run(() => HandleClientAsync(client, serviceProvider));
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

        //private static void AssureDatabaseCreated(IServiceProvider serviceProvider)
        //{
        //    using (var scope = serviceProvider.CreateScope())
        //    {
        //        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //        // dbContext.Database.EnsureCreated(); // Создает БД, но не позволяет использовать миграции
        //        dbContext.Database.Migrate(); // Применяет существующие миграции
        //        Console.WriteLine("Database connection successful and migrations applied.");
        //    }
        //}

        private static async Task HandleClientAsync(TcpClient client, IServiceProvider serviceProvider)
        {
            ClientSession? session = null;
            string clientKey = string.Empty;

            try
            {
                session = new ClientSession(client);
                clientKey = $"{session.IpAddress}:{((IPEndPoint)client.Client.RemoteEndPoint!).Port}";
                
                _activeClients.TryAdd(clientKey, session);

                Console.WriteLine($"*** CLIENT CONNECTED: {clientKey} ***");
                
                using StreamReader reader = new StreamReader(session.Stream, Encoding.UTF8);

                while (client.Connected && !reader.EndOfStream)
                {
                    string? message = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(message)) continue;

                    Console.WriteLine($"Received from {clientKey}: '{message}'");

                    string response = await CommandHandler(message, serviceProvider, session);

                    if (!string.IsNullOrEmpty(response)) // Отправляем ответ, только если он не пустой (для сообщений)
                    {
                        await session.Writer.WriteLineAsync(response);
                    }
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
        
        //private static async Task SendMessageClientAsync(TcpClient client)
        //{
        //    try
        //    {
        //        NetworkStream stream = client.GetStream();
        //        using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        //
        //        while (client.Connected)
        //        {
        //            string? message = Console.ReadLine();
        //
        //            await writer.WriteLineAsync(message);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error sending message: {ex.Message}");
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

        private static async Task<string> CommandHandler(string message, IServiceProvider serviceProvider, ClientSession session)
        {
            string[] parts = message.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => p.Trim())
                                    .ToArray();

            if (parts.Length == 0) return "ERROR;INVALID_FORMAT";

            string command = parts[0].ToLower();

            try
            {
                return command switch
                {
                    "registration_data" => await RegisterUserAsync(parts.Skip(1).ToArray(), serviceProvider, session),
                    "login_data" => await LoginUserAsync(parts.Skip(1).ToArray(), serviceProvider, session),
                    "message_data" => await HandleMessageAsync(parts.Skip(1).ToArray(), session),
                    _ => "ERROR;UNKNOWN_COMMAND"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command '{command}' error: {ex.Message}");
                return $"ERROR;COMMAND_FAILED;{ex.Message}";
            }
        }

        private static async Task<string> RegisterUserAsync(string[] args, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: username ; email ; password ; birthday
            if (args.Length != 4) return "ERROR;REG_FAILED;MISSING_DATA";

            var name = args[0];
            var email = args[1];
            var password = args[2];
            var birthdayString = args[3]; // Дата рождения как строка

            // ⚠️ Попытка парсинга даты рождения
            if (!DateTime.TryParse(birthdayString, out DateTime birthday))
            {
                return "ERROR;REG_FAILED;INVALID_BIRTHDAY_FORMAT";
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (await dbContext.Users.AnyAsync(u => u.Email == email))
                {
                    return "ERROR;REG_FAILED;EMAIL_EXISTS";
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

                return "SUCCESS;REG_OK";
            }
        }

        private static async Task<string> LoginUserAsync(string[] args, IServiceProvider serviceProvider, ClientSession session)
        {
            // Ожидаемый формат: email ; password
            if (args.Length != 2) return "ERROR;LOGIN_FAILED;MISSING_DATA";

            var email = args[0];
            var password = args[1];

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return "ERROR;LOGIN_FAILED;INVALID_CREDENTIALS";
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
                    return $"SUCCESS;LOGIN_OK;Welcome,{user.Name}";
                }
                else
                {
                    return "ERROR;LOGIN_FAILED;INVALID_CREDENTIALS";
                }
            }
        }

        private static async Task<string> HandleMessageAsync(string[] args, ClientSession session)
        {
            if (args.Length != 1) return "ERROR;MESSAGE_FAILED;MISSING_DATA";

            if (string.IsNullOrEmpty(session.LoggedInEmail))
            {
                return "ERROR;MESSAGE_FAILED;NOT_LOGGED_IN";
            }

            string message = args[0];
            string sender = session.LoggedInEmail;
            string timeStamp = DateTime.Now.ToShortTimeString();

            Console.WriteLine($"[CHAT] {timeStamp} <{sender}>: {message}");

            string broadcastMessage = $"MESSAGE;{timeStamp};{sender};{message}";

            var sendTasks = _activeClients.Values
                .Where(s => !string.IsNullOrEmpty(s.LoggedInEmail) && s.LoggedInEmail != session.LoggedInEmail)
                .Select(s => s.Writer.WriteLineAsync(broadcastMessage));

            await Task.WhenAll(sendTasks);

            // Отправляем подтверждение отправителю
            return $"SUCCESS;MESSAGE_SENT";
        }
    }
}
