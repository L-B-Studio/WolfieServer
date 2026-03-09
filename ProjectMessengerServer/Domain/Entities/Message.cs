namespace ProjectMessengerServer.Domain.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public Chat Chat { get; set; } = null!;
        public int MessageInChatId { get; set; }
        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;
        public string Text { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
