namespace ProjectMessengerServer.Application.DTO.Auth
{
    public record GetAccessTokenRequest(string Token_refresh, string? Device_id = null, string? Device_type = null, string? Place_authorization = null);
}
