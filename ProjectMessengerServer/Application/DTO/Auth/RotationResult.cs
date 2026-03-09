namespace ProjectMessengerServer.Application.DTO.Auth
{
    public class RotationResult
    {
        public string HashAccessToken { get; set; } = null!;
        public string HashRefreshToken { get; set; } = null!;
    }
}
