using ProjectMessengerServer.Data;

namespace ProjectMessengerServer.Helpers
{
    public class ResponseLoggingMiddlewareHelper
    {
        private readonly RequestDelegate _next;

        public ResponseLoggingMiddlewareHelper(RequestDelegate next)
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

            var originalBodyStream = context.Response.Body;

            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context); // выполняется endpoint

            // читаем ответ
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            // ===== IP клиента =====
            var ip = context.Connection.RemoteIpAddress?.ToString();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("----- HTTP RESPONSE -----");
            Console.WriteLine($"Time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");

            Console.WriteLine($"IP: {ip}");

            Console.WriteLine($"Status: {context.Response.StatusCode}");

            Console.WriteLine("Headers:");
            foreach (var header in context.Response.Headers)
            {
                Console.WriteLine($"{header.Key}: {header.Value}");
            }

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                Console.WriteLine("Body:");
                Console.WriteLine(bodyText);
            }
            else
            {
                Console.WriteLine("Body: <empty>");
            }

            Console.WriteLine("-------------------------");
            Console.ResetColor();

            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();

            await LogsHelper.AddLog("INFO", $"Response to IP: {ip}, Status: {context.Response.StatusCode}", dbContext);

            // отправляем ответ клиенту
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }
}
