namespace ProjectMessengerServer.Application.DTO.Password
{
    public record ChangedPassRequest(string Token_reset, string Password, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
}
