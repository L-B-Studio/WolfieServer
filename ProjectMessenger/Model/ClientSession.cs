using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectMessenger.Model
{
    public class ClientSession
    {
        public TcpClient Client { get; set; }
        public string IpAddress { get; set; }
        public string? LoggedInEmail { get; set; } // Email, если пользователь залогинен
        public NetworkStream Stream { get; set; }
        public StreamWriter Writer { get; set; }

        public ClientSession(TcpClient client, SslStream ssl)
        {
            Client = client;
            IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            Stream = client.GetStream();
            Writer = new StreamWriter(ssl, Encoding.UTF8) { AutoFlush = true };
        }

        public async Task SendJsonAsync(string command, Dictionary<string, string> data)
        {
            var package = new JsonPackage
            {
                header = command,
                body = data
            };

            Console.WriteLine($"Sending to {IpAddress}:{((IPEndPoint)Client.Client.RemoteEndPoint!).Port} '{JsonSerializer.Serialize(package)}'");

            string packet = JsonSerializer.Serialize(package);

            await Writer.WriteLineAsync(packet);
        }
    }
}
