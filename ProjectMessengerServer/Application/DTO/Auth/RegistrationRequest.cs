namespace ProjectMessengerServer.Application.DTO.Auth
{
    public record RegistrationRequest(string Username, string Email, string Password, string Birthday, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
}
