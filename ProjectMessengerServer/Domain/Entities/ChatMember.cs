namespace ProjectMessengerServer.Domain.Entities
{
    public class ChatMember
    {
        public int ChatId { get; set; }
        public Chat Chat { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime JoinedAt { get; set; }
        public ChatRole Role { get; set; }

        public enum ChatRole
        {
            Member = 0,
            Admin = 1,
            Owner = 2
        }

        public int? LastReadMessageId { get; set; }
        public Message? LastReadMessage { get; set; }
    }
}
