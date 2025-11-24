using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace ProjectMessenger.Helpers
{
    public static class CertificateHelper
    {
        private const string PfxFile = "server.pfx"; // самоподписанный сертификат
        private const string CerFile = "server.cer";

        public static X509Certificate2 GetOrCreateCertificate()
        {
            string baseDir = AppContext.BaseDirectory;
            Path.Combine(baseDir, PfxFile);
            string pfxPassword;
            try
            {
                pfxPassword = CertPasswordManager.LoadPassword();
            }
            catch
            {
                Console.Write("Enter a new password for the certificate: ");
                pfxPassword = Console.ReadLine();
                CertPasswordManager.SavePassword(pfxPassword);
            }

            // 2️⃣ Если ничего нет — создаём самоподписанный
            if (!File.Exists(PfxFile))
            {
                Console.WriteLine("Generating a self-signed certificate...");
                var cert = CreateSelfSignedCertificate("CN=MyTestServer");
                var bytes = cert.Export(X509ContentType.Pfx, pfxPassword);
                File.WriteAllBytes(PfxFile, bytes);
                if (!File.Exists(CerFile))
                {
                    Console.WriteLine("Generating a public key...");
                    File.WriteAllBytes(CerFile, cert.Export(X509ContentType.Cert));
                }
                Console.WriteLine($"The self-signed certificate is saved in {PfxFile}");
            }


            return new X509Certificate2(PfxFile, pfxPassword, X509KeyStorageFlags.MachineKeySet);
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Добавляем SAN: localhost и 127.0.0.1 
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("MyTlsServer");
            sanBuilder.AddIpAddress(IPAddress.Parse("213.231.4.165"));
            req.CertificateExtensions.Add(sanBuilder.Build());

            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // Server auth

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            // Export & re-import to get a cert that contains the private key and is exportable
            var pfxBytes = cert.Export(X509ContentType.Pfx);
            return new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable);
        }
    }
}
