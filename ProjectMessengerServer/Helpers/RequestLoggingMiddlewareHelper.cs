using System.Reflection.PortableExecutable;
using System.Text;
using ProjectMessengerServer.Data;

namespace ProjectMessengerServer.Helpers
{
    public class RequestLoggingMiddlewareHelper
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddlewareHelper(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            if (!context.Request.ContentType?.Contains("application/json") == true)
            {
                await _next(context);
                return;
            }

            // ===== IP клиента =====
            var ip = context.Connection.RemoteIpAddress?.ToString();


            // ===== Метод и путь =====
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("----- HTTP REQUEST -----");
            Console.WriteLine($"Time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
            Console.WriteLine($"IP: {ip}");
            Console.WriteLine($"{context.Request.Method} {context.Request.Path}");

            // ===== Headers =====
            Console.WriteLine("Headers:");
            foreach (var header in context.Request.Headers)
            {   
                Console.WriteLine($"{header.Key}: {header.Value}");
            }

            // ===== Body =====
            context.Request.EnableBuffering(); // 🔥 важно

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                Console.WriteLine("Body:");
                Console.WriteLine(body);
            }
            else
            {
                Console.WriteLine("Body: <empty>");
            }

            Console.WriteLine("------------------------");
            Console.ResetColor();

            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();

            await LogsHelper.AddLog("INFO", $"Request from IP: {ip}, Method: {context.Request.Method}, Path: {context.Request.Path}", dbContext);

            await _next(context);
        }
    }
}
