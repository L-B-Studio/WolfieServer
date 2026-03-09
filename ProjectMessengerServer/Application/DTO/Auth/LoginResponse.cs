namespace ProjectMessengerServer.Application.DTO.Auth
{
    public record LoginResponse(string Token_refresh, string Token_access);
}
