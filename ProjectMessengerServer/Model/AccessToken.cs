using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessengerServer.Model
{
    public class AccessToken
    {
        public int Id { get; set; }                    // PK
        public int UserId { get; set; }               // FK на User
        public User User { get; set; } = null!;
        public string AccessTokenHash { get; set; } = null!;  // Храним ХЕШ, а не сам токен
        public DateTime ExpiresAt { get; set; }         // Время жизни токена
        public DateTime CreatedAt { get; set; }
        public bool Revoked { get; set; } // Был ли токен отозван
    }
}
