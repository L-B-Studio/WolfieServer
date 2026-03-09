namespace ProjectMessengerServer.Application.DTO.Chat
{
    public record CreateChatRequest(string Chat_name, string Chat_type, List<string> Member_uids);
}
