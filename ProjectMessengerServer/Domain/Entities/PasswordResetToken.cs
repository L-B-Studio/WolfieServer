using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessengerServer.Domain.Entities
{
    public class PasswordResetToken
    {
        public int Id { get; set; }                    // PK

        public int UserId { get; set; }               // FK на User
        public User User { get; set; } = null!;

        public string TokenHash { get; set; } = null!;  // Храним ХЕШ, а не сам токен
        public DateTime ExpiresAt { get; set; }         // Время жизни токена
        public bool Used { get; set; }                  // Был ли token использован
        public DateTime CreatedAt { get; set; }
    }
}
