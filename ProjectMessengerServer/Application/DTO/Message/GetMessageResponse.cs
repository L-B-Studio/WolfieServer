namespace ProjectMessengerServer.Application.DTO.Message
{
    public record GetMessageResponse(string Chat_uid, string Message_id, string Sender_id, string Sender_name, string Message_text, string Created_at);
}
