using System.Security.Cryptography;

namespace ProjectMessengerServer.Infrastructure.Security
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        private const int MinIterations = 100000;
        private const int MaxIterations = 120000;

        private static int GetRandomIterations()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int randomValue = BitConverter.ToInt32(bytes, 0);

                return Math.Abs(randomValue % (MaxIterations - MinIterations + 1)) + MinIterations;
            }
        }

        public static (byte[] hash, byte[] salt, int iterations) HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            int Iterations = GetRandomIterations();

            var hash = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                _hashAlgorithm
            ).GetBytes(KeySize);

            return (hash, salt, Iterations);
        }

        public static bool VerifyPassword(string password, byte[] hash, byte[] salt, int iterations)
        {
            var hashToCompare = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                _hashAlgorithm
            ).GetBytes(KeySize);

            return CryptographicOperations.FixedTimeEquals(hashToCompare, hash);
        }
    }
}
