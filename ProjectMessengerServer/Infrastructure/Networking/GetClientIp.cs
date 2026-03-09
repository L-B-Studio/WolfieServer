namespace ProjectMessengerServer.Infrastructure.Networking
{
    public class GetClientIp
    {
        public static string GetClientIpAddress(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress;

            if (ip == null)
                return "unknown";

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            return ip.ToString();
        }
    }
}
