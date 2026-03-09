namespace ProjectMessengerServer.Domain.Entities
{
    public class UserDevice
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string? DeviceId { get; set; } = null!;
        public string? DeviceType { get; set; } = null!;
        public string? PlaceAuthorization { get; set; } = null!;
        public DateTime LastActive { get; set; }
    }
}
