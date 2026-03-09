namespace ProjectMessengerServer.Application.DTO.Chat
{
    public record GetChatsResponse(string Chat_uid, string Chat_name, string Chat_type, string Last_Message_Text, string Last_Message_Sender, string Last_Message_Created_at, int Not_read_messages_count, int Last_Message_Id);
}
