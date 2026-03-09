namespace ProjectMessengerServer.Domain.Entities
{
    public class UserPrivacy
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public bool ShowEmail { get; set; }
        public bool ShowPhoneNumber { get; set; }
        public bool ShowLastSeen { get; set; }
    }
}
