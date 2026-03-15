using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectMessengerServer.Application.Services;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;
using ProjectMessengerServer.Infrastructure.Logging;
using ProjectMessengerServer.Infrastructure.Security;
using ProjectMessengerServer.Infrastructure.WebSockets;

namespace ProjectMessengerServer
{
    public class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private const int _port = 1234;
        private const string _serverIp = "192.168.168.118";
        //private const string _serverIp = "141.105.132.149";

        public static void Main(string[] args)
        {
            Console.Title = "Messager";

            var certificate = CertificateManager.GetOrCreateCertificate();

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            //services.AddDbContext<AppDbContext>(options =>
            //    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            var builder = WebApplication.CreateBuilder(args);

            // добавляем DbContext сразу в DI приложения
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            // Собираем сервис-провайдер
            //var serviceProvider = new ServiceCollection().BuildServiceProvider();

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<TokenService>();
            builder.Services.AddScoped<PasswordResetService>();
            builder.Services.AddScoped<ChatService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<DeviceService>();
            builder.Services.AddScoped<ProfileService>();

            builder.Services.AddScoped<WsHandler>();
            builder.Services.AddScoped<WsMessageService>();
            builder.Services.AddSingleton<WsConnectionManager>();
            builder.Services.AddScoped<WsEventService>();
            builder.Services.AddScoped<WsSender>();

            builder.Services.AddScoped<LogManager>();

            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    };
                });

            builder.Services.AddControllers();

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

            try
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB connection or migration error: {ex.Message}");
                return;
            }

            app.UseWebSockets();

            app.UseMiddleware<RequestLoggingMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<ResponseLoggingMiddleware>();

            app.Map("/ws", async ctx =>
            {
                Console.WriteLine($"New WS connection from {ctx.Connection.RemoteIpAddress}");

                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    Console.WriteLine("Not a WebSocket request");
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Not a WebSocket request");
                    return;
                }

                var authHeader = ctx.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    Console.WriteLine("Missing or malformed Authorization header");
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("Missing token");
                    return;
                }

                var token = authHeader["Bearer ".Length..].Trim();
                Console.WriteLine($"Token received: {token}");

                var key = Encoding.UTF8.GetBytes(ctx.RequestServices.GetRequiredService<IConfiguration>()["Jwt:Key"]!);
                var tokenHandler = new JwtSecurityTokenHandler();

                try
                {
                    var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = ctx.RequestServices.GetRequiredService<IConfiguration>()["Jwt:Issuer"],
                        ValidAudience = ctx.RequestServices.GetRequiredService<IConfiguration>()["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    }, out var validatedToken);

                    Console.WriteLine("JWT validated successfully");

                    var subClaim = principal.Claims.FirstOrDefault(c =>
                        c.Type == JwtRegisteredClaimNames.Sub || // "sub"
                        c.Type == ClaimTypes.NameIdentifier      // иногда "sub" мапится сюда
                    );

                    if (subClaim == null || !int.TryParse(subClaim.Value, out int userId))
                    {
                        Console.WriteLine($"Sub claim not found or not int. Claims available:");
                        foreach (var c in principal.Claims)
                            Console.WriteLine($"  {c.Type} = {c.Value}");

                        ctx.Response.StatusCode = 401;
                        await ctx.Response.WriteAsync("Invalid user");
                        return;
                    }

                    Console.WriteLine($"UserId from Sub claim: {userId}");

                    var socket = await ctx.WebSockets.AcceptWebSocketAsync();
                    Console.WriteLine("WebSocket connection accepted");

                    var wsHandler = ctx.RequestServices.GetRequiredService<WsHandler>();
                    await wsHandler.HandleAsync(socket, userId);
                }
                catch (SecurityTokenException ex)
                {
                    Console.WriteLine($"JWT validation failed: {ex.Message}");
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("Invalid token");
                }
            });
            app.MapControllers();

            app.MapGet("/ping", () => "pong");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} *** SERVER START WORKING on {_serverIp}:{_port} ***");
            Console.ResetColor();

            app.Run();

        }
    }
}
