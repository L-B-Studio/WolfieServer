using System.Text;

namespace ProjectMessengerServer.Infrastructure.Utilities
{
    public class RandomStringGenerator
    {
        private const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        //private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static string GenerateRandomString(int length)
        {
            var random = new Random();

            // Используем StringBuilder для эффективного построения строки
            StringBuilder stringBuilder = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                // Выбираем случайный символ из Chars
                char randomChar = Chars[random.Next(Chars.Length)];
                stringBuilder.Append(randomChar);
            }

            return stringBuilder.ToString();
        }
    }
}
