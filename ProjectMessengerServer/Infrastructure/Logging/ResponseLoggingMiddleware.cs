using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Infrastructure.Logging
{
    public class ResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public ResponseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, LogManager _logManager)
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

            string ip;

            try
            {
                await _next(context);

                responseBody.Seek(0, SeekOrigin.Begin);
                var text = await new StreamReader(responseBody).ReadToEndAsync();

                ip = context.Connection.RemoteIpAddress?.ToString();

                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }


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

            Console.WriteLine("Body: <hidden>");
            Console.WriteLine("-------------------------");

            Console.ResetColor();

            string messageLog = $"----- HTTP RESPONSE ----- \n" +
                             $"Time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} \n" +
                             $"IP: {ip} \n" +
                             $"Status: {context.Response.StatusCode} \n" +
                             $"Headers: \n";

            foreach (var header in context.Response.Headers)
            {
                messageLog += $"{header.Key}: {header.Value} \n";
            }
            messageLog += "Body: <hidden> \n";
            messageLog += "------------------------";


            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();

            await _logManager.AddLog("INFO", messageLog);
        }
    }
}
