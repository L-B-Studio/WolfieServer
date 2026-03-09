using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessengerServer.Domain.Entities
{
    public class PasswordResetTokenReset
    {
        public int Id { get; set; }                    // PK
        public int UserId { get; set; }               // FK на User
        public User User { get; set; } = null!;
        public string TokenResetHash { get; set; } = null!;  // Храним ХЕШ, а не сам токен
        public DateTime ExpiresAt { get; set; }         // Время жизни токена
        public DateTime CreatedAt { get; set; }
        public bool Used { get; set; }                  // Был ли token использован
        public string IpAddress { get; set; } = null!;
        public bool Revoked { get; set; } // Был ли токен отозван
    }
}
