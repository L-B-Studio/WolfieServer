namespace ProjectMessengerServer.Infrastructure.Security
{
    public class CertPasswordManager
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
}
