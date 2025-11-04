using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessenger.Helpers
{
    public static class PasswordHelper
    {
        // Эти параметры можно вынести в appsettings.json
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        //private const int Iterations = 100000; // Рекомендуемое кол-во итераций для PBKDF2-SHA256
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        private const int MinIterations = 100000;
        private const int MaxIterations = 120000;

        private static int GetRandomIterations()
        {
            // Используем криптографически безопасный генератор случайных чисел
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int randomValue = BitConverter.ToInt32(bytes, 0);

                // Ограничиваем случайное число безопасным диапазоном
                return Math.Abs(randomValue % (MaxIterations - MinIterations + 1)) + MinIterations;
            }
        }

        public static (byte[] hash, byte[] salt, int iterations) HashPassword(string password)
        {
            // 1. Генерируем "соль"
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            int Iterations = GetRandomIterations();

            // 2. Генерируем хэш с помощью PBKDF2
            var hash = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                _hashAlgorithm
            ).GetBytes(KeySize);

            // 3. Возвращаем хэш, соль и кол-во итераций
            return (hash, salt, Iterations);
        }

        public static bool VerifyPassword(string password, byte[] hash, byte[] salt, int iterations)
        {
            // 1. Генерируем хэш из введенного пароля, используя те же соль и итерации
            var hashToCompare = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                _hashAlgorithm
            ).GetBytes(KeySize);

            // 2. Сравниваем байт в байт.
            // Использовать CryptographicOperations.FixedTimeEquals важно,
            // чтобы предотвратить "атаки по времени" (timing attacks).
            return CryptographicOperations.FixedTimeEquals(hashToCompare, hash);
        }
    }
}
