using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessengerServer.Model
{
    public class RefreshToken
    {
        public int Id { get; set; }                    // PK
        public int UserId { get; set; }               // FK на User
        public User User { get; set; } = null!;
        public string RefreshTokenHash { get; set; } = null!;  // Храним ХЕШ, а не сам токен
        public DateTime ExpiresAt { get; set; }         // Время жизни токена
        public DateTime CreatedAt { get; set; }
        public bool Used { get; set; }                  // Был ли token использован
        public string DeviceInfo { get; set; } = null!; // Информация об устройстве которому был выдан токен
        public string IpAddress { get; set; } = null!;
        public bool Revoked { get; set; } // Был ли токен отозван
    }
}
