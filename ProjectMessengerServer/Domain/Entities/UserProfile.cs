namespace ProjectMessengerServer.Domain.Entities
{
    public class UserProfile
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string PublicId { get; set; } = null!;
        public string? PhoneNumber { get; set; } = null!;
        public string? AvatarUrl { get; set; } = null!;
        public DateTime Birthday { get; set; }
        public string? Bio { get; set; } = null!;
    }
}
