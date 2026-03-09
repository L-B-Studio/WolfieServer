namespace ProjectMessengerServer.Domain.Entities
{
    public class UserSettings
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public bool IsDarkMode { get; set; }
        public string? Language { get; set; } = null!;
        public bool NotificationsEnabled { get; set; }
    }
}
