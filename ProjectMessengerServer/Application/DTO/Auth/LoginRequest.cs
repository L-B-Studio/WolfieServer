namespace ProjectMessengerServer.Application.DTO.Auth
{
    public record LoginRequest(string Email, string Password, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
}
