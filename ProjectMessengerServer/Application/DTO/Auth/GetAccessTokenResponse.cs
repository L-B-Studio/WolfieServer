namespace ProjectMessengerServer.Application.DTO.Auth
{
    public record GetAccessTokenResponse(string Token_refresh, string Token_access);
}
