namespace ProjectMessengerServer.Model
{
    public class Chat
    {
        public int Id { get; set; }
        public string Uid { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public ICollection<ChatMember>? Members { get; set; } = null!; 
        public ICollection<Message> Messages { get; set; } = new List<Message>(); 
        public int? LastMessageId { get; set; }
        public Message? LastMessage { get; set; }
    }
}
