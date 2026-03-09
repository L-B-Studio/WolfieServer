namespace ProjectMessengerServer.Application.DTO.Password
{
    public record ForgotPassVerifyRequest(string Email, string Code);
}
