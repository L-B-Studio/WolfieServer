namespace ProjectMessengerServer.Domain.Entities
{
    public class UserSequence
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int LastSeq { get; set; }
    }
}
