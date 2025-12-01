using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessenger.Model
{
    public class User
    {
        public int Id { get; set; }              // PK
        public Guid PublicId { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;

        // Добавляем дату рождения
        public DateTime Birthday { get; set; }

        // Безопасное хранение пароля:
        public byte[] PasswordHash { get; set; } = null!;
        public byte[] PasswordSalt { get; set; } = null!;
        public int HashIterations { get; set; }    // сохраняем число итераций
        public DateTime CreatedAt { get; set; }
    }
}
