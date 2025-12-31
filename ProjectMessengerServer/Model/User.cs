using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessengerServer.Model
{
    public class User
    {
        public int Id { get; set; }              // PK
        public Guid PublicId { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Status { get; set; } // Статус пользователя (например Logger_status или Developer_status)

        // Добавляем дату рожденияS
        public DateTime Birthday { get; set; }

        // Безопасное хранение пароля:
        public byte[] PasswordHash { get; set; } = null!;
        public byte[] PasswordSalt { get; set; } = null!;
        public int HashIterations { get; set; }    // сохраняем число итераций
        public DateTime CreatedAt { get; set; }
    }
}
